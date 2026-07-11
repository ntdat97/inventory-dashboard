using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Domain.Services;

public record AgingResult(int DaysInInventory, AgingTier Tier, int? DaysUntilAging);
