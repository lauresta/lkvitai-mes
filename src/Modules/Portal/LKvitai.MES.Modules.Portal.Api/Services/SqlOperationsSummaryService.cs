using System.Data;
using LKvitai.MES.Modules.Portal.Api.Configuration;
using LKvitai.MES.Modules.Portal.Api.Models;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Portal.Api.Services;

/// <summary>
/// Executes <c>dbo.mes_OperationsSummary</c> against the legacy SQL Server
/// database (LKvitaiDb) and maps the five result sets into a
/// <see cref="PortalOperationsSummaryResponse"/>.
///
/// Fails soft: if <see cref="SalesDbOptions.ConnectionString"/> is absent or
/// the SP call throws, the method returns <c>null</c> so the Portal API can
/// still render partial data instead of returning 500.
/// </summary>
public sealed class SqlOperationsSummaryService
{
    private const string SpName = "dbo.mes_OperationsSummary";

    private readonly SalesDbOptions _options;
    private readonly ILogger<SqlOperationsSummaryService> _logger;

    public SqlOperationsSummaryService(
        SalesDbOptions options,
        ILogger<SqlOperationsSummaryService> logger)
    {
        _options = options;
        _logger  = logger;
    }

    /// <summary>
    /// Returns the operations summary for the requested period, or
    /// <c>null</c> when the connection string is missing or the call fails.
    /// </summary>
    public async Task<PortalOperationsSummaryResponse?> GetAsync(
        string period,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogWarning(
                "SqlOperationsSummaryService: ConnectionStrings:LKvitaiDb is not configured; " +
                "returning null. Set the env var ConnectionStrings__LKvitaiDb to enable real data.");
            return null;
        }

        // Normalise: only 'last' is a valid non-default value.
        var normalised = string.Equals(period?.Trim(), "last", StringComparison.OrdinalIgnoreCase)
            ? "last"
            : "this";

        try
        {
            await using var conn = new SqlConnection(_options.ConnectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = new SqlCommand(SpName, conn)
            {
                CommandType    = CommandType.StoredProcedure,
                CommandTimeout = _options.CommandTimeoutSeconds,
            };
            cmd.Parameters.Add(new SqlParameter("@Period", SqlDbType.NVarChar, 10)
            {
                Value = normalised
            });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);

            // ---------------------------------------------------------
            // Result set 1 — Period metadata (1 row)
            // ---------------------------------------------------------
            PortalPeriodInfo? periodInfo = null;
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                periodInfo = new PortalPeriodInfo(
                    Key:  reader.GetString(0),
                    From: reader.GetString(1),
                    To:   reader.GetString(2));
            }

            // ---------------------------------------------------------
            // Result set 2 — Stage counts (8 rows)
            // ---------------------------------------------------------
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
            var stages = new List<PortalStageDto>(8);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                stages.Add(new PortalStageDto(
                    Key:   reader.GetString(0),
                    Label: reader.GetString(1),
                    Count: reader.GetInt32(2)));
            }

            // ---------------------------------------------------------
            // Result set 3 — Raw status counts
            // ---------------------------------------------------------
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
            var statuses = new List<PortalStatusCountDto>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                statuses.Add(new PortalStatusCountDto(
                    Status: reader.GetString(0),
                    Count:  reader.GetInt32(1)));
            }

            // ---------------------------------------------------------
            // Result set 4 — Created by day
            // ---------------------------------------------------------
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
            var createdByDay = new List<PortalDayCountDto>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                createdByDay.Add(new PortalDayCountDto(
                    Date:  reader.GetString(0),
                    Count: reader.GetInt32(1)));
            }

            // ---------------------------------------------------------
            // Result set 5 — Completed by day
            // ---------------------------------------------------------
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
            var completedByDay = new List<PortalDayCountDto>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                completedByDay.Add(new PortalDayCountDto(
                    Date:  reader.GetString(0),
                    Count: reader.GetInt32(1)));
            }

            return new PortalOperationsSummaryResponse(
                Period:         periodInfo ?? new PortalPeriodInfo(normalised, string.Empty, string.Empty),
                Stages:         stages,
                Statuses:       statuses,
                CreatedByDay:   createdByDay,
                CompletedByDay: completedByDay);
        }
        catch (Exception ex) when (ex is SqlException
                                      or TaskCanceledException
                                      or InvalidOperationException
                                      or InvalidCastException)
        {
            _logger.LogWarning(ex,
                "SqlOperationsSummaryService: failed to fetch operations summary for period={Period}",
                normalised);
            return null;
        }
    }
}
