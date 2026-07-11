using Serilog.Context;

namespace Inventory.Api.Infrastructure.Observability;

/// <summary>
/// Assigns a correlation id to every request (honouring an inbound <c>X-Correlation-Id</c> when present, else
/// generating one), pushes it into the Serilog <see cref="LogContext"/> so every log line for the request carries it,
/// and echoes it back on the response header so clients can correlate. First middleware in the pipeline (design §9).
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const string LogPropertyName = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        // Make it available to handlers (e.g. ProblemDetails enrichment) and downstream.
        context.Items[LogPropertyName] = correlationId;
        context.TraceIdentifier = correlationId;

        // Echo it back before the body is written.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var inbound))
        {
            var value = inbound.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
