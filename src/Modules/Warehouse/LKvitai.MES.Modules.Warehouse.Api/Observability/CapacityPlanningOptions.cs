namespace LKvitai.MES.Modules.Warehouse.Api.Observability;

public sealed class CapacityPlanningOptions
{
    public const string SectionName = "CapacityPlanning";

    public double AllocatedDatabaseStorageGb { get; set; } = 150;

    public double DatabaseGrowthPerMonthGb { get; set; } = 5;

    public double CurrentEventsPerDay { get; set; } = 10000;

    public double EventGrowthPerDay { get; set; } = 83.3;

    public int ForecastMonths { get; set; } = 6;

    public double DatabaseWarningPercent { get; set; } = 80;

    public double LocationCriticalPercent { get; set; } = 90;

    public double EventWarningPerDay { get; set; } = 1_000_000;
}
