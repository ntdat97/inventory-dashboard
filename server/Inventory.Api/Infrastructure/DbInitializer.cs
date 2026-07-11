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

        // (daysInInventory, make, model, year, acquisitionCost, listPrice) spread across all tiers,
        // including the exact tier boundaries (30/60/90).
        var seedVehicles = new (int Days, string Make, string Model, int Year, decimal Cost, decimal ListPrice)[]
        {
            (5, "Toyota", "Camry", 2024, 24000m, 27500m),
            (18, "Honda", "Civic", 2024, 21000m, 24000m),
            (30, "Mazda", "CX-5", 2023, 26000m, 29500m),
            (38, "Ford", "F-150", 2023, 38000m, 43000m),
            (52, "Kia", "Sportage", 2023, 25000m, 28500m),
            (60, "Subaru", "Outback", 2022, 27000m, 30500m),
            (66, "Hyundai", "Tucson", 2022, 24500m, 27800m),
            (80, "Nissan", "Altima", 2022, 19500m, 22800m),
            (90, "Chevrolet", "Equinox", 2022, 23000m, 26200m),
            (95, "Volkswagen", "Jetta", 2021, 18500m, 21600m),
            (130, "Jeep", "Grand Cherokee", 2021, 31000m, 34500m),
            (210, "BMW", "3 Series", 2020, 29000m, 33200m),
        };

        var vehicles = seedVehicles.Select((seed, index) => new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = $"SEED{Guid.NewGuid():N}".Substring(0, 17).ToUpperInvariant(),
            DealershipId = index % 2 == 0 ? downtown.Id : uptown.Id,
            Make = seed.Make,
            Model = seed.Model,
            Year = seed.Year,
            AcquisitionDate = clock.UtcNow.AddDays(-seed.Days),
            AcquisitionCost = seed.Cost,
            ListPrice = seed.ListPrice,
            Status = VehicleStatus.InStock,
        }).ToList();

        db.Vehicles.AddRange(vehicles);

        await db.SaveChangesAsync();
    }
}
