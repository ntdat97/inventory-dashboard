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

    /// <summary>
    /// UTC date the vehicle left active inventory (sold / transferred / sent to auction). Null while the vehicle is
    /// still held. This is the "as-of" anchor that freezes the derived aging + carrying-cost metrics at the moment the
    /// unit stopped being capital-at-risk — a closed unit's days-in-inventory is a historical fact, not a live counter.
    /// Invariant: set iff <see cref="Status"/> is a closed status (see <c>VehicleStatusExtensions.IsClosed</c>).
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    public Dealership? Dealership { get; set; }
    public ICollection<InventoryAction> Actions { get; set; } = new List<InventoryAction>();

    public void Reserve()
    {
        if (Status != VehicleStatus.InStock)
        {
            throw new InvalidOperationException($"Vehicle {Id} is {Status} and cannot be reserved.");
        }

        Status = VehicleStatus.Reserved;
    }

    public void ReleaseReservation()
    {
        if (Status != VehicleStatus.Reserved)
        {
            throw new InvalidOperationException($"Vehicle {Id} is {Status} and cannot release a reservation.");
        }

        Status = VehicleStatus.InStock;
    }

    /// <summary>
    /// Takes the vehicle out of active inventory: stamps a closed <paramref name="status"/> and the
    /// <paramref name="asOf"/> anchor together, so the <c>ClosedDate set iff Status is closed</c> invariant is
    /// enforced in one place. A unit closes exactly once — re-closing an already-closed vehicle is a programming
    /// error, not a legal transition (the write path gates on <see cref="VehicleStatusExtensions.IsClosed"/> first,
    /// so this guard is defence-in-depth for the invariant).
    /// </summary>
    public void Close(VehicleStatus status, DateTime asOf)
    {
        if (!status.IsClosed())
        {
            throw new ArgumentException($"{status} is not a closed status.", nameof(status));
        }

        if (Status.IsClosed())
        {
            throw new InvalidOperationException($"Vehicle {Id} is already {Status} and cannot be closed again.");
        }

        Status = status;
        ClosedDate = asOf;
    }
}
