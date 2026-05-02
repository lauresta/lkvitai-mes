using System.Data;
using System.Diagnostics;
using System.Globalization;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// SQL Server adapter for <see cref="IOrdersQueryService"/> over the legacy
/// <c>LKvitaiDb</c> database. Calls dedicated server-side stored procedures
/// for every read; nothing is cached or reshaped in C#.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>dbo.weblb_Orders_Paged</c> — page + filter + sort the orders
///   list. Returns only the requested page plus a windowed <c>TotalRows</c>
///   value, so cost scales with page size instead of table size.</item>
///   <item><c>dbo.weblb_Orders_Filters</c> — distinct status / store labels
///   from the reference (<c>Zinynas_*</c>) tables, two result sets in one
///   round-trip.</item>
///   <item><c>dbo.weblb_Orders_LookupByNumber</c> — resolves a human-readable
///   order number to the legacy <c>UzsakymasID</c> the details procs need.</item>
///   <item><c>dbo.weblb_Order</c>, <c>dbo.weblb_Accessories</c>,
///   <c>dbo.weblb_Items</c>, <c>dbo.weblb_Employees</c> — per-order details,
///   each takes <c>@OrderId BIGINT</c> and is unchanged from the legacy app.</item>
/// </list>
/// All column reads are <see cref="DBNull"/>-guarded; missing or unexpected
/// types degrade to safe defaults (<c>0m</c>, empty string, <c>null</c>) instead
/// of throwing, so a single bad row never blanks the entire list.
/// </remarks>
public sealed class SqlOrdersQueryService : IOrdersQueryService
{
    private const string OrdersPagedProcName    = "dbo.weblb_Orders_Paged";
    private const string OrdersFiltersProcName  = "dbo.weblb_Orders_Filters";
    private const string OrdersLookupProcName   = "dbo.weblb_Orders_LookupByNumber";
    private const string OrderProcName          = "dbo.weblb_Order";
    private const string AccessoriesProcName    = "dbo.weblb_Accessories";
    private const string ItemsProcName          = "dbo.weblb_Items";
    private const string EmployeesProcName      = "dbo.weblb_Employees";
    private const string OrderIdParameter       = "@OrderId";

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

        var pageSize = query.PageSize <= 0 ? 100 : query.PageSize;
        var page     = query.Page     <= 0 ? 1   : query.Page;

        var totalSw = Stopwatch.StartNew();

        var openSw = Stopwatch.StartNew();
        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        openSw.Stop();

        await using var cmd = new SqlCommand(OrdersPagedProcName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        };

        // The SP normalises NULL / empty parameters internally (LTRIM/RTRIM
        // and IS NULL guards), so we can pass DBNull straight through for the
        // "no filter" case without pre-trimming on the client.
        cmd.Parameters.Add(new SqlParameter("@Page",     SqlDbType.Int)            { Value = page });
        cmd.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int)            { Value = pageSize });
        cmd.Parameters.Add(new SqlParameter("@Search",   SqlDbType.NVarChar, 200)  { Value = NullableNVarChar(query.Search) });
        cmd.Parameters.Add(new SqlParameter("@Status",   SqlDbType.NVarChar, 100)  { Value = NullableNVarChar(query.Status) });
        cmd.Parameters.Add(new SqlParameter("@Store",    SqlDbType.NVarChar, 100)  { Value = NullableNVarChar(query.Store) });
        cmd.Parameters.Add(new SqlParameter("@HasDebt",  SqlDbType.Bit)            { Value = query.HasDebt ? (object)true : DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@DateFrom", SqlDbType.Date)           { Value = DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@DateTo",   SqlDbType.Date)           { Value = DBNull.Value });

        var execSw = Stopwatch.StartNew();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        execSw.Stop();

        // Column ordinals for dbo.weblb_Orders_Paged (kept in sync with
        // .scratch/sp-paged.sql). Any drift would silently mis-project rows,
        // so when the SP changes shape both sides must be updated together.
        // Order: Id, Number, Date, Price, Debt, Customer, Status, Store,
        //        IsCancelled, sImageKey, Address, ProductsSearch, TotalRows
        var rows = new List<OrderSummaryDto>(capacity: pageSize);
        var totalRows = 0;

        var matSw = Stopwatch.StartNew();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadOrderSummary(reader));
            // TotalRows is windowed COUNT(*) OVER () — same value on every row;
            // we just keep the last one read (or zero if the page is empty).
            totalRows = (int)ReadInt64ByOrdinal(reader, ordinal: 12);
        }
        matSw.Stop();
        totalSw.Stop();

        _logger.LogInformation(
            "[SalesPerf] sql.GetOrders returned={Returned} total={Total} page={Page} pageSize={PageSize} " +
            "search={HasSearch} status={HasStatus} store={HasStore} hasDebt={HasDebt} " +
            "openMs={OpenMs} execMs={ExecMs} materialiseMs={MaterialiseMs} totalMs={TotalMs}",
            rows.Count, totalRows, page, pageSize,
            !string.IsNullOrWhiteSpace(query.Search), !string.IsNullOrWhiteSpace(query.Status),
            !string.IsNullOrWhiteSpace(query.Store), query.HasDebt,
            openSw.ElapsedMilliseconds, execSw.ElapsedMilliseconds,
            matSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

        return new PagedResult<OrderSummaryDto>(rows, totalRows, page, pageSize);
    }

    public async Task<OrdersFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        // dbo.weblb_Orders_Filters returns two result sets:
        //   1) statuses : single column [Status] from dbo.Zinynas_busenos
        //   2) stores   : single column [Store]  from dbo.Zinynas_vieta
        // Both already trimmed and ordered server-side — no projection here.
        var totalSw = Stopwatch.StartNew();

        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new SqlCommand(OrdersFiltersProcName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        };

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var statuses = new List<string>(capacity: 16);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            statuses.Add(reader.GetString(0));
        }

        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

        var stores = new List<string>(capacity: 32);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            stores.Add(reader.GetString(0));
        }

        totalSw.Stop();

        _logger.LogInformation(
            "[SalesPerf] sql.GetFilterOptions statuses={StatusCount} stores={StoreCount} totalMs={TotalMs}",
            statuses.Count, stores.Count, totalSw.ElapsedMilliseconds);

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

        var totalSw = Stopwatch.StartNew();

        var openSw = Stopwatch.StartNew();
        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        openSw.Stop();

        // Phase 1: resolve Number → UzsakymasID via the dedicated lookup SP.
        // The legacy details procs (weblb_Order/Items/Accessories/Employees)
        // all key on @OrderId, so we pay one cheap index seek up-front rather
        // than scanning the orders list.
        var lookupSw = Stopwatch.StartNew();
        long orderId;
        await using (var cmd = new SqlCommand(OrdersLookupProcName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        })
        {
            cmd.Parameters.Add(new SqlParameter("@Number", SqlDbType.NVarChar, 100) { Value = number });
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (result is null || result is DBNull)
            {
                lookupSw.Stop();
                _logger.LogInformation(
                    "[SalesPerf] sql.GetOrderDetails number={Number} found=False lookupMs={LookupMs} totalMs={TotalMs}",
                    number, lookupSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);
                return null;
            }
            orderId = Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        lookupSw.Stop();

        // Phase 2: the legacy weblb_Order proc returns the authoritative
        // header (operator + amounts + address override). The summary fields
        // we still need (number, date, price, debt, store, status) are
        // re-projected from a single-row dbo.weblb_Orders_Paged search call
        // so this method does not need its own copy of the projection logic.
        var summarySw = Stopwatch.StartNew();
        OrderSummaryDto? summary = await ReadSummaryByNumberAsync(conn, number, cancellationToken).ConfigureAwait(false);
        summarySw.Stop();

        if (summary is null)
        {
            // Shouldn't happen — the lookup just told us the order exists —
            // but guard anyway so we don't NRE on a stale read.
            _logger.LogWarning(
                "[SalesPerf] sql.GetOrderDetails number={Number} lookupOk=True summary=null lookupMs={LookupMs} summaryMs={SummaryMs} totalMs={TotalMs}",
                number, lookupSw.ElapsedMilliseconds, summarySw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);
            return null;
        }

        OrderOperatorDto? @operator = null;
        var amounts = new List<OrderAmountDto>(6);
        var headerCustomer = summary.Customer;
        var headerStatus   = summary.Status;
        var headerAddress  = summary.Address;

        var headerSw = Stopwatch.StartNew();
        await using (var cmd = CreateProc(conn, OrderProcName, orderId))
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
        headerSw.Stop();

        // Phase 3: accessories (collected first so we can attach them to their parent items).
        var accSw = Stopwatch.StartNew();
        var accessoriesByItemId = new Dictionary<long, List<OrderItemDto>>();
        await using (var cmd = CreateProc(conn, AccessoriesProcName, orderId))
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
        accSw.Stop();

        // Phase 4: items — each becomes one OrderItemGroupDto seeded with its parent line plus accessories.
        var itemsSw = Stopwatch.StartNew();
        var itemGroups = new List<OrderItemGroupDto>();
        await using (var cmd = CreateProc(conn, ItemsProcName, orderId))
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
        itemsSw.Stop();

        // Phase 5: employees.
        var empSw = Stopwatch.StartNew();
        var employees = new List<OrderEmployeeDto>();
        await using (var cmd = CreateProc(conn, EmployeesProcName, orderId))
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
        empSw.Stop();
        totalSw.Stop();

        _logger.LogInformation(
            "[SalesPerf] sql.GetOrderDetails number={Number} found=True items={ItemGroups} accessories={AccessoryItems} employees={EmployeeCount} " +
            "lookupMs={LookupMs} summaryMs={SummaryMs} openMs={OpenMs} headerMs={HeaderMs} accMs={AccMs} itemsMs={ItemsMs} empMs={EmpMs} totalMs={TotalMs}",
            number, itemGroups.Count, accessoriesByItemId.Sum(kv => kv.Value.Count), employees.Count,
            lookupSw.ElapsedMilliseconds, summarySw.ElapsedMilliseconds, openSw.ElapsedMilliseconds,
            headerSw.ElapsedMilliseconds, accSw.ElapsedMilliseconds, itemsSw.ElapsedMilliseconds,
            empSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

        return new OrderDetailsDto(
            Id:         orderId,
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
    /// Pulls a single-row summary from <c>dbo.weblb_Orders_Paged</c> by passing
    /// the human-readable order number through as the <c>@Search</c> filter
    /// (LIKE-search on Number / Customer / Address). Filtered to one row by
    /// <c>@PageSize = 1</c>, then post-filtered to the exact match in C# so a
    /// substring collision (e.g. another order whose Customer text contains
    /// our number) cannot win.
    /// </summary>
    private async Task<OrderSummaryDto?> ReadSummaryByNumberAsync(
        SqlConnection conn,
        string number,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(OrdersPagedProcName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        };
        cmd.Parameters.Add(new SqlParameter("@Page",     SqlDbType.Int)            { Value = 1 });
        cmd.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int)            { Value = 5 });
        cmd.Parameters.Add(new SqlParameter("@Search",   SqlDbType.NVarChar, 200)  { Value = number });
        cmd.Parameters.Add(new SqlParameter("@Status",   SqlDbType.NVarChar, 100)  { Value = DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Store",    SqlDbType.NVarChar, 100)  { Value = DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@HasDebt",  SqlDbType.Bit)            { Value = DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@DateFrom", SqlDbType.Date)           { Value = DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@DateTo",   SqlDbType.Date)           { Value = DBNull.Value });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var summary = ReadOrderSummary(reader);
            if (string.Equals(summary.Number, number, StringComparison.OrdinalIgnoreCase))
            {
                return summary;
            }
        }
        return null;
    }

    /// <summary>
    /// Projects a row of <c>dbo.weblb_Orders_Paged</c> output into the
    /// <see cref="OrderSummaryDto"/> shape the WebUI expects. Column ordinals
    /// are kept in sync with the SP definition (see <c>.scratch/sp-paged.sql</c>).
    /// </summary>
    private static OrderSummaryDto ReadOrderSummary(SqlDataReader reader)
    {
        var debt = ReadDecimalByOrdinal(reader, ordinal: 4);
        var status = ReadStringByOrdinal(reader, ordinal: 6, fallback: string.Empty);

        return new OrderSummaryDto(
            Id:             ReadInt64ByOrdinal(reader, ordinal: 0),
            Number:         ReadStringByOrdinal(reader, ordinal: 1, fallback: string.Empty),
            Date:           ReadDateOnlyByOrdinal(reader, ordinal: 2),
            Price:          ReadDecimalByOrdinal(reader, ordinal: 3),
            Debt:           debt,
            IsOverdue:      false,
            Customer:       ReadStringByOrdinal(reader, ordinal: 5, fallback: string.Empty),
            HasDebt:        debt > 0m,
            IsVip:          false,
            HasNote:        false,
            Status:         status,
            StatusCode:     SalesOrderStatusMap.ToStatusCode(status),
            Store:          ReadStringByOrdinal(reader, ordinal: 7, fallback: string.Empty),
            // ordinals 8 (IsCancelled) and 9 (sImageKey) are returned by the
            // SP but currently unused by the C# DTO — left in the result set
            // for parity with the legacy weblb_Orders shape.
            Address:        ReadStringByOrdinal(reader, ordinal: 10, fallback: string.Empty),
            ProductsSearch: ReadStringOrNullByOrdinal(reader, ordinal: 11));
    }

    /// <summary>
    /// SQL parameter helper: emit <see cref="DBNull.Value"/> for null / blank
    /// strings so the SP's <c>IS NULL</c> branches fire instead of looking
    /// for the literal <c>N''</c>.
    /// </summary>
    private static object NullableNVarChar(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

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
    /// Reduces the Lithuanian duty title to a stable short code the WebUI uses
    /// to pick the <c>duty--*</c> dot color. Recognised buckets (per business
    /// confirmation 2026-05-02): <c>kons</c> Konsultantas, <c>vady</c>
    /// Vadybininkas, <c>matu</c> Matuotojas, <c>mont</c> Montuotojas,
    /// <c>trans</c> Transportas. Anything else returns empty so the WebUI
    /// renders a neutral light-grey dot — the data is small and any new role
    /// only needs an extra branch here plus a one-line CSS rule.
    /// </summary>
    private static string BuildDutyCode(string dutyLabel)
    {
        if (string.IsNullOrWhiteSpace(dutyLabel)) return string.Empty;
        var lower = dutyLabel.Trim().ToLowerInvariant();

        if (lower.Contains("konsult"))                                  return "kons";
        if (lower.Contains("vadyb"))                                    return "vady";
        if (lower.Contains("matuot"))                                   return "matu";
        if (lower.Contains("montuot") || lower.Contains("install"))     return "mont";
        if (lower.Contains("transport"))                                return "trans";

        return string.Empty;
    }

    private static DateOnly? ParseLegacyDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }
}
