namespace LKvitai.MES.Api.Observability;

public sealed class AlertEscalationOptions
{
    public const string SectionName = "AlertEscalation";

    public int DeduplicationWindowMinutes { get; set; } = 5;

    public EscalationPolicyOptions EscalationPolicy { get; set; } = new();

    public RoutingOptions Routing { get; set; } = new();

    public OnCallScheduleOptions OnCallSchedule { get; set; } = new();

    public string RunbookBaseUrl { get; set; } = string.Empty;
}

public sealed class EscalationPolicyOptions
{
    public int L1Minutes { get; set; } = 5;

    public int L2Minutes { get; set; } = 15;

    public int L3Minutes { get; set; } = 30;
}

public sealed class RoutingOptions
{
    public string Critical { get; set; } = "pagerduty";

    public string Warning { get; set; } = "email";

    public string Info { get; set; } = "slack";
}

public sealed class OnCallScheduleOptions
{
    public bool Enabled { get; set; } = true;

    public string Rotation { get; set; } = "weekly";

    public List<string> TeamMembers { get; set; } =
    [
        "L1 On-call",
        "L2 On-call",
        "L3 Manager"
    ];
}
