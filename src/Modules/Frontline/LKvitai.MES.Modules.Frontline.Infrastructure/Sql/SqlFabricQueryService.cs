using System.Data;
using System.Diagnostics;
using System.Globalization;
using LKvitai.MES.Modules.Frontline.Application.Ports;
using LKvitai.MES.Modules.Frontline.Contracts.Common;
using LKvitai.MES.Modules.Frontline.Contracts.Fabric;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Sql;

/// <summary>
/// SQL Server adapter for <see cref="IFabricQueryService"/> over the legacy
/// <c>LKvitaiDb</c> database. Lookup card calls
/// <c>dbo.mes_Fabric_GetMobileCard</c> (F-2.2 successor to the legacy
/// <c>weblb_Fabric_GetMobileCard</c>); the low-stock list is reserved for
/// F-2.3 and currently throws.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>dbo.mes_Fabric_GetMobileCard</c> — returns 3 result sets (main,
///   widths, alternatives). Unknown / blacklisted code → zero result sets;
///   the adapter treats "no rows in RS1" as a 404 to keep the proc cheap on
///   the unhappy path.</item>
///   <item>Status integers in RS2/RS3 map 1:1 to
///   <see cref="FabricAvailabilityStatus"/> (1 Enough, 2 Low, 3 None,
///   4 Discontinued, 0 Unknown).</item>
///   <item>Selected width is picked client-side: explicit <c>?width=</c>
///   wins when present in the returned set; otherwise the smallest available
///   width — same rule as the F-1 in-memory stub and the legacy
///   <c>FabricAvailabilityController.Mobile</c>.</item>
/// </list>
/// All column reads are <see cref="DBNull"/>-guarded; missing columns or
/// unexpected types degrade to safe defaults instead of throwing, so a
/// single bad row never blanks the whole card.
/// </remarks>
public sealed class SqlFabricQueryService : IFabricQueryService
{
    private const string GetMobileCardProcName = "dbo.mes_Fabric_GetMobileCard";
    private const string PlaceholderPhotoUrl   = "/img/fabric_pl.png";

    private readonly FrontlineSqlOptions _options;
    private readonly ILogger<SqlFabricQueryService> _logger;

    public SqlFabricQueryService(FrontlineSqlOptions options, ILogger<SqlFabricQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "SqlFabricQueryService requires a non-empty FrontlineSqlOptions.ConnectionString. " +
                "Configure ConnectionStrings:LKvitaiDb or switch Frontline:FabricDataSource to 'Stub'.");
        }

        _options = options;
        _logger  = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FabricCardDto?> GetMobileCardAsync(
        FabricLookupParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Code))
        {
            return null;
        }

        // Normalise on the client side too. The proc itself is case-aware
        // (the legacy WHERE clauses compare raw strings against the
        // configured collation), so passing already-uppercased input keeps
        // matches deterministic across machines / connections.
        var normalised = query.Code.Trim().ToUpperInvariant();

        var totalSw = Stopwatch.StartNew();

        var openSw = Stopwatch.StartNew();
        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        openSw.Stop();

        await using var cmd = new SqlCommand(GetMobileCardProcName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        };
        cmd.Parameters.Add(new SqlParameter("@Code",            SqlDbType.NVarChar, 50) { Value = normalised });
        cmd.Parameters.Add(new SqlParameter("@LowThreshold",    SqlDbType.Int)          { Value = query.LowThreshold });
        cmd.Parameters.Add(new SqlParameter("@EnoughThreshold", SqlDbType.Int)          { Value = query.EnoughThreshold });

        var execSw = Stopwatch.StartNew();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        execSw.Stop();

        var matSw = Stopwatch.StartNew();

        // RS1: main row. Zero rows here = unknown / blacklisted code; the
        // proc deliberately RETURNs early without emitting any of the three
        // result sets, which is fine — Read returns false and we bail.
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            matSw.Stop();
            totalSw.Stop();
            _logger.LogInformation(
                "[FrontlinePerf] sql.GetMobileCard code={Code} found=False openMs={OpenMs} execMs={ExecMs} totalMs={TotalMs}",
                normalised, openSw.ElapsedMilliseconds, execSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);
            return null;
        }

        var code     = ReadString(reader, "Code", normalised);
        var name     = ReadString(reader, "Name", string.Empty);
        var notes    = ReadStringOrNull(reader, "Notes");
        var photoUrl = ReadString(reader, "PhotoUrl", PlaceholderPhotoUrl);
        var discount = ReadIntOrNull(reader, "DiscountPercent");
        // mes_LastCheckedAt is on RS1 for parity with the proc shape and
        // future use (e.g. surfacing "checked 5 min ago" on the card), but
        // FabricCardDto does not currently expose it. Drained explicitly so
        // a future column-order shift in the proc still doesn't shift the
        // ordinals we actually consume.
        _ = ReadDateTimeOrNull(reader, "MesLastCheckedAt");

        // RS2: widths.
        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
        var widths = new List<WidthStockDto>(capacity: 4);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            widths.Add(new WidthStockDto(
                WidthMm:        ReadInt(reader, "WidthMm"),
                Status:         ReadStatus(reader, "Status"),
                StockMeters:    ReadIntOrNull(reader, "StockMeters"),
                ExpectedDate:   ReadDateOnlyOrNull(reader, "ExpectedDate"),
                IncomingMeters: ReadIntOrNull(reader, "IncomingMeters"),
                IncomingDate:   ReadDateOnlyOrNull(reader, "IncomingDate")));
        }

        // RS3: alternatives.
        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
        var alternatives = new List<FabricAlternativeDto>(capacity: 8);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            alternatives.Add(new FabricAlternativeDto(
                Code:         ReadString(reader, "Code", string.Empty),
                PhotoUrl:     ReadString(reader, "PhotoUrl", PlaceholderPhotoUrl),
                WidthMm:      ReadInt(reader, "WidthMm"),
                Status:       ReadStatus(reader, "Status"),
                StockMeters:  ReadIntOrNull(reader, "StockMeters"),
                ExpectedDate: ReadDateOnlyOrNull(reader, "ExpectedDate")));
        }

        matSw.Stop();
        totalSw.Stop();

        // Selected width: explicit ?width= wins when present in the returned
        // set; otherwise smallest available. Keeps parity with the F-1 stub
        // and the legacy mobile controller so swapping the data source
        // doesn't visibly shift which width chip is highlighted by default.
        int? selectedWidth = null;
        if (widths.Count > 0)
        {
            selectedWidth = query.Width is { } requested && widths.Any(w => w.WidthMm == requested)
                ? requested
                : widths.OrderBy(w => w.WidthMm).First().WidthMm;
        }

        _logger.LogInformation(
            "[FrontlinePerf] sql.GetMobileCard code={Code} found=True widths={Widths} alts={Alts} " +
            "openMs={OpenMs} execMs={ExecMs} materialiseMs={MatMs} totalMs={TotalMs}",
            normalised, widths.Count, alternatives.Count,
            openSw.ElapsedMilliseconds, execSw.ElapsedMilliseconds,
            matSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

        return new FabricCardDto(
            Code:            code,
            Name:            name,
            PhotoUrl:        photoUrl,
            Notes:           notes,
            DiscountPercent: discount,
            Widths:          widths,
            SelectedWidthMm: selectedWidth,
            Alternatives:    alternatives);
    }

    public Task<PagedResult<FabricLowStockDto>> GetLowStockListAsync(
        FabricLowStockQueryParams query,
        CancellationToken cancellationToken)
    {
        // F-2.3 will wire this to dbo.mes_Fabric_GetLowStockList. Until then
        // the SQL data source covers the lookup card only; anyone running
        // Frontline:FabricDataSource=Sql who needs the low-stock list must
        // either complete F-2.3 or switch back to Stub.
        throw new NotImplementedException(
            "SqlFabricQueryService.GetLowStockListAsync is reserved for F-2.3 " +
            "(dbo.mes_Fabric_GetLowStockList over dbo.V_Remains + TBD_Components.mes_*). " +
            "Set Frontline:FabricDataSource=Stub if you need the low-stock list to render today.");
    }

    // ---------------------------------------------------------------------
    // Defensive readers — same pattern as SqlOrdersQueryService. Tolerate
    // DBNull, missing columns, and unexpected types so a single bad row
    // never blanks the entire card.
    // ---------------------------------------------------------------------

    private static FabricAvailabilityStatus ReadStatus(SqlDataReader reader, string columnName)
    {
        var raw = ReadInt(reader, columnName);
        return raw is < 0 or > 4 ? FabricAvailabilityStatus.Unknown : (FabricAvailabilityStatus)raw;
    }

    private static int ReadInt(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return 0;
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            int i  => i,
            long l => l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l,
            _      => int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture),
                                   NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
        };
    }

    private static int? ReadIntOrNull(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return null;
        return ReadInt(reader, columnName);
    }

    private static string ReadString(SqlDataReader reader, string columnName, string fallback)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return fallback;
        return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? fallback;
    }

    private static string? ReadStringOrNull(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return null;
        return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateOnly? ReadDateOnlyOrNull(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return null;
        var dt = Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        return DateOnly.FromDateTime(dt);
    }

    private static DateTime? ReadDateTimeOrNull(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return null;
        return Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int TryGetOrdinal(SqlDataReader reader, string columnName)
    {
        try { return reader.GetOrdinal(columnName); }
        catch (IndexOutOfRangeException) { return -1; }
    }
}
