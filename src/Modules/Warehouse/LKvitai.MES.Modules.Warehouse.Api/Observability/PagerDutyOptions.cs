namespace LKvitai.MES.Modules.Warehouse.Api.Observability;

public sealed class PagerDutyOptions
{
    public const string SectionName = "PagerDuty";

    // PagerDuty Events API routing key.
    public string ApiKey { get; set; } = string.Empty;

    public string EventsApiUrl { get; set; } = "https://events.pagerduty.com/v2/enqueue";

    public string Source { get; set; } = "lkvitai-mes";
}
