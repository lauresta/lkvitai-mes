using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Stub;

/// <summary>
/// In-memory stub of <see cref="IOrdersQueryService"/> used in S-1 to feed the
/// Sales WebUI from the API while no real data source exists. The seven sample
/// orders mirror the S-0 mock data 1:1 (same numbers, customers, statuses).
/// Replaced in S-2 by the real SQL Server adapter over the legacy
/// <c>weblb_*</c> stored procedures.
/// </summary>
public sealed class StubOrdersQueryService : IOrdersQueryService
{
    private static readonly IReadOnlyList<OrderSummaryDto> Orders = BuildOrders();
    private static readonly IReadOnlyList<OrderItemGroupDto> SampleItemGroups = BuildSampleItemGroups();
    private static readonly IReadOnlyList<OrderAmountDto> SampleAmounts = BuildSampleAmounts();
    private static readonly IReadOnlyList<OrderEmployeeDto> SampleEmployees = BuildSampleEmployees();
    private static readonly OrderOperatorDto SampleOperator = new(
        Name: "Rūta Markevičienė",
        At: new DateTime(2026, 4, 29, 9, 42, 17, DateTimeKind.Unspecified));

    public Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(
        OrdersQueryParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<OrderSummaryDto> filtered = Orders;

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

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(o =>
                string.Equals(o.Status, query.Status, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(query.Store))
        {
            filtered = filtered.Where(o =>
                string.Equals(o.Store, query.Store, StringComparison.Ordinal));
        }

        if (query.HasDebt)
        {
            filtered = filtered.Where(o => o.HasDebt);
        }

        // S-1 ignores the Date preset on purpose — sample dates are clustered around
        // 2026-04, so applying "30d / month / ytd" against the real clock would empty
        // the list. Real date semantics arrive in S-2 (handoff R-7).

        var materialised = filtered.ToList();
        var pageSize = query.PageSize <= 0 ? 100 : query.PageSize;
        var page = query.Page <= 0 ? 1 : query.Page;
        var pagedItems = materialised
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PagedResult<OrderSummaryDto>(pagedItems, materialised.Count, page, pageSize);
        return Task.FromResult(result);
    }

    public Task<OrderDetailsDto?> GetOrderDetailsAsync(
        string number,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return Task.FromResult<OrderDetailsDto?>(null);
        }

        var summary = Orders.FirstOrDefault(o =>
            string.Equals(o.Number, number, StringComparison.OrdinalIgnoreCase));

        if (summary is null)
        {
            return Task.FromResult<OrderDetailsDto?>(null);
        }

        var details = new OrderDetailsDto(
            Id:         summary.Id,
            Number:     summary.Number,
            Date:       summary.Date,
            Price:      summary.Price,
            Debt:       summary.Debt,
            IsOverdue:  summary.IsOverdue,
            Customer:   summary.Customer,
            HasDebt:    summary.HasDebt,
            IsVip:      summary.IsVip,
            HasNote:    summary.HasNote,
            Status:     summary.Status,
            StatusCode: summary.StatusCode,
            Store:      summary.Store,
            Address:    summary.Address,
            Operator:   SampleOperator,
            ItemGroups: SampleItemGroups,
            Amounts:    SampleAmounts,
            Employees:  SampleEmployees);

        return Task.FromResult<OrderDetailsDto?>(details);
    }

    private static IReadOnlyList<OrderSummaryDto> BuildOrders()
    {
        return new List<OrderSummaryDto>
        {
            new(
                Id:             1,
                Number:         "KVT-240518-018",
                Date:           new DateOnly(2026, 4, 29),
                Price:          1482.00m,
                Debt:           482.00m,
                IsOverdue:      false,
                Customer:       "UAB Audenis Projektai",
                HasDebt:        true,
                IsVip:          false,
                HasNote:        false,
                Status:         "Gaminamas",
                StatusCode:     OrderStatusCodes.InProgress,
                Store:          "Vilnius",
                Address:        "Ukmergės g. 221, Vilnius",
                ProductsSearch: "Roller blind R120 White Bedroom Metal chain DN45 Graphite Vertical V78 Sand Office"),
            new(
                Id:             2,
                Number:         "KVT-240517-044",
                Date:           new DateOnly(2026, 4, 28),
                Price:          746.50m,
                Debt:           0m,
                IsOverdue:      false,
                Customer:       "Ingrida Petrauskienė",
                HasDebt:        false,
                IsVip:          false,
                HasNote:        false,
                Status:         "Patvirtintas",
                StatusCode:     OrderStatusCodes.Approved,
                Store:          "Kaunas",
                Address:        "Savanorių pr. 284, Kaunas",
                ProductsSearch: "Day-night blind DN45 Graphite Kitchen window"),
            new(
                Id:             3,
                Number:         "KVT-240516-102",
                Date:           new DateOnly(2026, 4, 27),
                Price:          2330.00m,
                Debt:           2330.00m,
                IsOverdue:      true,
                Customer:       "MB Namai Sau",
                HasDebt:        true,
                IsVip:          true,
                HasNote:        false,
                Status:         "Įvestas",
                StatusCode:     OrderStatusCodes.Entered,
                Store:          "Klaipėda",
                Address:        "Taikos pr. 52, Klaipėda",
                ProductsSearch: "Vertical blinds V78 Sand Office split controls Aluminium track"),
            new(
                Id:             4,
                Number:         "KVT-240516-076",
                Date:           new DateOnly(2026, 4, 26),
                Price:          392.80m,
                Debt:           0m,
                IsOverdue:      false,
                Customer:       "Rasa Butkutė",
                HasDebt:        false,
                IsVip:          false,
                HasNote:        false,
                Status:         "Pagamintas",
                StatusCode:     OrderStatusCodes.Made,
                Store:          "Vilnius",
                Address:        "Kalvarijų g. 125, Vilnius",
                ProductsSearch: "Roller blind R200 Cream Living room Plastic chain"),
            new(
                Id:             5,
                Number:         "KVT-240515-031",
                Date:           new DateOnly(2026, 4, 25),
                Price:          924.10m,
                Debt:           124.10m,
                IsOverdue:      false,
                Customer:       "UAB Šiaurės Apartamentai",
                HasDebt:        false,
                IsVip:          false,
                HasNote:        true,
                Status:         "Filialui",
                StatusCode:     OrderStatusCodes.Shipped,
                Store:          "Panevėžys",
                Address:        "Respublikos g. 39, Panevėžys",
                ProductsSearch: "Roman blind RM210 Linen Conference Decorative trim"),
            new(
                Id:             6,
                Number:         "KVT-240514-009",
                Date:           new DateOnly(2026, 4, 24),
                Price:          1096.00m,
                Debt:           0m,
                IsOverdue:      false,
                Customer:       "Darius Vaitkus",
                HasDebt:        false,
                IsVip:          false,
                HasNote:        false,
                Status:         "Atiduotas",
                StatusCode:     OrderStatusCodes.Delivered,
                Store:          "Šiauliai",
                Address:        "Tilžės g. 109, Šiauliai",
                ProductsSearch: "Pleated blind P150 Beige Bathroom Side guides"),
            new(
                Id:             7,
                Number:         "KVT-240513-088",
                Date:           new DateOnly(2026, 4, 22),
                Price:          615.70m,
                Debt:           615.70m,
                IsOverdue:      true,
                Customer:       "UAB Interjero Linija",
                HasDebt:        true,
                IsVip:          false,
                HasNote:        false,
                Status:         "Sustabdytas",
                StatusCode:     OrderStatusCodes.Paused,
                Store:          "Vilnius",
                Address:        "Konstitucijos pr. 12, Vilnius",
                ProductsSearch: "Venetian blind V25 Aluminium Office Tilt cord"),
        };
    }

    private static IReadOnlyList<OrderItemGroupDto> BuildSampleItemGroups()
    {
        return new List<OrderItemGroupDto>
        {
            new(
                Label: "1. Roller blind Audinys R120",
                Lines: new List<OrderItemDto>
                {
                    new(
                        Num:         "1",
                        Name:        "Roller blind Audinys R120",
                        Side:        "Left",
                        Color:       "White",
                        Width:       1250.00m,
                        Height:      1710.00m,
                        Notes:       "Bedroom window",
                        Qty:         2m,
                        Price:       148.00m,
                        Amount:      296.00m,
                        IsAccessory: false),
                    new(
                        Num:         string.Empty,
                        Name:        "Metal chain 100 cm",
                        Side:        string.Empty,
                        Color:       string.Empty,
                        Width:       null,
                        Height:      null,
                        Notes:       string.Empty,
                        Qty:         2m,
                        Price:       8.00m,
                        Amount:      16.00m,
                        IsAccessory: true),
                }),
            new(
                Label: "2. Day-night blind DN45",
                Lines: new List<OrderItemDto>
                {
                    new(
                        Num:         "2",
                        Name:        "Day-night blind DN45",
                        Side:        "Right",
                        Color:       "Graphite",
                        Width:       980.00m,
                        Height:      1420.00m,
                        Notes:       "Kitchen",
                        Qty:         1m,
                        Price:       210.00m,
                        Amount:      210.00m,
                        IsAccessory: false),
                }),
            new(
                Label: "3. Vertical blinds V78",
                Lines: new List<OrderItemDto>
                {
                    new(
                        Num:         "3",
                        Name:        "Vertical blinds V78",
                        Side:        "Center",
                        Color:       "Sand",
                        Width:       2420.00m,
                        Height:      2360.00m,
                        Notes:       "Office, split controls",
                        Qty:         1m,
                        Price:       960.00m,
                        Amount:      960.00m,
                        IsAccessory: false),
                }),
        };
    }

    private static IReadOnlyList<OrderAmountDto> BuildSampleAmounts()
    {
        return new List<OrderAmountDto>
        {
            new(OrderAmountKind.Defined,       "Defined",        Amount: 1482.00m, Percent: null),
            new(OrderAmountKind.Calculated,    "Calculated",     Amount: 1540.00m, Percent: null),
            new(OrderAmountKind.Discount,      "Discount",       Amount: null,     Percent: 5.00m),
            new(OrderAmountKind.AfterDiscount, "After discount", Amount: 1482.00m, Percent: null),
            new(OrderAmountKind.Paid,          "Paid",           Amount: 1000.00m, Percent: null),
            new(OrderAmountKind.Debt,          "Debt",           Amount: 482.00m,  Percent: null),
        };
    }

    private static IReadOnlyList<OrderEmployeeDto> BuildSampleEmployees()
    {
        return new List<OrderEmployeeDto>
        {
            new(
                Name:             "Rūta Markevičienė",
                Initials:         "RM",
                DutyCode:         "sales",
                DutyLabel:        "Sales consultant",
                ServiceDate:      new DateOnly(2026, 4, 29),
                AcquaintanceDate: new DateOnly(2026, 4, 29),
                OrderQty:         1,
                ItemQty:          4,
                Amount:           1482.00m),
            new(
                Name:             "Mantas Jankauskas",
                Initials:         "MJ",
                DutyCode:         "prod",
                DutyLabel:        "Production",
                ServiceDate:      new DateOnly(2026, 4, 30),
                AcquaintanceDate: new DateOnly(2026, 4, 30),
                OrderQty:         1,
                ItemQty:          3,
                Amount:           1210.00m),
            new(
                Name:             "Tomas Varnas",
                Initials:         "TV",
                DutyCode:         "inst",
                DutyLabel:        "Installation",
                ServiceDate:      new DateOnly(2026, 5, 3),
                AcquaintanceDate: new DateOnly(2026, 5, 3),
                OrderQty:         1,
                ItemQty:          4,
                Amount:           272.00m),
        };
    }
}
