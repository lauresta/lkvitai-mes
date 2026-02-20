namespace LKvitai.MES.Modules.Warehouse.Api.Security;

public sealed class MfaOptions
{
    public const string SectionName = "Mfa";

    public bool Enabled { get; set; } = true;
    public List<string> RequiredRoles { get; set; } =
    [
        WarehouseRoles.WarehouseAdmin,
        WarehouseRoles.WarehouseManager,
        "Admin",
        "Manager"
    ];

    public int SessionTimeoutHours { get; set; } = 8;
    public int ChallengeTimeoutMinutes { get; set; } = 10;
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
