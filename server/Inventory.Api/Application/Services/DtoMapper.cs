using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Entities;

namespace Inventory.Api.Application.Services;

/// <summary>Entity -> DTO mapping shared by the read and write services.</summary>
internal static class DtoMapper
{
    public static ActionDto ToActionDto(InventoryAction a) => new(
        a.Id, a.VehicleId, a.Type, a.Status, a.ProposedValue, a.Note, a.Outcome, a.CreatedAt, a.UpdatedAt);
}
