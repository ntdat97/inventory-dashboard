namespace Inventory.Api.Domain.Configuration;

/// <summary>Tier upper bounds in days, per SYSTEM-DESIGN A3 (Fresh 0-30, Watch 31-60, Aging 61-90, Critical 91+).</summary>
public class AgingConfig
{
    public int FreshMaxDays { get; set; } = 30;
    public int WatchMaxDays { get; set; } = 60;
    public int AgingMaxDays { get; set; } = 90;
}
