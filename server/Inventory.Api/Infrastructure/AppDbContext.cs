using Inventory.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Dealership> Dealerships => Set<Dealership>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<InventoryAction> InventoryActions => Set<InventoryAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dealership>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Vin).IsRequired().HasMaxLength(17);
            entity.HasIndex(v => v.Vin).IsUnique();
            entity.HasIndex(v => v.DealershipId);
            entity.HasIndex(v => v.AcquisitionDate);
            entity.HasIndex(v => v.Make);
            entity.Property(v => v.Make).IsRequired().HasMaxLength(100);
            entity.Property(v => v.Model).IsRequired().HasMaxLength(100);
            entity.Property(v => v.AcquisitionCost).HasPrecision(18, 2);
            entity.Property(v => v.ListPrice).HasPrecision(18, 2);

            entity.HasOne(v => v.Dealership)
                .WithMany(d => d.Vehicles)
                .HasForeignKey(v => v.DealershipId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryAction>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.VehicleId);
            entity.Property(a => a.ProposedValue).HasPrecision(18, 2);
            entity.Property(a => a.Note).HasMaxLength(2000);

            entity.HasOne(a => a.Vehicle)
                .WithMany(v => v.Actions)
                .HasForeignKey(a => a.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
