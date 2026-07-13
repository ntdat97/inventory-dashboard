using FluentAssertions;
using Inventory.Api.Domain.Entities;
using Inventory.Api.Domain.Enums;

namespace Inventory.Tests.Domain.Entities;

/// <summary>
/// Pins the closing invariant that lives on the entity: <c>Close</c> is the one place Status and ClosedDate move
/// together, it refuses non-closed statuses, and a unit closes exactly once. The exit-lane mapping (which closed
/// status a resolved deal produces) is asserted alongside it.
/// </summary>
public class VehicleClosingTests
{
    private static readonly DateTime AsOf = new(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);

    private static Vehicle ActiveVehicle(VehicleStatus status = VehicleStatus.InStock) => new()
    {
        Id = Guid.NewGuid(),
        Status = status,
        AcquisitionDate = AsOf.AddDays(-40),
    };

    [Theory]
    [InlineData(VehicleStatus.Sold)]
    [InlineData(VehicleStatus.Transferred)]
    [InlineData(VehicleStatus.AtAuction)]
    public void Close_StampsStatusAndClosedDateTogether(VehicleStatus closed)
    {
        var vehicle = ActiveVehicle();

        vehicle.Close(closed, AsOf);

        vehicle.Status.Should().Be(closed);
        vehicle.ClosedDate.Should().Be(AsOf);
    }

    [Theory]
    [InlineData(VehicleStatus.InStock)]
    [InlineData(VehicleStatus.Reserved)]
    public void Close_RejectsNonClosedStatus_AndLeavesVehicleUntouched(VehicleStatus notClosed)
    {
        var vehicle = ActiveVehicle();

        var act = () => vehicle.Close(notClosed, AsOf);

        act.Should().Throw<ArgumentException>();
        vehicle.Status.Should().Be(VehicleStatus.InStock);
        vehicle.ClosedDate.Should().BeNull();
    }

    [Fact]
    public void Close_OnAlreadyClosedVehicle_Throws()
    {
        var vehicle = ActiveVehicle();
        vehicle.Close(VehicleStatus.Sold, AsOf);

        var act = () => vehicle.Close(VehicleStatus.Transferred, AsOf.AddDays(1));

        act.Should().Throw<InvalidOperationException>();
        vehicle.Status.Should().Be(VehicleStatus.Sold);
        vehicle.ClosedDate.Should().Be(AsOf);
    }

    [Fact]
    public void Reserve_FromInStock_MarksVehicleReserved_WithoutClosingIt()
    {
        var vehicle = ActiveVehicle();

        vehicle.Reserve();

        vehicle.Status.Should().Be(VehicleStatus.Reserved);
        vehicle.ClosedDate.Should().BeNull();
    }

    [Theory]
    [InlineData(VehicleStatus.Reserved)]
    [InlineData(VehicleStatus.Sold)]
    [InlineData(VehicleStatus.Transferred)]
    [InlineData(VehicleStatus.AtAuction)]
    public void Reserve_FromAnyNonInStockStatus_Throws(VehicleStatus status)
    {
        var vehicle = ActiveVehicle(status);

        var act = () => vehicle.Reserve();

        act.Should().Throw<InvalidOperationException>();
        vehicle.Status.Should().Be(status);
    }

    [Fact]
    public void ReleaseReservation_FromReserved_MarksVehicleInStock_WithoutClosingIt()
    {
        var vehicle = ActiveVehicle(VehicleStatus.Reserved);

        vehicle.ReleaseReservation();

        vehicle.Status.Should().Be(VehicleStatus.InStock);
        vehicle.ClosedDate.Should().BeNull();
    }

    [Theory]
    [InlineData(VehicleStatus.InStock)]
    [InlineData(VehicleStatus.Sold)]
    [InlineData(VehicleStatus.Transferred)]
    [InlineData(VehicleStatus.AtAuction)]
    public void ReleaseReservation_FromAnyNonReservedStatus_Throws(VehicleStatus status)
    {
        var vehicle = ActiveVehicle(status);

        var act = () => vehicle.ReleaseReservation();

        act.Should().Throw<InvalidOperationException>();
        vehicle.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(ActionType.PriceReduction, VehicleStatus.Sold)]
    [InlineData(ActionType.Promote, VehicleStatus.Sold)]
    [InlineData(ActionType.Recondition, VehicleStatus.Sold)]
    [InlineData(ActionType.Other, VehicleStatus.Sold)]
    [InlineData(ActionType.Transfer, VehicleStatus.Transferred)]
    [InlineData(ActionType.Auction, VehicleStatus.AtAuction)]
    public void SaleDestination_MapsEachActionTypeToItsExitLane(ActionType type, VehicleStatus expected)
    {
        type.SaleDestination().Should().Be(expected);
    }
}
