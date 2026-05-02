using System.Data;
using System.Globalization;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// SQL Server adapter for <see cref="IOrdersQueryService"/> that calls the
/// legacy <c>dbo.weblb_*</c> stored procedures (S-2). Mirrors the behaviour of
/// <c>LKvitai.Web.Controllers.OrdersController</c> in the legacy ASP.NET app:
/// <list type="bullet">
///   <item><c>dbo.weblb_Orders</c> — full table read, no parameters (used for
///   both the orders list and as the seed for the details summary).</item>
///   <item><c>dbo.weblb_Order</c> with <c>@OrderId</c> — order header + amounts.</item>
///   <item><c>dbo.weblb_Accessories</c> with <c>@OrderId</c> — accessory rows.</item>
///   <item><c>dbo.weblb_Items</c> with <c>@OrderId</c> — item rows (each becomes
///   one <see cref="OrderItemGroupDto"/>; matching accessories are appended).</item>
///   <item><c>dbo.weblb_Employees</c> with <c>@OrderId</c> — employee assignments.</item>
/// </list>
/// All column reads are <see cref="DBNull"/>-guarded; missing or unexpected
/// types degrade to safe defaults (<c>0m</c>, empty string, <c>null</c>) instead
/// of throwing, so a single bad row never blanks the entire list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Paging / filtering note (TODO).</b> <c>dbo.weblb_Orders</c> takes no
/// parameters and returns the full result set. This adapter therefore
/// materialises every row into memory, then applies search / status / store /
/// has-debt filters and pages in C#. That matches the legacy app's behaviour and
/// is acceptable while the table size is in the low thousands, but a long-term
/// fix is to introduce a paged proc wrapper (e.g. <c>dbo.weblb_Orders_Paged</c>
/// taking <c>@Skip</c>, <c>@Take</c>, <c>@Search</c>, …) so the database does
/// the work and we stop shipping the entire table per request.
/// </para>
/// <para>
/// <b>Date filter.</b> <see cref="OrdersQueryParams.Date"/> is intentionally
/// ignored here — the legacy proc has no date range parameters and the WebUI
/// keeps that filter disabled until a paged proc wrapper exists. A future
/// revision should plumb a real range through both the proc and this adapter.
/// </para>
/// <para>
/// <b>Customer flags.</b> The legacy proc does not return <c>IsVip</c>,
/// <c>HasNote</c> or <c>IsOverdue</c>. We derive <c>HasDebt = Debt &gt; 0</c>
/// and leave the other three at <c>false</c>. Unknown column ordinals 8 and 9
/// of <c>weblb_Orders</c> may carry these — to be confirmed with the DBA.
/// </para>
/// </remarks>
public sealed class SqlOrdersQueryService : IOrdersQueryService
{
    private const string OrdersProcName      = "dbo.weblb_Orders";
    private const string OrderProcName       = "dbo.weblb_Order";
    private const string AccessoriesProcName = "dbo.weblb_Accessories";
    private const string ItemsProcName       = "dbo.weblb_Items";
    private const string EmployeesProcName   = "dbo.weblb_Employees";
    private const string OrderIdParameter    = "@OrderId";

    private readonly SalesSqlOptions _options;
    private readonly ILogger<SqlOrdersQueryService> _logger;

    public SqlOrdersQueryService(SalesSqlOptions options, ILogger<SqlOrdersQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "SqlOrdersQueryService requires a non-empty SalesSqlOptions.ConnectionString. " +
                "Configure ConnectionStrings:LKvitaiDb or switch Sales:OrdersDataSource to 'Stub'.");
        }

        _options = options;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(
        OrdersQueryParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await ReadAllOrdersAsync(cancellationToken).ConfigureAwait(false);
        return ApplyFilterSortPage(all, query);
    }

    public async Task<OrdersFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        // Same full-table read as GetOrdersAsync (weblb_Orders has no parameters).
        // We project the two label sets the WebUI toolbar needs; the data is
        // small enough that an ad-hoc DISTINCT in C# is cheaper than another
        // round trip. When the paged proc wrapper lands (TODO above), expose
        // dedicated dbo.weblb_StatusList / dbo.weblb_StoreList and wire them
        // here instead.
        var all = await ReadAllOrdersAsync(cancellationToken).ConfigureAwait(false);

        var statuses = all
            .Select(o => o.Status)
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static s => s, StringComparer.CurrentCulture)
            .ToList();

        var stores = all
            .Select(o => o.Store)
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static s => s, StringComparer.CurrentCulture)
            .ToList();

        return new OrdersFilterOptionsDto(statuses, stores);
    }

    public async Task<OrderDetailsDto?> GetOrderDetailsAsync(
        string number,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        // Phase 1: locate the summary row in weblb_Orders so we can resolve the
        // legacy Id (the @OrderId parameter all other procs require) and seed
        // the price / debt / store / status fields the details proc does not
        // return.
        var summary = (await ReadAllOrdersAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(o => string.Equals(o.Number, number, StringComparison.OrdinalIgnoreCase));

        if (summary is null)
        {
            return null;
        }

        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Phase 2: header (operator + amounts + authoritative address override).
        OrderOperatorDto? @operator = null;
        var amounts = new List<OrderAmountDto>(6);
        var headerCustomer = summary.Customer;
        var headerStatus   = summary.Status;
        var headerAddress  = summary.Address;

        await using (var cmd = CreateProc(conn, OrderProcName, summary.Id))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                headerCustomer = ReadString(reader, "Customer", summary.Customer);
                headerStatus   = ReadString(reader, "Status",   summary.Status);
                headerAddress  = ReadString(reader, "Address",  summary.Address);

                var operatorName = ReadString(reader, "sUserNameInsert", string.Empty);
                var operatorAt   = ReadDateTime(reader, "Date");
                if (!string.IsNullOrWhiteSpace(operatorName) && operatorAt is not null)
                {
                    @operator = new OrderOperatorDto(operatorName, operatorAt.Value);
                }

                amounts.Add(new OrderAmountDto(OrderAmountKind.Defined,       "Defined",        ReadDecimal(reader, "DefinedAmount"),    Percent: null));
                amounts.Add(new OrderAmountDto(OrderAmountKind.Calculated,    "Calculated",     ReadDecimal(reader, "CalculatedAmount"), Percent: null));
                amounts.Add(new OrderAmountDto(OrderAmountKind.Discount,      "Discount",       Amount: null,                            ReadDecimal(reader, "Discount")));
                amounts.Add(new OrderAmountDto(OrderAmountKind.AfterDiscount, "After discount", ReadDecimal(reader, "FinalCost"),        Percent: null));
                amounts.Add(new OrderAmountDto(OrderAmountKind.Paid,          "Paid",           ReadDecimal(reader, "Paid"),             Percent: null));
                amounts.Add(new OrderAmountDto(OrderAmountKind.Debt,          "Debt",           ReadDecimal(reader, "Debt"),             Percent: null));
            }
        }

        // Phase 3: accessories (collected first so we can attach them to their parent items).
        var accessoriesByItemId = new Dictionary<long, List<OrderItemDto>>();
        await using (var cmd = CreateProc(conn, AccessoriesProcName, summary.Id))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var itemId = ReadLongFromMaybeString(reader, "ItemId");
                var qty    = ReadDecimal(reader, "Quantity");
                var price  = ReadDecimal(reader, "Price");
                var amount = ReadDecimal(reader, "Amount");
                var title  = ReadString(reader, "Title", string.Empty);

                var accessoryRow = new OrderItemDto(
                    Num:         string.Empty,
                    Name:        title,
                    Side:        string.Empty,
                    Color:       string.Empty,
                    Width:       null,
                    Height:      null,
                    Notes:       string.Empty,
                    Qty:         qty,
                    Price:       price,
                    Amount:      amount,
                    IsAccessory: true);

                if (!accessoriesByItemId.TryGetValue(itemId, out var bucket))
                {
                    bucket = new List<OrderItemDto>();
                    accessoriesByItemId[itemId] = bucket;
                }
                bucket.Add(accessoryRow);
            }
        }

        // Phase 4: items — each becomes one OrderItemGroupDto seeded with its parent line plus accessories.
        var itemGroups = new List<OrderItemGroupDto>();
        await using (var cmd = CreateProc(conn, ItemsProcName, summary.Id))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            var displayOrder = 1;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var itemId = ReadLongFromMaybeString(reader, "Id");
                var name   = ReadString(reader, "Title",  string.Empty);
                var side   = ReadString(reader, "Side",   string.Empty);
                var color  = ReadString(reader, "Color",  string.Empty);
                var notes  = ReadString(reader, "Notes",  string.Empty);
                var width  = ReadDecimal(reader, "Width");
                var height = ReadDecimal(reader, "Height");
                var qty    = ReadDecimal(reader, "Quantity");
                var price  = ReadDecimal(reader, "Price");
                var amount = ReadDecimal(reader, "Amount");

                var num = displayOrder.ToString(CultureInfo.InvariantCulture);

                var parent = new OrderItemDto(
                    Num:         num,
                    Name:        name,
                    Side:        side,
                    Color:       color,
                    Width:       width == 0m ? null : width,
                    Height:      height == 0m ? null : height,
                    Notes:       notes,
                    Qty:         qty,
                    Price:       price,
                    Amount:      amount,
                    IsAccessory: false);

                var lines = new List<OrderItemDto>(capacity: 1) { parent };
                if (accessoriesByItemId.TryGetValue(itemId, out var accessoryRows))
                {
                    lines.AddRange(accessoryRows);
                }

                itemGroups.Add(new OrderItemGroupDto(
                    Label: $"{num}. {name}",
                    Lines: lines));

                displayOrder++;
            }
        }

        // Phase 5: employees.
        var employees = new List<OrderEmployeeDto>();
        await using (var cmd = CreateProc(conn, EmployeesProcName, summary.Id))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var fullName = ReadString(reader, "EmployeeFullName", string.Empty);
                var dutyText = ReadString(reader, "Duty",             string.Empty);

                employees.Add(new OrderEmployeeDto(
                    Name:             fullName,
                    Initials:         BuildInitials(fullName),
                    DutyCode:         BuildDutyCode(dutyText),
                    DutyLabel:        dutyText,
                    ServiceDate:      ParseLegacyDate(ReadString(reader, "ServiceDate",    string.Empty)),
                    AcquaintanceDate: ParseLegacyDate(ReadString(reader, "AquiranceDate",  string.Empty)),
                    OrderQty:         ReadIntFromMaybeString(reader, "AquiredOrderQuanity"),
                    ItemQty:          ReadIntFromMaybeString(reader, "AquiredBlindsQuanity"),
                    Amount:           ReadDecimal(reader, "AquiredAmount")));
            }
        }

        return new OrderDetailsDto(
            Id:         summary.Id,
            Number:     summary.Number,
            Date:       summary.Date,
            Price:      summary.Price,
            Debt:       summary.Debt,
            IsOverdue:  summary.IsOverdue,
            Customer:   headerCustomer,
            HasDebt:    summary.HasDebt,
            IsVip:      summary.IsVip,
            HasNote:    summary.HasNote,
            Status:     headerStatus,
            StatusCode: SalesOrderStatusMap.ToStatusCode(headerStatus),
            Store:      summary.Store,
            Address:    headerAddress,
            Operator:   @operator,
            ItemGroups: itemGroups,
            Amounts:    amounts,
            Employees:  employees);
    }

    /// <summary>
    /// Reads <c>dbo.weblb_Orders</c> in full and projects each row to an
    /// <see cref="OrderSummaryDto"/>. Column reads are by ordinal because the
    /// legacy proc has no documented column-name contract — the indexes match
    /// the legacy <c>OrdersController</c>'s <c>GetX(n)</c> calls one-for-one.
    /// </summary>
    private async Task<List<OrderSummaryDto>> ReadAllOrdersAsync(CancellationToken cancellationToken)
    {
        var rows = new List<OrderSummaryDto>(capacity: 256);

        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new SqlCommand(OrdersProcName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        };

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var fieldCount = reader.FieldCount;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id             = ReadInt64ByOrdinal(reader, ordinal: 0);
            var number         = ReadStringByOrdinal(reader, ordinal: 1, fallback: string.Empty);
            var date           = ReadDateOnlyByOrdinal(reader, ordinal: 2);
            var price          = ReadDecimalByOrdinal(reader, ordinal: 3);
            var debt           = ReadDecimalByOrdinal(reader, ordinal: 4);
            var customer       = ReadStringByOrdinal(reader, ordinal: 5, fallback: string.Empty);
            var status         = ReadStringByOrdinal(reader, ordinal: 6, fallback: string.Empty);
            var store          = ReadStringByOrdinal(reader, ordinal: 7, fallback: string.Empty);
            // ordinals 8 and 9 are unused by the legacy controller — possibly
            // IsVip / HasNote / DueDate. To be confirmed with the DBA.
            var address        = fieldCount > 10
                ? ReadStringByOrdinal(reader, ordinal: 10, fallback: string.Empty)
                : string.Empty;
            var productsSearch = fieldCount > 11
                ? ReadStringOrNullByOrdinal(reader, ordinal: 11)
                : null;

            rows.Add(new OrderSummaryDto(
                Id:             id,
                Number:         number,
                Date:           date,
                Price:          price,
                Debt:           debt,
                IsOverdue:      false,
                Customer:       customer,
                HasDebt:        debt > 0m,
                IsVip:          false,
                HasNote:        false,
                Status:         status,
                StatusCode:     SalesOrderStatusMap.ToStatusCode(status),
                Store:          store,
                Address:        address,
                ProductsSearch: productsSearch));
        }

        return rows;
    }

    /// <summary>
    /// Applies the in-memory equivalent of the legacy WHERE / ORDER BY / TOP
    /// clauses to the materialised order list. Mirrors the contract documented
    /// on <see cref="OrdersQueryParams"/> and lets the WebUI render real paging
    /// metadata (Total / Page / PageSize) without an extra count round-trip.
    /// </summary>
    private static PagedResult<OrderSummaryDto> ApplyFilterSortPage(
        IReadOnlyList<OrderSummaryDto> all,
        OrdersQueryParams query)
    {
        IEnumerable<OrderSummaryDto> filtered = all;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim();
            filtered = filtered.Where(o =>
                o.Number.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                o.Customer.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                o.Address.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                (o.ProductsSearch is { Length: > 0 } &&
                    o.ProductsSearch.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        // Trim both sides — legacy nchar/char columns are right-padded with
        // spaces, which would otherwise make an exact-equality dropdown filter
        // silently return zero rows.
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            filtered = filtered.Where(o =>
                string.Equals(o.Status?.Trim(), status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Store))
        {
            var store = query.Store.Trim();
            filtered = filtered.Where(o =>
                string.Equals(o.Store?.Trim(), store, StringComparison.OrdinalIgnoreCase));
        }

        if (query.HasDebt)
        {
            filtered = filtered.Where(o => o.HasDebt);
        }

        // S-2 ignores the Date preset on purpose — the legacy proc has no date
        // range parameter and a real range arrives once a paged proc wrapper
        // exists. Until then the WebUI keeps that selector disabled.

        var sorted = filtered
            .OrderByDescending(o => o.Date)
            .ThenByDescending(o => o.Number, StringComparer.Ordinal)
            .ToList();

        var pageSize = query.PageSize <= 0 ? 100 : query.PageSize;
        var page     = query.Page     <= 0 ? 1   : query.Page;

        var paged = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<OrderSummaryDto>(paged, sorted.Count, page, pageSize);
    }

    private SqlCommand CreateProc(SqlConnection conn, string procName, long orderId)
    {
        var cmd = new SqlCommand(procName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        };
        cmd.Parameters.Add(new SqlParameter(OrderIdParameter, SqlDbType.BigInt) { Value = orderId });
        return cmd;
    }

    // ---------------------------------------------------------------------
    // Defensive readers — every accessor below tolerates DBNull, missing
    // columns and unexpected types so a single broken row never blanks the
    // entire orders list.
    // ---------------------------------------------------------------------

    private static long ReadInt64ByOrdinal(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static string ReadStringByOrdinal(SqlDataReader reader, int ordinal, string fallback)
        => reader.IsDBNull(ordinal) ? fallback : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? fallback;

    private static string? ReadStringOrNullByOrdinal(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static decimal ReadDecimalByOrdinal(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static DateOnly ReadDateOnlyByOrdinal(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return default;
        var dt = Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        return DateOnly.FromDateTime(dt);
    }

    private static string ReadString(SqlDataReader reader, string columnName, string fallback)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return fallback;
        return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? fallback;
    }

    private static decimal ReadDecimal(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return 0m;
        return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return null;
        return Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static long ReadLongFromMaybeString(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return 0L;
        var raw = reader.GetValue(ordinal);
        if (raw is long l) return l;
        if (raw is int i)  return i;
        return long.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0L;
    }

    private static int ReadIntFromMaybeString(SqlDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return 0;
        var raw = reader.GetValue(ordinal);
        if (raw is int i)   return i;
        if (raw is long l)  return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l;
        return int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static int TryGetOrdinal(SqlDataReader reader, string columnName)
    {
        try { return reader.GetOrdinal(columnName); }
        catch (IndexOutOfRangeException) { return -1; }
    }

    private static string BuildInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return string.Empty;
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
        return string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant();
    }

    /// <summary>
    /// Reduces the Lithuanian duty title to a stable lowercase code the WebUI
    /// uses to pick the <c>duty--*</c> dot color (<c>sales</c>, <c>prod</c>,
    /// <c>inst</c>). Conservative: first non-empty word lowercased, then mapped
    /// onto the three known buckets — anything else falls through as-is and
    /// renders as a neutral dot.
    /// </summary>
    private static string BuildDutyCode(string dutyLabel)
    {
        if (string.IsNullOrWhiteSpace(dutyLabel)) return string.Empty;
        var lower = dutyLabel.Trim().ToLowerInvariant();

        if (lower.Contains("pardav") || lower.Contains("sales"))                 return "sales";
        if (lower.Contains("gamyb")  || lower.Contains("prod"))                  return "prod";
        if (lower.Contains("monta")  || lower.Contains("install") || lower.Contains("inst")) return "inst";

        var firstWord = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstWord ?? string.Empty;
    }

    private static DateOnly? ParseLegacyDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }
}
