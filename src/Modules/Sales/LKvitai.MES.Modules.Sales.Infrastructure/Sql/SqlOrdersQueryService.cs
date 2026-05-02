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
    private const string OrdersProcName       = "dbo.weblb_Orders";
    private const string OrdersPagedProcName  = "dbo.weblb_Orders_Paged";
    private const string OrderProcName        = "dbo.weblb_Order";
    private const string AccessoriesProcName  = "dbo.weblb_Accessories";
    private const string ItemsProcName        = "dbo.weblb_Items";
    private const string EmployeesProcName    = "dbo.weblb_Employees";
    private const string OrderIdParameter     = "@OrderId";

    private readonly SalesSqlOptions _options;
    private readonly ILogger<SqlOrdersQueryService> _logger;

    // Single-flight in-process snapshot cache for dbo.weblb_Orders. Guarded by
    // a SemaphoreSlim so that N concurrent page-change requests behind a stale
    // snapshot only trigger ONE SQL re-fetch — the rest wait, then return the
    // freshly populated list. The cached list is treated as immutable (every
    // reader uses LINQ enumeration; no mutation in ApplyFilterSortPage), so
    // it is safe to share across concurrent callers without copying.
    //
    // Stop-gap until a paged dbo.weblb_Orders_Paged wrapper exists; see the
    // class header TODO. TTL is configurable via SalesSqlOptions; 0 disables
    // caching entirely.
    private readonly SemaphoreSlim _snapshotGate = new(initialCount: 1, maxCount: 1);
    private List<OrderSummaryDto> _snapshotRows = new(capacity: 0);
    private DateTime _snapshotExpiresUtc = DateTime.MinValue;

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

        // Two implementations live behind this method (selected by config —
        // SalesSqlOptions.QueryMode). The behaviour seen by the caller is the
        // same; only the latency profile and where the work happens differ.
        return _options.QueryMode == OrdersQueryMode.Paged
            ? await GetOrdersFromPagedSpAsync(query, cancellationToken).ConfigureAwait(false)
            : await GetOrdersFromSnapshotAsync(query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Snapshot path. Materialises <c>dbo.weblb_Orders</c> in full (cached for
    /// <see cref="SalesSqlOptions.OrdersListCacheTtlSeconds"/>) and runs
    /// filter / sort / page in C#. Trades freshness for simplicity — was the
    /// only path before <c>dbo.weblb_Orders_Paged</c> existed.
    /// </summary>
    private async Task<PagedResult<OrderSummaryDto>> GetOrdersFromSnapshotAsync(
        OrdersQueryParams query,
        CancellationToken cancellationToken)
    {
        // Phase 0 instrumentation: split the SQL read from the in-memory
        // filter/sort/page pass so we can attribute latency to whichever side
        // is the actual cost. Connection string and credentials never logged.
        var totalSw = Stopwatch.StartNew();
        var sqlSw = Stopwatch.StartNew();
        var all = await ReadAllOrdersAsync(cancellationToken).ConfigureAwait(false);
        sqlSw.Stop();

        var postSw = Stopwatch.StartNew();
        var result = ApplyFilterSortPage(all, query);
        postSw.Stop();
        totalSw.Stop();

        _logger.LogInformation(
            "[SalesPerf] sql.GetOrders mode=snapshot rows={SourceRows} returned={Returned} total={Total} page={Page} pageSize={PageSize} " +
            "search={HasSearch} status={HasStatus} store={HasStore} hasDebt={HasDebt} " +
            "sqlMs={SqlMs} postProcMs={PostProcMs} totalMs={TotalMs}",
            all.Count, result.Items.Count, result.Total, result.Page, result.PageSize,
            !string.IsNullOrWhiteSpace(query.Search), !string.IsNullOrWhiteSpace(query.Status),
            !string.IsNullOrWhiteSpace(query.Store), query.HasDebt,
            sqlSw.ElapsedMilliseconds, postSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// Paged path. Calls <c>dbo.weblb_Orders_Paged</c> with the filter / page
    /// parameters; the SP returns just the requested page plus a windowed
    /// <c>TotalRows</c> value (replicated on every row). No in-process cache
    /// for the orders list itself — every call is a fresh SP execution, so
    /// the cost scales with <c>PageSize</c>, not with the table size.
    /// </summary>
    private async Task<PagedResult<OrderSummaryDto>> GetOrdersFromPagedSpAsync(
        OrdersQueryParams query,
        CancellationToken cancellationToken)
    {
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
            var id             = ReadInt64ByOrdinal(reader, ordinal: 0);
            var number         = ReadStringByOrdinal(reader, ordinal: 1, fallback: string.Empty);
            var date           = ReadDateOnlyByOrdinal(reader, ordinal: 2);
            var price          = ReadDecimalByOrdinal(reader, ordinal: 3);
            var debt           = ReadDecimalByOrdinal(reader, ordinal: 4);
            var customer       = ReadStringByOrdinal(reader, ordinal: 5, fallback: string.Empty);
            var status         = ReadStringByOrdinal(reader, ordinal: 6, fallback: string.Empty);
            var store          = ReadStringByOrdinal(reader, ordinal: 7, fallback: string.Empty);
            // ordinals 8 (IsCancelled) and 9 (sImageKey) are returned by the
            // SP but currently unused by the C# DTO — left in the result set
            // for parity with the legacy weblb_Orders shape.
            var address        = ReadStringByOrdinal(reader, ordinal: 10, fallback: string.Empty);
            var productsSearch = ReadStringOrNullByOrdinal(reader, ordinal: 11);
            // TotalRows is windowed COUNT(*) OVER () — same value on every row;
            // we just keep the last one read (or zero if the page is empty).
            totalRows = (int)ReadInt64ByOrdinal(reader, ordinal: 12);

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
        matSw.Stop();
        totalSw.Stop();

        _logger.LogInformation(
            "[SalesPerf] sql.GetOrders mode=paged returned={Returned} total={Total} page={Page} pageSize={PageSize} " +
            "search={HasSearch} status={HasStatus} store={HasStore} hasDebt={HasDebt} " +
            "openMs={OpenMs} execMs={ExecMs} materialiseMs={MaterialiseMs} totalMs={TotalMs}",
            rows.Count, totalRows, page, pageSize,
            !string.IsNullOrWhiteSpace(query.Search), !string.IsNullOrWhiteSpace(query.Status),
            !string.IsNullOrWhiteSpace(query.Store), query.HasDebt,
            openSw.ElapsedMilliseconds, execSw.ElapsedMilliseconds,
            matSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

        return new PagedResult<OrderSummaryDto>(rows, totalRows, page, pageSize);
    }

    /// <summary>
    /// SQL parameter helper: emit <see cref="DBNull.Value"/> for null / blank
    /// strings so the SP's <c>IS NULL</c> branches fire instead of looking
    /// for the literal <c>N''</c>.
    /// </summary>
    private static object NullableNVarChar(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    public async Task<OrdersFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        // Same full-table read as GetOrdersAsync (weblb_Orders has no parameters).
        // We project the two label sets the WebUI toolbar needs; the data is
        // small enough that an ad-hoc DISTINCT in C# is cheaper than another
        // round trip. When the paged proc wrapper lands (TODO above), expose
        // dedicated dbo.weblb_StatusList / dbo.weblb_StoreList and wire them
        // here instead.
        var totalSw = Stopwatch.StartNew();
        var sqlSw = Stopwatch.StartNew();
        var all = await ReadAllOrdersAsync(cancellationToken).ConfigureAwait(false);
        sqlSw.Stop();

        var projSw = Stopwatch.StartNew();
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
        projSw.Stop();
        totalSw.Stop();

        _logger.LogInformation(
            "[SalesPerf] sql.GetFilterOptions rows={SourceRows} statuses={StatusCount} stores={StoreCount} " +
            "sqlMs={SqlMs} projectMs={ProjectMs} totalMs={TotalMs}",
            all.Count, statuses.Count, stores.Count,
            sqlSw.ElapsedMilliseconds, projSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

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

        // Phase 1: locate the summary row in weblb_Orders so we can resolve the
        // legacy Id (the @OrderId parameter all other procs require) and seed
        // the price / debt / store / status fields the details proc does not
        // return.
        var lookupSw = Stopwatch.StartNew();
        var summary = (await ReadAllOrdersAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(o => string.Equals(o.Number, number, StringComparison.OrdinalIgnoreCase));
        lookupSw.Stop();

        if (summary is null)
        {
            _logger.LogInformation(
                "[SalesPerf] sql.GetOrderDetails number={Number} found=False lookupMs={LookupMs} totalMs={TotalMs}",
                number, lookupSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);
            return null;
        }

        var openSw = Stopwatch.StartNew();
        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        openSw.Stop();

        // Phase 2: header (operator + amounts + authoritative address override).
        OrderOperatorDto? @operator = null;
        var amounts = new List<OrderAmountDto>(6);
        var headerCustomer = summary.Customer;
        var headerStatus   = summary.Status;
        var headerAddress  = summary.Address;

        var headerSw = Stopwatch.StartNew();
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
        headerSw.Stop();

        // Phase 3: accessories (collected first so we can attach them to their parent items).
        var accSw = Stopwatch.StartNew();
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
        accSw.Stop();

        // Phase 4: items — each becomes one OrderItemGroupDto seeded with its parent line plus accessories.
        var itemsSw = Stopwatch.StartNew();
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
        itemsSw.Stop();

        // Phase 5: employees.
        var empSw = Stopwatch.StartNew();
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
        empSw.Stop();
        totalSw.Stop();

        _logger.LogInformation(
            "[SalesPerf] sql.GetOrderDetails number={Number} found=True items={ItemGroups} accessories={AccessoryItems} employees={EmployeeCount} " +
            "lookupMs={LookupMs} openMs={OpenMs} headerMs={HeaderMs} accMs={AccMs} itemsMs={ItemsMs} empMs={EmpMs} totalMs={TotalMs}",
            number, itemGroups.Count, accessoriesByItemId.Sum(kv => kv.Value.Count), employees.Count,
            lookupSw.ElapsedMilliseconds, openSw.ElapsedMilliseconds, headerSw.ElapsedMilliseconds,
            accSw.ElapsedMilliseconds, itemsSw.ElapsedMilliseconds, empSw.ElapsedMilliseconds,
            totalSw.ElapsedMilliseconds);

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
    /// <summary>
    /// Returns the materialised <c>dbo.weblb_Orders</c> result set, served from
    /// an in-process snapshot whose lifetime is bounded by
    /// <see cref="SalesSqlOptions.OrdersListCacheTtlSeconds"/>. Behind a
    /// <see cref="SemaphoreSlim"/>: while one caller is fetching a fresh
    /// snapshot, every concurrent caller waits and then reuses the same
    /// list (single-flight). Set the TTL to <c>0</c> to disable the cache
    /// and read from SQL on every call.
    /// </summary>
    private async Task<List<OrderSummaryDto>> ReadAllOrdersAsync(CancellationToken cancellationToken)
    {
        var ttlSeconds = _options.OrdersListCacheTtlSeconds;
        if (ttlSeconds <= 0)
        {
            // Caching disabled — preserve original "fresh SQL on every call"
            // behaviour. Useful for diagnostics and once a paged proc wrapper
            // makes the in-process snapshot obsolete.
            _logger.LogDebug("[SalesPerf] sql.cache disabled ttlSec={TtlSeconds}", ttlSeconds);
            return await LoadAllOrdersFromSqlAsync(cancellationToken).ConfigureAwait(false);
        }

        // Fast-path: snapshot still fresh, no lock needed. Reads of the two
        // fields are independently atomic on .NET 8 (reference + DateTime is
        // 8 bytes), so worst case we serve a snapshot that just expired —
        // identical to the post-lock branch under contention.
        var nowUtc = DateTime.UtcNow;
        var snapshotRows = _snapshotRows;
        var snapshotExpiresUtc = _snapshotExpiresUtc;
        if (nowUtc < snapshotExpiresUtc)
        {
            var ageMs = (long)(nowUtc - (snapshotExpiresUtc - TimeSpan.FromSeconds(ttlSeconds))).TotalMilliseconds;
            _logger.LogInformation(
                "[SalesPerf] sql.cache hit rows={Rows} ageMs={AgeMs} remainingMs={RemainingMs}",
                snapshotRows.Count, ageMs, (long)(snapshotExpiresUtc - nowUtc).TotalMilliseconds);
            return snapshotRows;
        }

        // Slow-path: stale or first call. Single-flight via SemaphoreSlim —
        // only one caller fetches; the rest wait then return the new snapshot.
        await _snapshotGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check: another caller may have refreshed the snapshot
            // while we were waiting on the semaphore. If so, return that.
            nowUtc = DateTime.UtcNow;
            if (nowUtc < _snapshotExpiresUtc)
            {
                _logger.LogInformation(
                    "[SalesPerf] sql.cache hit-after-wait rows={Rows} remainingMs={RemainingMs}",
                    _snapshotRows.Count, (long)(_snapshotExpiresUtc - nowUtc).TotalMilliseconds);
                return _snapshotRows;
            }

            var fresh = await LoadAllOrdersFromSqlAsync(cancellationToken).ConfigureAwait(false);
            _snapshotRows = fresh;
            _snapshotExpiresUtc = DateTime.UtcNow.AddSeconds(ttlSeconds);

            _logger.LogInformation(
                "[SalesPerf] sql.cache miss rows={Rows} ttlSec={TtlSeconds} expiresAtUtc={ExpiresAtUtc:O}",
                fresh.Count, ttlSeconds, _snapshotExpiresUtc);
            return fresh;
        }
        finally
        {
            _snapshotGate.Release();
        }
    }

    private async Task<List<OrderSummaryDto>> LoadAllOrdersFromSqlAsync(CancellationToken cancellationToken)
    {
        // Phase 0 instrumentation — split SQL into Open / Exec (TTFB)
        // / Materialise so we can tell *where* the time is going. These
        // segments are cheap (Stopwatch.GetTimestamp() ticks) and emitted
        // at Debug level only — the parent GetOrders/GetFilterOptions
        // method already logs the rolled-up sqlMs at Information.
        var rows = new List<OrderSummaryDto>(capacity: 1024);
        var totalSw = Stopwatch.StartNew();

        var openSw = Stopwatch.StartNew();
        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        openSw.Stop();

        await using var cmd = new SqlCommand(OrdersProcName, conn)
        {
            CommandType    = CommandType.StoredProcedure,
            CommandTimeout = _options.CommandTimeoutSeconds,
        };

        var execSw = Stopwatch.StartNew();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        execSw.Stop();

        var fieldCount = reader.FieldCount;
        var matSw = Stopwatch.StartNew();

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
        matSw.Stop();
        totalSw.Stop();

        _logger.LogDebug(
            "[SalesPerf] sql.weblb_Orders rows={Rows} openMs={OpenMs} execMs={ExecMs} materialiseMs={MaterialiseMs} totalMs={TotalMs}",
            rows.Count, openSw.ElapsedMilliseconds, execSw.ElapsedMilliseconds,
            matSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

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

        return string.Empty; // neutral dot — keeps the row legible without guessing
    }

    private static DateOnly? ParseLegacyDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }
}
