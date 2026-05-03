using System.Data;
using System.Diagnostics;
using LKvitai.MES.Modules.Frontline.Application.Ports;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Sql;

/// <summary>
/// SQL Server adapter for <see cref="IFabricLookupRecorder"/>. Calls
/// <c>dbo.mes_Fabric_RecordLookup</c> (F-2.1), which inside a single
/// transaction stamps <c>TBD_Components.mes_LastCheckedAt</c> for the
/// matching fabric and appends a row to
/// <c>dbo.mes_FabricAvailabilityCheckLog</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never throws.</b> Audit-log failures must not poison the look-up
/// response — the operator on the floor needs to see the stock card even if
/// the bookkeeping table is unavailable. Any exception is logged at warning
/// level and swallowed; <see cref="OperationCanceledException"/> on the
/// caller's token is the single exception that propagates so request
/// cancellation still tears down cleanly.
/// </para>
/// <para>
/// <b>Connection lifecycle.</b> A fresh <see cref="SqlConnection"/> per
/// call, opened then disposed in the same scope. The proc itself is
/// sub-millisecond on the existing log table; no pooling tuning required
/// for the look-up volume Frontline expects in F-2 (≤ 1 RPS per warehouse).
/// </para>
/// </remarks>
public sealed class SqlFabricLookupRecorder : IFabricLookupRecorder
{
    private const string ProcName = "dbo.mes_Fabric_RecordLookup";

    private readonly FrontlineSqlOptions _options;
    private readonly ILogger<SqlFabricLookupRecorder> _logger;

    public SqlFabricLookupRecorder(FrontlineSqlOptions options, ILogger<SqlFabricLookupRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "SqlFabricLookupRecorder requires a non-empty FrontlineSqlOptions.ConnectionString. " +
                "Configure ConnectionStrings:LKvitaiDb or switch Frontline:FabricDataSource to 'Stub'.");
        }

        _options = options;
        _logger  = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RecordAsync(string code, string? checkedBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(_options.ConnectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = new SqlCommand(ProcName, conn)
            {
                CommandType    = CommandType.StoredProcedure,
                CommandTimeout = _options.CommandTimeoutSeconds,
            };
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 32) { Value = code });
            cmd.Parameters.Add(new SqlParameter("@CheckedBy", SqlDbType.NVarChar, 64)
            {
                Value = string.IsNullOrWhiteSpace(checkedBy) ? DBNull.Value : checkedBy,
            });

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            sw.Stop();
            _logger.LogInformation(
                "[FrontlinePerf] sql.RecordLookup code={Code} checkedBy={CheckedBy} elapsedMs={ElapsedMs}",
                code, checkedBy ?? "<null>", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // Caller cancelled the request — propagate so the request
            // pipeline can tear down without surfacing a misleading warning.
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "[FrontlinePerf] sql.RecordLookup failed code={Code} checkedBy={CheckedBy} elapsedMs={ElapsedMs}. " +
                "Lookup response will still be returned to the operator.",
                code, checkedBy ?? "<null>", sw.ElapsedMilliseconds);
        }
    }
}
