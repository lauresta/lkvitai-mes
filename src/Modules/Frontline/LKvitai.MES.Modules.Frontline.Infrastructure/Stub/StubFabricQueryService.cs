using System.Text.RegularExpressions;
using LKvitai.MES.Modules.Frontline.Application.Ports;
using LKvitai.MES.Modules.Frontline.Contracts.Common;
using LKvitai.MES.Modules.Frontline.Contracts.Fabric;

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Stub;

/// <summary>
/// In-memory implementation of <see cref="IFabricQueryService"/> for F-1 design
/// validation. Returns the exact six fabrics the F-0 Razor pages displayed
/// inline, so wiring the WebUI to the API does not visibly change the screen.
/// </summary>
/// <remarks>
/// <para>
/// <b>F-2 will replace this with <c>SqlFabricQueryService</c>.</b> The stub
/// intentionally keeps the legacy validation rules the SQL adapter will
/// inherit:
/// </para>
/// <list type="bullet">
///   <item>uppercase the fabric code before lookup;</item>
///   <item>reject codes that don't match <c>^[A-Z0-9\-_./]{2,}$</c>;</item>
///   <item>pick the smallest available width when no <c>?width=</c> is supplied,
///   matching <c>FabricAvailabilityController.Mobile</c> in the legacy app.</item>
/// </list>
/// <para>
/// <b>Deferred to F-2 (R-6 / R-7):</b>
/// </para>
/// <list type="bullet">
///   <item><c>weblb_Fabric_GetLowStockList</c> proc — derived from the legacy
///   <c>web_RemainsAll</c> view. Will project (Code, Name, WidthMm,
///   AvailableMeters, ThresholdMeters, Status, ETA, Incoming, Supplier,
///   Alternatives) with paged + filtered output.</item>
///   <item><c>Frontline_FabricLastChecked</c> table — populated on every
///   successful mobile lookup, surfaced as the <c>LastChecked</c> column on the
///   low-stock list (purchasing wants to know which fabrics are actively being
///   asked for on the floor).</item>
///   <item><c>Incoming meters / dates</c> — needs an extension to
///   <c>weblb_Fabric_GetMobileCard</c> RS2 (or a join to a supplier-orders
///   table); not present in the current legacy proc.</item>
/// </list>
/// </remarks>
public sealed class StubFabricQueryService : IFabricQueryService
{
    private static readonly Regex CodeShape =
        new("^[A-Z0-9\\-_./]{2,}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static readonly IReadOnlyDictionary<string, FabricCardDto> Cards = BuildCards();
    private static readonly IReadOnlyList<FabricLowStockDto> LowStock = BuildLowStock();

    public Task<FabricCardDto?> GetMobileCardAsync(
        FabricLookupParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Code))
        {
            return Task.FromResult<FabricCardDto?>(null);
        }

        var normalised = query.Code.Trim().ToUpperInvariant();
        if (!CodeShape.IsMatch(normalised))
        {
            return Task.FromResult<FabricCardDto?>(null);
        }

        if (!Cards.TryGetValue(normalised, out var card))
        {
            return Task.FromResult<FabricCardDto?>(null);
        }

        var widths = card.Widths;
        var selected =
            query.Width is { } w && widths.Any(x => x.WidthMm == w)
                ? w
                : widths.OrderBy(x => x.WidthMm).First().WidthMm;

        var resolved = card with { SelectedWidthMm = selected };
        return Task.FromResult<FabricCardDto?>(resolved);
    }

    public Task<PagedResult<FabricLowStockDto>> GetLowStockListAsync(
        FabricLowStockQueryParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<FabricLowStockDto> rows = LowStock;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim();
            rows = rows.Where(r =>
                r.Code.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                (r.Supplier?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                r.AlternativeCodes.Any(a => a.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        rows = (query.Status?.ToLowerInvariant()) switch
        {
            "low"  => rows.Where(r => r.Status == FabricAvailabilityStatus.Low),
            "out"  => rows.Where(r => r.Status == FabricAvailabilityStatus.None),
            "disc" => rows.Where(r => r.Status == FabricAvailabilityStatus.Discontinued),
            _ => rows,
        };

        if (query.ThresholdMeters is { } threshold)
        {
            if (threshold <= 0)
            {
                rows = rows.Where(r => r.Status == FabricAvailabilityStatus.None);
            }
            else
            {
                rows = rows.Where(r =>
                    r.AvailableMeters <= threshold ||
                    r.Status is FabricAvailabilityStatus.Discontinued or FabricAvailabilityStatus.None);
            }
        }

        if (query.WidthMm is { } widthMm)
        {
            rows = rows.Where(r => r.WidthMm == widthMm);
        }

        if (!string.IsNullOrWhiteSpace(query.Supplier))
        {
            rows = rows.Where(r =>
                string.Equals(r.Supplier, query.Supplier, StringComparison.OrdinalIgnoreCase));
        }

        var materialised = rows.ToList();
        var total = materialised.Count;

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 500 ? 50 : query.PageSize;

        var paged = materialised
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<FabricLowStockDto>(paged, total, page, pageSize));
    }

    private static IReadOnlyDictionary<string, FabricCardDto> BuildCards()
    {
        const string PlaceholderPhoto = "/img/fabric_pl.png";

        return new Dictionary<string, FabricCardDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["R120"] = new(
                Code: "R120",
                Name: "Linen texture, warm grey",
                PhotoUrl: PlaceholderPhoto,
                Notes: "Use with white bottom bar. Similar tone to R118.",
                DiscountPercent: 15,
                Widths: new WidthStockDto[]
                {
                    new(1600, FabricAvailabilityStatus.Enough, 42, null, null, null),
                    new(2000, FabricAvailabilityStatus.Low,     8, new DateOnly(2026, 5, 8),  60, new DateOnly(2026, 5, 8)),
                    new(2500, FabricAvailabilityStatus.None,    0, new DateOnly(2026, 5, 12), 40, new DateOnly(2026, 5, 12)),
                },
                SelectedWidthMm: null,
                Alternatives: new FabricAlternativeDto[]
                {
                    new("R118", PlaceholderPhoto, 2000, FabricAvailabilityStatus.Enough, 34, null),
                    new("R124", PlaceholderPhoto, 2000, FabricAvailabilityStatus.Low,    11, new DateOnly(2026, 5, 6)),
                    new("R130", PlaceholderPhoto, 2000, FabricAvailabilityStatus.Enough, 68, null),
                }),
            ["DN45"] = new(
                Code: "DN45",
                Name: "Day-night graphite stripe",
                PhotoUrl: PlaceholderPhoto,
                Notes: null,
                DiscountPercent: null,
                Widths: new WidthStockDto[]
                {
                    new(1600, FabricAvailabilityStatus.Low,  12, new DateOnly(2026, 5, 12),  80, new DateOnly(2026, 5, 12)),
                    new(1800, FabricAvailabilityStatus.None,  0, new DateOnly(2026, 5, 12), 120, new DateOnly(2026, 5, 12)),
                },
                SelectedWidthMm: null,
                Alternatives: new FabricAlternativeDto[]
                {
                    new("DN47", PlaceholderPhoto, 1800, FabricAvailabilityStatus.Enough, 56, null),
                    new("DN51", PlaceholderPhoto, 1800, FabricAvailabilityStatus.Low,    14, new DateOnly(2026, 5, 9)),
                }),
            ["V78"] = new(
                Code: "V78",
                Name: "Vertical blind sand",
                PhotoUrl: PlaceholderPhoto,
                Notes: "Pairs naturally with V80 for two-tone installs.",
                DiscountPercent: null,
                Widths: new WidthStockDto[]
                {
                    new(2500, FabricAvailabilityStatus.Low, 6, new DateOnly(2026, 5, 6), 40, new DateOnly(2026, 5, 6)),
                },
                SelectedWidthMm: null,
                Alternatives: new FabricAlternativeDto[]
                {
                    new("V80", PlaceholderPhoto, 2500, FabricAvailabilityStatus.Enough, 11, null),
                }),
        };
    }

    private static IReadOnlyList<FabricLowStockDto> BuildLowStock()
    {
        const string PlaceholderPhoto = "/img/fabric_pl.png";

        return new FabricLowStockDto[]
        {
            new(
                Code: "R120",
                Name: "Linen texture, warm grey",
                PhotoUrl: PlaceholderPhoto,
                WidthMm: 2000,
                AvailableMeters: 8,
                ThresholdMeters: 25,
                Status: FabricAvailabilityStatus.Low,
                ExpectedDate: new DateOnly(2026, 5, 8),
                IncomingMeters: 60,
                Supplier: "Decora",
                AlternativeCodes: new[] { "R118", "R124" },
                LastChecked: new DateTimeOffset(2026, 5, 2, 9, 42, 0, TimeSpan.Zero),
                CanReserve: true,
                CanNotify: false,
                CanReplace: false),
            new(
                Code: "DN45",
                Name: "Day-night graphite stripe",
                PhotoUrl: PlaceholderPhoto,
                WidthMm: 1800,
                AvailableMeters: 0,
                ThresholdMeters: 25,
                Status: FabricAvailabilityStatus.None,
                ExpectedDate: new DateOnly(2026, 5, 12),
                IncomingMeters: 120,
                Supplier: "Vali",
                AlternativeCodes: new[] { "DN47", "DN51" },
                LastChecked: new DateTimeOffset(2026, 5, 2, 9, 35, 0, TimeSpan.Zero),
                CanReserve: false,
                CanNotify: true,
                CanReplace: false),
            new(
                Code: "V78",
                Name: "Vertical blind sand",
                PhotoUrl: PlaceholderPhoto,
                WidthMm: 2500,
                AvailableMeters: 6,
                ThresholdMeters: 25,
                Status: FabricAvailabilityStatus.Low,
                ExpectedDate: new DateOnly(2026, 5, 6),
                IncomingMeters: 40,
                Supplier: "Marla",
                AlternativeCodes: new[] { "V80" },
                LastChecked: new DateTimeOffset(2026, 5, 2, 9, 30, 0, TimeSpan.Zero),
                CanReserve: true,
                CanNotify: false,
                CanReplace: false),
            new(
                Code: "R099",
                Name: "Blackout terracotta",
                PhotoUrl: PlaceholderPhoto,
                WidthMm: 1600,
                AvailableMeters: 2,
                ThresholdMeters: 25,
                Status: FabricAvailabilityStatus.Discontinued,
                ExpectedDate: null,
                IncomingMeters: null,
                Supplier: "Decora",
                AlternativeCodes: new[] { "R101" },
                LastChecked: new DateTimeOffset(2026, 5, 2, 9, 21, 0, TimeSpan.Zero),
                CanReserve: false,
                CanNotify: false,
                CanReplace: true),
            new(
                Code: "R118",
                Name: "Linen texture, cool grey",
                PhotoUrl: PlaceholderPhoto,
                WidthMm: 2500,
                AvailableMeters: 9,
                ThresholdMeters: 25,
                Status: FabricAvailabilityStatus.Low,
                ExpectedDate: new DateOnly(2026, 5, 10),
                IncomingMeters: 80,
                Supplier: "Decora",
                AlternativeCodes: new[] { "R120", "R124" },
                LastChecked: new DateTimeOffset(2026, 5, 2, 9, 18, 0, TimeSpan.Zero),
                CanReserve: true,
                CanNotify: false,
                CanReplace: false),
            new(
                Code: "V80",
                Name: "Vertical blind seafoam",
                PhotoUrl: PlaceholderPhoto,
                WidthMm: 2500,
                AvailableMeters: 11,
                ThresholdMeters: 25,
                Status: FabricAvailabilityStatus.Low,
                ExpectedDate: new DateOnly(2026, 5, 14),
                IncomingMeters: 50,
                Supplier: "Marla",
                AlternativeCodes: Array.Empty<string>(),
                LastChecked: new DateTimeOffset(2026, 5, 2, 9, 2, 0, TimeSpan.Zero),
                CanReserve: true,
                CanNotify: false,
                CanReplace: false),
        };
    }
}
