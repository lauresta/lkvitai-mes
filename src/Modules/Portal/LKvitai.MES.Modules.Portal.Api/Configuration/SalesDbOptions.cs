namespace LKvitai.MES.Modules.Portal.Api.Configuration;

/// <summary>
/// Connection settings for the legacy SQL Server database (LKvitaiDb).
/// Used by <c>SqlOperationsSummaryService</c> to run
/// <c>dbo.mes_OperationsSummary</c> against the same database that
/// Sales.Api uses. Configure via <c>ConnectionStrings:LKvitaiDb</c>
/// (environment variable <c>ConnectionStrings__LKvitaiDb</c>).
/// If the connection string is absent the Portal API starts normally
/// but the operations summary endpoint returns null / empty data.
/// </summary>
public sealed class SalesDbOptions
{
    public string? ConnectionString { get; init; }
    public int CommandTimeoutSeconds { get; init; } = 30;
}
