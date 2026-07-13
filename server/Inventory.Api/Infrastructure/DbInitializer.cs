using Inventory.Api.Domain.Entities;
using Inventory.Api.Domain.Enums;
using Inventory.Api.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Infrastructure;

/// <summary>Seeds 2 dealerships and vehicles spanning every aging tier, anchored to the injected clock.</summary>
public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db, IClock clock)
    {
        if (await db.Dealerships.AnyAsync())
        {
            return;
        }

        var downtown = new Dealership { Id = Guid.NewGuid(), Name = "DatNguyen Motors - Downtown", CreatedAt = clock.UtcNow };
        var uptown = new Dealership { Id = Guid.NewGuid(), Name = "DatNguyen Motors - Uptown", CreatedAt = clock.UtcNow };
        db.Dealerships.AddRange(downtown, uptown);

        // (daysInInventory, make, model, trim, colour, mileage, year, acquisitionCost, listPrice, status, closedDaysAgo)
        // spread across every aging tier — including the exact boundaries (30/60/90) — and every VehicleStatus,
        // so the dashboard can be exercised end to end. Status is assigned to read like a real lot: fresh stock
        // gets reserved/sold, mid-life stays in stock or transfers between rooftops, and the oldest metal is
        // pushed out to auction — the aging → action story the tool is meant to surface.
        //
        // closedDaysAgo: for a closed unit (sold/transferred/auctioned) this is how many days ago it LEFT inventory.
        // Its ClosedDate = now - closedDaysAgo freezes days-in-inventory + carrying cost at the moment it stopped being
        // capital-at-risk (held-days = Days - closedDaysAgo), instead of letting those figures run forever. 0 = still held.
        var seedVehicles = new (int Days, string Make, string Model, string Trim, string Color, int Mileage, int Year, decimal Cost, decimal ListPrice, VehicleStatus Status, int ClosedDaysAgo)[]
        {
            // Fresh (< 30 days)
            (3, "Toyota", "Camry", "XSE", "Wind Chill Pearl", 12, 2024, 24000m, 27500m, VehicleStatus.InStock, 0),
            (7, "Tesla", "Model 3", "Long Range", "Midnight Silver", 8, 2024, 39000m, 44500m, VehicleStatus.Reserved, 0),
            (12, "Honda", "Civic", "Sport", "Rallye Red", 21, 2024, 21000m, 24000m, VehicleStatus.Sold, 5),
            (18, "Lexus", "RX 350", "F Sport", "Nightfall Mica", 15, 2024, 47000m, 53500m, VehicleStatus.InStock, 0),
            (24, "Mazda", "CX-30", "Premium", "Soul Red Crystal", 19, 2024, 27000m, 30500m, VehicleStatus.Reserved, 0),
            // Watch (30–59)
            (30, "Mazda", "CX-5", "Carbon Edition", "Machine Gray", 26, 2023, 26000m, 29500m, VehicleStatus.InStock, 0),
            (38, "Ford", "F-150", "Lariat", "Oxford White", 33, 2023, 38000m, 43000m, VehicleStatus.InStock, 0),
            (45, "Audi", "Q5", "Premium Plus", "Mythos Black", 29, 2023, 42000m, 47800m, VehicleStatus.Sold, 10),
            (52, "Kia", "Sportage", "X-Line", "Fusion Black", 24, 2023, 25000m, 28500m, VehicleStatus.Transferred, 12),
            // Aging (60–89)
            (60, "Subaru", "Outback", "Onyx Edition", "Crystal Black", 31, 2022, 27000m, 30500m, VehicleStatus.InStock, 0),
            (66, "Hyundai", "Tucson", "Limited", "Amazon Gray", 41, 2022, 24500m, 27800m, VehicleStatus.InStock, 0),
            (72, "BMW", "X3", "xDrive30i", "Phytonic Blue", 38, 2022, 44000m, 49500m, VehicleStatus.Transferred, 20),
            (80, "Nissan", "Altima", "SR", "Gun Metallic", 52, 2022, 19500m, 22800m, VehicleStatus.InStock, 0),
            (85, "Mercedes-Benz", "C 300", "AMG Line", "Selenite Grey", 36, 2022, 46000m, 51900m, VehicleStatus.Reserved, 0),
            // Critical (>= 90)
            (90, "Chevrolet", "Equinox", "LT", "Summit White", 47, 2022, 23000m, 26200m, VehicleStatus.InStock, 0),
            (95, "Volkswagen", "Jetta", "SEL", "Pure Gray", 58, 2021, 18500m, 21600m, VehicleStatus.AtAuction, 30),
            (110, "Jeep", "Grand Cherokee", "Limited", "Diamond Black", 62, 2021, 31000m, 34500m, VehicleStatus.Transferred, 25),
            (128, "Acura", "MDX", "Technology", "Liquid Carbon", 55, 2021, 34000m, 38200m, VehicleStatus.InStock, 0),
            (155, "Ford", "Mustang", "GT Premium", "Grabber Blue", 44, 2021, 41000m, 46500m, VehicleStatus.AtAuction, 60),
            (180, "BMW", "3 Series", "330i", "Alpine White", 61, 2020, 29000m, 33200m, VehicleStatus.Sold, 70),
            (210, "Porsche", "Macan", "S", "Carrara White", 49, 2020, 52000m, 58900m, VehicleStatus.AtAuction, 80),
            (240, "Land Rover", "Discovery", "SE", "Santorini Black", 68, 2020, 48000m, 53500m, VehicleStatus.AtAuction, 90),
        };

        var vehicles = seedVehicles.Select((seed, index) => new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = $"SEED{Guid.NewGuid():N}".Substring(0, 17).ToUpperInvariant(),
            DealershipId = index % 2 == 0 ? downtown.Id : uptown.Id,
            Make = seed.Make,
            Model = seed.Model,
            Year = seed.Year,
            Trim = seed.Trim,
            Color = seed.Color,
            Mileage = seed.Mileage * 1000,
            AcquisitionDate = clock.UtcNow.AddDays(-seed.Days),
            AcquisitionCost = seed.Cost,
            ListPrice = seed.ListPrice,
            Status = seed.Status,
            // Closed units freeze their metrics at the day they left the lot; active units stay null (accrue live).
            ClosedDate = seed.Status.IsClosed() ? clock.UtcNow.AddDays(-seed.ClosedDaysAgo) : null,
        }).ToList();

        db.Vehicles.AddRange(vehicles);

        // A little history on a couple of closed units so the "review the frozen record" path has something to show:
        // the price cut that ultimately moved the car, already resolved as Sold. Retained, read-only.
        var soldWithHistory = vehicles.Where(v => v.Status == VehicleStatus.Sold && v.ClosedDate is not null);
        foreach (var sold in soldWithHistory)
        {
            var closedAt = sold.ClosedDate!.Value;
            db.InventoryActions.Add(new InventoryAction
            {
                Id = Guid.NewGuid(),
                VehicleId = sold.Id,
                Type = ActionType.PriceReduction,
                Status = ActionStatus.Resolved,
                ProposedValue = Math.Round(sold.ListPrice * 0.95m, 0),
                Note = "Price cut to close the deal.",
                Outcome = ActionOutcome.Sold,
                CreatedAt = closedAt.AddDays(-2),
                UpdatedAt = closedAt,
            });
        }

        await db.SaveChangesAsync();
    }
}
