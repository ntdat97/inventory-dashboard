using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Inventory.Api.Application.Auth;
using Inventory.Api.Application.Recommendations;
using Inventory.Api.Application.Services;
using Inventory.Api.Controllers;
using Inventory.Api.Domain.Configuration;
using Inventory.Api.Domain.Services;
using Inventory.Api.Infrastructure;
using Inventory.Api.Infrastructure.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

// Named CORS policy for the SPA origin(s); registered from config and applied in the request pipeline below.
const string SpaCorsPolicy = "SpaCors";

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Structured JSON logs, correlation-id enriched (the id is pushed into LogContext by the middleware). design §9.
    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter()));

    builder.Services
        .AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Consistent RFC 7807 error contract; enrich every problem with the request's correlation id (ties errors to logs).
    builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
    {
        if (ctx.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId) && correlationId is string id)
        {
            ctx.ProblemDetails.Extensions["correlationId"] = id;
        }
    });

    // Readiness: DB connectivity (hard) + AI reachability (soft — AI-down reports Degraded, not Unhealthy). design §9.
    builder.Services.AddHealthChecks()
        .AddCheck<DbHealthCheck>("database")
        .AddCheck<AiHealthCheck>("ai");

    // Auth: JWT bearer accepting the demo bearer and (when configured) Entra-issued tokens; current-user seam. design §2 A7.
    builder.Services.AddInventoryAuth(builder.Configuration);

    // CORS so the SPA (separate origin) can call the live API. Origins are config-driven (Cors:AllowedOrigins) so the
    // deployed frontend URL is added via env without a code change; defaults to the Vite dev origin for local work.
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("X-Correlation-Id")));

    // Application-specific Prometheus metrics (AI outcome/latency + business gauge); HTTP metrics come from UseHttpMetrics.
    builder.Services.AddSingleton<AppMetrics>();

    builder.Services.Configure<AgingConfig>(builder.Configuration.GetSection("Aging"));
    builder.Services.Configure<CarryingCostConfig>(builder.Configuration.GetSection("CarryingCost"));
    builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));

    // Pure domain logic (clock injected).
    builder.Services.AddSingleton<IClock, SystemClock>();
    builder.Services.AddScoped<AgingCalculator>();
    builder.Services.AddScoped<CarryingCostCalculator>();
    builder.Services.AddScoped<ActionWorkflow>();
    builder.Services.AddSingleton<BaselineRecommender>();

    // Application services (read path, action lifecycle, recommendation).
    builder.Services.AddScoped<InventoryService>();
    builder.Services.AddScoped<ActionService>();
    builder.Services.AddScoped<VehicleReservationService>();
    builder.Services.AddScoped<RecommendationService>();

    // Request validation.
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // AI: provider-agnostic client behind a Polly-based resilience handler (timeout/retry/circuit-breaker).
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient<IAiClient, HttpAiClient>()
        .AddStandardResilienceHandler();

    // Per-vehicle rate limit on the recommendation endpoint (cost/abuse guard on the public demo).
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(RateLimitPolicies.VehicleRecommendation, httpContext =>
        {
            var aiOptions = httpContext.RequestServices.GetRequiredService<IOptions<AiOptions>>().Value;
            var partitionKey = httpContext.Request.RouteValues["id"]?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = aiOptions.RateLimitPermitPerVehicle,
                Window = TimeSpan.FromSeconds(aiOptions.RateLimitWindowSeconds),
            });
        });
    });

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    var app = builder.Build();

    // Correlation id first so every log line (incl. request-completion + error logs) and every response carries it.
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Unhandled errors -> RFC 7807 ProblemDetails (no stack leak); logged with the correlation id already in scope.
    app.UseExceptionHandler();

    // One structured completion log per request (method, path, status, elapsed), correlation-id enriched.
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseRouting();

    // CORS must sit after routing and before auth so preflight is answered and the SPA's credentialed calls are allowed.
    app.UseCors(SpaCorsPolicy);

    // HTTP request count/latency by route (Prometheus) — must sit after routing so the route label is known.
    app.UseHttpMetrics();

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapMetrics(); // /metrics (Prometheus scrape endpoint)

    // Liveness: process is up (runs no dependency checks). Readiness: DB + AI (AI-down = Degraded, still 200). design §9.
    app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        ResponseWriter = HealthResponseWriter.WriteJson,
    });

    // Dev/Testing-only hook to exercise the global exception handler (unhandled error -> ProblemDetails, no stack leak).
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        app.MapGet("/api/_diag/boom", () => { throw new InvalidOperationException("Intentional failure for observability verification."); });
    }

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
        await DbInitializer.SeedAsync(db, clock);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Inventory.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed so the integration tests can bootstrap the app via <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
