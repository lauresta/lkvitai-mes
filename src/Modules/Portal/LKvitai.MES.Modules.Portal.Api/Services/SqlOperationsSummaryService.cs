using System.Data;
using LKvitai.MES.Modules.Portal.Api.Configuration;
using LKvitai.MES.Modules.Portal.Api.Models;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Portal.Api.Services;

/// <summary>
/// Executes <c>dbo.mes_OperationsSummary</c> against the legacy SQL Server
/// database (LKvitaiDb) and maps the six result sets into a
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
                    Date:  ((DateTime)reader.GetValue(0)).ToString("yyyy-MM-dd"),
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
                    Date:  ((DateTime)reader.GetValue(0)).ToString("yyyy-MM-dd"),
                    Count: reader.GetInt32(1)));
            }

            // ---------------------------------------------------------
            // Result set 6 — Branches on track
            // ---------------------------------------------------------
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
            var branchesOnTrack = new List<PortalBranchOnTrackDto>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                branchesOnTrack.Add(new PortalBranchOnTrackDto(
                    Branch:         reader.GetString(0),
                    ReadyBasis:     reader.GetString(1),
                    Ready:          reader.GetInt32(2),
                    Issued:         reader.GetInt32(3),
                    OnTrackPercent: reader.IsDBNull(4) ? null : reader.GetInt32(4)));
            }

            var resolvedPeriod = periodInfo ?? new PortalPeriodInfo(normalised, string.Empty, string.Empty);

            return new PortalOperationsSummaryResponse(
                Period:          resolvedPeriod,
                Stages:          stages,
                Statuses:        statuses,
                CreatedByDay:    PadToFullPeriod(createdByDay,   resolvedPeriod.From, resolvedPeriod.To),
                CompletedByDay:  PadToFullPeriod(completedByDay, resolvedPeriod.From, resolvedPeriod.To),
                BranchesOnTrack: branchesOnTrack);
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

    /// <summary>
    /// Expands a sparse list of day-count entries into a dense list that
    /// contains one entry for every calendar day in [from, to], filling
    /// missing days with a zero count.  This ensures the UI bar chart
    /// always renders the full month width regardless of data density.
    /// </summary>
    private static IReadOnlyList<PortalDayCountDto> PadToFullPeriod(
        IReadOnlyList<PortalDayCountDto> data,
        string fromStr,
        string toStr)
    {
        if (!DateTime.TryParse(fromStr, out var from) ||
            !DateTime.TryParse(toStr,   out var to)   || from > to)
        {
            return data;
        }

        var lookup = data.ToDictionary(d => d.Date, StringComparer.Ordinal);
        var result = new List<PortalDayCountDto>((int)(to - from).TotalDays + 1);

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var key = day.ToString("yyyy-MM-dd");
            result.Add(lookup.TryGetValue(key, out var dto)
                ? dto
                : new PortalDayCountDto(key, 0));
        }

        return result;
    }
}
