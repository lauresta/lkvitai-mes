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
/// <c>weblb_Fabric_GetMobileCard</c>); the desktop low-stock list calls
/// <c>dbo.mes_Fabric_GetLowStockList</c> (F-2.3 — server-side filter,
/// sort and paging over <c>dbo.V_Remains</c> + <c>dbo.TBD_Components</c>).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>dbo.mes_Fabric_GetMobileCard</c> — returns 3 result sets (main,
///   widths, alternatives). Unknown / blacklisted code → zero result sets;
///   the adapter treats "no rows in RS1" as a 404 to keep the proc cheap on
///   the unhappy path.</item>
///   <item><c>dbo.mes_Fabric_GetLowStockList</c> — single paged result set
///   plus a windowed <c>TotalRows</c> column on every row (same shape as
///   <c>dbo.weblb_Orders_Paged</c>). The adapter trims and uppercases the
///   filter inputs before passing them through; nullable filters travel as
///   <see cref="DBNull"/> so the SP's <c>IS NULL</c> branches fire instead
///   of looking for the literal <c>N''</c>.</item>
///   <item>Status integers map 1:1 to <see cref="FabricAvailabilityStatus"/>
///   (1 Enough, 2 Low, 3 None, 4 Discontinued, 0 Unknown).</item>
///   <item>Selected width on the lookup card is picked client-side:
///   explicit <c>?width=</c> wins when present in the returned set;
///   otherwise the smallest available width — same rule as the in-memory
///   stub and the legacy <c>FabricAvailabilityController.Mobile</c>.</item>
///   <item><c>AlternativeCodes</c> on the low-stock list comes back from
///   the SP as a raw CSV string (same format as <c>fa_Alternatives</c>).
///   The adapter splits and normalises it client-side: tokens are trimmed,
///   uppercased, deduplicated, and the legacy <c>NRxxx → Rxxx</c> rewrite
///   is applied so the WebUI never sees a mixed bag.</item>
/// </list>
/// All column reads are <see cref="DBNull"/>-guarded; missing columns or
/// unexpected types degrade to safe defaults instead of throwing, so a
/// single bad row never blanks the whole card / page.
/// </remarks>
public sealed class SqlFabricQueryService : IFabricQueryService
{
    private const string GetMobileCardProcName  = "dbo.mes_Fabric_GetMobileCard";
    private const string GetLowStockProcName    = "dbo.mes_Fabric_GetLowStockList";
    private const string PlaceholderPhotoUrl    = "/img/fabric_pl.png";

    /// <summary>Soft cap on per-page rows; mirrors the SP's own clamp so
    /// the wire never sees a request the SP would silently shrink. The SP
    /// caps at 1000 because the prod catalogue is ~757 fabrics, so the
    /// WebUI low-stock view can fetch the entire filtered result set in a
    /// single round-trip until #100 wires real MudDataGrid windowed paging.</summary>
    private const int MaxPageSize = 1000;

    /// <summary>Deduplication-friendly empty list, returned for fabrics
    /// whose <c>fa_Alternatives</c> column is NULL or whitespace.</summary>
    private static readonly IReadOnlyList<string> EmptyAlternativeList = Array.Empty<string>();

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

    public async Task<PagedResult<FabricLowStockDto>> GetLowStockListAsync(
        FabricLowStockQueryParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page     = query.Page     < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : (query.PageSize > MaxPageSize ? MaxPageSize : query.PageSize);

        var totalSw = Stopwatch.StartNew();

        var openSw = Stopwatch.StartNew();
        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        openSw.Stop();

        await using var cmd = new SqlCommand(GetLowStockProcName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        };
        cmd.Parameters.Add(new SqlParameter("@Search",          SqlDbType.NVarChar, 100) { Value = NullableNVarChar(query.Search) });
        cmd.Parameters.Add(new SqlParameter("@ThresholdMeters", SqlDbType.Int)           { Value = (object?)query.ThresholdMeters ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Status",          SqlDbType.NVarChar, 20)  { Value = NullableNVarChar(query.Status) });
        cmd.Parameters.Add(new SqlParameter("@WidthMm",         SqlDbType.Int)           { Value = (object?)query.WidthMm ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Supplier",        SqlDbType.NVarChar, 100) { Value = NullableNVarChar(query.Supplier) });
        cmd.Parameters.Add(new SqlParameter("@Page",            SqlDbType.Int)           { Value = page });
        cmd.Parameters.Add(new SqlParameter("@PageSize",        SqlDbType.Int)           { Value = pageSize });

        var execSw = Stopwatch.StartNew();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        execSw.Stop();

        var matSw = Stopwatch.StartNew();
        var rows = new List<FabricLowStockDto>(capacity: pageSize);
        var totalRows = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new FabricLowStockDto(
                Code:             ReadString(reader, "Code", string.Empty),
                Name:             ReadString(reader, "Name", string.Empty),
                PhotoUrl:         ReadString(reader, "PhotoUrl", PlaceholderPhotoUrl),
                WidthMm:          ReadInt(reader, "WidthMm"),
                AvailableMeters:  ReadInt(reader, "AvailableMeters"),
                ThresholdMeters:  ReadInt(reader, "ThresholdMeters"),
                Status:           ReadStatus(reader, "Status"),
                ExpectedDate:     ReadDateOnlyOrNull(reader, "ExpectedDate"),
                IncomingMeters:   ReadIntOrNull(reader, "IncomingMeters"),
                Supplier:         ReadStringOrNull(reader, "Supplier"),
                AlternativeCodes: SplitAlternatives(ReadStringOrNull(reader, "AlternativeCodes"), excludeCode: ReadString(reader, "Code", string.Empty)),
                LastChecked:      ReadDateTimeOffsetOrNull(reader, "LastChecked"),
                CanReserve:       ReadBool(reader, "CanReserve"),
                CanNotify:        ReadBool(reader, "CanNotify"),
                CanReplace:       ReadBool(reader, "CanReplace")));

            totalRows = ReadInt(reader, "TotalRows");  // every row carries the same windowed count
        }
        matSw.Stop();
        totalSw.Stop();

        _logger.LogInformation(
            "[FrontlinePerf] sql.GetLowStockList returned={Returned} total={Total} page={Page} pageSize={PageSize} " +
            "search={HasSearch} threshold={Threshold} status={HasStatus} width={HasWidth} supplier={HasSupplier} " +
            "openMs={OpenMs} execMs={ExecMs} materialiseMs={MatMs} totalMs={TotalMs}",
            rows.Count, totalRows, page, pageSize,
            !string.IsNullOrWhiteSpace(query.Search), query.ThresholdMeters,
            !string.IsNullOrWhiteSpace(query.Status), query.WidthMm.HasValue,
            !string.IsNullOrWhiteSpace(query.Supplier),
            openSw.ElapsedMilliseconds, execSw.ElapsedMilliseconds,
            matSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

        return new PagedResult<FabricLowStockDto>(rows, totalRows, page, pageSize);
    }

    /// <summary>
    /// Splits a CSV-ish alternatives string into a normalised list. Operators
    /// have used commas, semicolons, slashes, pipes, spaces, and tabs as
    /// separators over the years — mirror the legacy proc's tolerance so
    /// the same input that worked on <c>weblb_Fabric_GetMobileCard</c>
    /// keeps working here. Tokens are trimmed, uppercased, and the legacy
    /// <c>NRxxx → Rxxx</c> rewrite is applied. The fabric's own code is
    /// removed from the result so a self-reference never appears as an
    /// alternative chip.
    /// </summary>
    private static IReadOnlyList<string> SplitAlternatives(string? raw, string excludeCode)
    {
        if (string.IsNullOrWhiteSpace(raw)) return EmptyAlternativeList;

        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(capacity: 4);
        var exclude = (excludeCode ?? string.Empty).Trim().ToUpperInvariant();

        foreach (var token in raw.Split(_alternativeSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim().ToUpperInvariant();
            if (t.Length == 0) continue;

            // Strip residual punctuation that has shown up in operator entries.
            t = t.Replace(".", string.Empty, StringComparison.Ordinal)
                 .Replace(":", string.Empty, StringComparison.Ordinal)
                 .Replace("\\", string.Empty, StringComparison.Ordinal);
            if (t.Length == 0) continue;

            // Legacy "NRxxx" prefix → "Rxxx".
            if (t.Length > 2 && t[0] == 'N' && t[1] == 'R')
            {
                t = "R" + t.Substring(2);
            }

            if (string.Equals(t, exclude, StringComparison.Ordinal)) continue;
            if (seen.Add(t)) result.Add(t);
        }

        return result.Count == 0 ? EmptyAlternativeList : result;
    }

    private static readonly char[] _alternativeSeparators =
        new[] { ',', ';', ' ', '\t', '\n', '\r', '|', '/' };

    private static object NullableNVarChar(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

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

    /// <summary>
    /// Reads a SQL <c>datetime2</c> column as a UTC <see cref="DateTimeOffset"/>.
    /// The proc stamps <c>mes_LastCheckedAt</c> via <c>sysutcdatetime()</c>,
    /// so the value is already in UTC — we just attach the offset so callers
    /// can display "checked 5 min ago" without re-deriving the timezone.
    /// </summary>
    private static DateTimeOffset? ReadDateTimeOffsetOrNull(SqlDataReader reader, string columnName)
    {
        var dt = ReadDateTimeOrNull(reader, columnName);
        if (dt is null) return null;
        return new DateTimeOffset(DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc));
    }

    /// <summary>
    /// Reads a SQL <c>bit</c> column as <see cref="bool"/>. Tolerates wider
    /// numeric representations (some legacy procs project <c>tinyint</c>);
    /// non-zero ⇒ <c>true</c>.
    /// </summary>
    private static bool ReadBool(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return false;
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            bool b   => b,
            byte by  => by != 0,
            short s  => s != 0,
            int i    => i != 0,
            long l   => l != 0,
            _        => bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed) && parsed,
        };
    }

    private static int TryGetOrdinal(SqlDataReader reader, string columnName)
    {
        try { return reader.GetOrdinal(columnName); }
        catch (IndexOutOfRangeException) { return -1; }
    }
}
