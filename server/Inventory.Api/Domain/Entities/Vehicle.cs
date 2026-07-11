using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public Guid DealershipId { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Trim { get; set; }
    public string? Color { get; set; }
    public int? Mileage { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal ListPrice { get; set; }
    public VehicleStatus Status { get; set; }

    public Dealership? Dealership { get; set; }
    public ICollection<InventoryAction> Actions { get; set; } = new List<InventoryAction>();
}
