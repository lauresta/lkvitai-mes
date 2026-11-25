using System.Globalization;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ItemCardUiTests
{
    private static readonly CultureInfo Lt = CultureInfo.GetCultureInfo("lt-LT");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateTimeOffset T0 = new(2026, 11, 24, 12, 23, 0, TimeSpan.Zero);

    [Fact]
    public void Format_Quantities_UseCultureSeparators_AndUnicodeMinus()
    {
        ItemCardFormat.Qty(412.5m, Lt).Should().Be("412,5");
        ItemCardFormat.Qty(86m, Lt).Should().Be("86,0");
        ItemCardFormat.Qty(0.125m, Lt).Should().Be("0,125");
        ItemCardFormat.Qty(1234.5m, Lt).Should().Be("1 234,5");
        ItemCardFormat.Qty(412.5m, En).Should().Be("412.5");
        ItemCardFormat.Qty(-2m, Lt).Should().Be("−2,0");
        ItemCardFormat.Whole(150m, Lt).Should().Be("150");
        ItemCardFormat.Whole(null, Lt).Should().Be(ItemCardFormat.Dash);
    }

    [Fact]
    public void Format_Signed_Money_Percent_AndUnit()
    {
        ItemCardFormat.Signed(300m, Lt).Should().Be("+300,0");
        ItemCardFormat.Signed(-18m, Lt).Should().Be("−18,0");
        ItemCardFormat.Signed(0m, Lt).Should().Be("0,0");
        ItemCardFormat.Money(4.8m, Lt).Should().Be("4,80 €");
        ItemCardFormat.Money(null, Lt).Should().Be(ItemCardFormat.Dash);
        ItemCardFormat.Percent(57.45m, Lt).Should().Be("57,5 %");
        ItemCardFormat.Unit(" M ").Should().Be("m");
        ItemCardFormat.Unit(null).Should().BeEmpty();
        ItemCardFormat.QtyWithUnit(412.5m, "M", Lt).Should().Be("412,5 m");
        ItemCardFormat.QtyWithUnit(5m, null, Lt).Should().Be("5,0");
    }

    [Fact]
    public void Format_Dates_AreInvariant()
    {
        ItemCardFormat.Date(new DateOnly(2026, 12, 3)).Should().Be("2026-12-03");
        ItemCardFormat.ShortDate(new DateOnly(2026, 12, 3)).Should().Be("12-03");
        ItemCardFormat.AsOffset(new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Unspecified)).Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void StockRows_DropOutsideRows_AndSortOrdinal()
    {
        var rows = ItemStockMath.StockRows(
        [
            Row("b-02", 1m, 0m, 1m),
            Row("SUPPLIER", -5m, 0m, 0m, "Outside"),
            Row("A-03", 2m, 0m, 2m),
            Row("RECEIVING", 30m, 0m, 30m, "Receiving")
        ]);

        rows.Select(x => x.Location).Should().Equal("A-03", "RECEIVING", "b-02");
        ItemStockMath.IsAwaitingPutaway(rows[1]).Should().BeTrue();
        ItemStockMath.IsAwaitingPutaway(rows[0]).Should().BeFalse();
    }

    [Fact]
    public void StockSummary_SumsRows_CountsDistinctLocations_AndTakesLatestUpdate()
    {
        var latest = new DateTime(2026, 11, 25, 8, 42, 0, DateTimeKind.Utc);
        var summary = ItemStockSummary.From(
        [
            Row("A-03-2", 220m, 60m, 160m),
            Row("A-03-3", 150m, 26m, 124m) with { LastUpdated = latest },
            Row("a-03-3", 30m, 0m, 30m),
            Row("GAM-02", 12.5m, 0m, 12.5m)
        ]);

        summary.Physical.Should().Be(412.5m);
        summary.Reserved.Should().Be(86m);
        summary.Available.Should().Be(326.5m);
        summary.LocationCount.Should().Be(3);
        summary.LastUpdated.Should().Be(latest);

        var empty = ItemStockSummary.From(Array.Empty<AvailableStockItemDto>());
        empty.Physical.Should().Be(0m);
        empty.LastUpdated.Should().BeNull();
    }

    [Fact]
    public void Bar_ScaleIsPhysicalPlusIncoming_WithRoundedEnds_AndMinMarker()
    {
        var bar = ItemStockMath.Bar(physical: 412.5m, reserved: 86m, available: 326.5m, incoming: 500m, minQty: 150m);

        bar.Segments.Select(x => x.Kind).Should().Equal(ItemStockMath.ReservedSegment, ItemStockMath.AvailableSegment, ItemStockMath.IncomingSegment);
        bar.Segments.Select(x => x.Percent).Should().Equal(9.42m, 35.78m, 54.79m);
        bar.Segments[0].RoundLeft.Should().BeTrue();
        bar.Segments[0].RoundRight.Should().BeFalse();
        bar.Segments[2].RoundRight.Should().BeTrue();
        bar.Segments[0].CssWidth.Should().Be("9.42%");
        bar.MinMarkerPercent.Should().Be(16.44m);
        bar.MinMarkerCss.Should().Be("16.44%");
    }

    [Fact]
    public void Bar_SkipsEmptySegments_ClampsMarker_AndIsEmptyWithoutStock()
    {
        var noIncoming = ItemStockMath.Bar(100m, 0m, 100m, 0m, 250m);
        noIncoming.Segments.Should().ContainSingle();
        noIncoming.Segments[0].RoundLeft.Should().BeTrue();
        noIncoming.Segments[0].RoundRight.Should().BeTrue();
        noIncoming.MinMarkerPercent.Should().Be(100m);

        var withoutMin = ItemStockMath.Bar(100m, 10m, 90m, 0m, null);
        withoutMin.MinMarkerPercent.Should().BeNull();
        withoutMin.MinMarkerCss.Should().BeNull();

        var empty = ItemStockMath.Bar(0m, 0m, 0m, 0m, 150m);
        empty.Segments.Should().BeEmpty();
        empty.MinMarkerPercent.Should().BeNull();
    }

    [Theory]
    [InlineData(null, 10d, MinQtyState.None)]
    [InlineData(150d, 326.5d, MinQtyState.Above)]
    [InlineData(150d, 150d, MinQtyState.Above)]
    [InlineData(150d, 149.9d, MinQtyState.Below)]
    public void MinState_ComparesAvailableWithMinimum(double? min, double available, MinQtyState expected)
    {
        ItemStockMath.MinState((decimal?)min, (decimal)available).Should().Be(expected);
    }

    [Fact]
    public void MarginPercent_IsBaseMinusWacOverBase_OrNull()
    {
        ItemStockMath.MarginPercent(4.80m, 2.04m).Should().Be(57.5m);
        ItemStockMath.MarginPercent(null, 2.04m).Should().BeNull();
        ItemStockMath.MarginPercent(4.80m, null).Should().BeNull();
        ItemStockMath.MarginPercent(0m, 1m).Should().BeNull();
        ItemStockMath.MarginPercent(2m, 3m).Should().Be(-50m);
    }

    [Fact]
    public void Incoming_SumsOpenQuantity_AndFindsNearestEta()
    {
        var summary = ItemIncomingSummary.From(
        [
            new ItemIncomingRowDto { OpenQty = 300m, ExpectedDate = new DateOnly(2026, 12, 17) },
            new ItemIncomingRowDto { OpenQty = 200m, ExpectedDate = new DateOnly(2026, 12, 3) },
            new ItemIncomingRowDto { OpenQty = 5m }
        ]);

        summary.Total.Should().Be(505m);
        summary.NearestEta.Should().Be(new DateOnly(2026, 12, 3));
        ItemIncomingSummary.From(Array.Empty<ItemIncomingRowDto>()).NearestEta.Should().BeNull();
        ItemStockMath.DaysUntil(new DateOnly(2026, 12, 3), new DateOnly(2026, 11, 25)).Should().Be(8);
    }

    [Fact]
    public void PrimaryStorageLocation_PrefersStorageWithMostAvailable()
    {
        var rows = new[]
        {
            Row("RECEIVING", 300m, 0m, 300m, "Receiving"),
            Row("A-03-3", 150m, 26m, 124m),
            Row("A-03-2", 220m, 60m, 160m),
            Row("X-01", 1m, 0m, 1m, null)
        };

        ItemStockMath.PrimaryStorageLocation(rows).Should().Be("A-03-2");
        ItemStockMath.PrimaryStorageLocation([Row("RECEIVING", 1m, 0m, 1m, "Receiving")]).Should().BeNull();
    }

    [Fact]
    public void Timeline_SignsMovements_AndRunsTheBalanceBackFromTheCurrentTotal()
    {
        var timeline = ItemMovementTimeline.Build(SampleMovements(), currentTotal: 412.5m);

        timeline.Select(x => x.Kind).Should().Equal(
            MovementKind.Issue,
            MovementKind.Adjustment,
            MovementKind.Transfer,
            MovementKind.Receipt,
            MovementKind.Opening);
        timeline.Select(x => x.Delta).Should().Equal(-18m, -2m, 0m, 300m, 132.5m);
        timeline.Select(x => x.Balance).Should().Equal(412.5m, 430.5m, 432.5m, 432.5m, 132.5m);
        timeline[2].Route.Should().Be("A-03-2 → GAM-02");
        timeline[3].Route.Should().Be("RECEIVING");
        timeline[0].Route.Should().Be("A-03-3");
    }

    [Fact]
    public void Timeline_WithoutCurrentTotal_HasNoBalance()
    {
        ItemMovementTimeline.Build(SampleMovements(), null).Should().OnlyContain(x => x.Balance == null);
    }

    [Fact]
    public void Timeline_Filters_CountByKind()
    {
        var timeline = ItemMovementTimeline.Build(SampleMovements(), 412.5m);

        ItemMovementTimeline.Count(timeline, MovementFilter.All).Should().Be(5);
        ItemMovementTimeline.Count(timeline, MovementFilter.Receipts).Should().Be(2);
        ItemMovementTimeline.Count(timeline, MovementFilter.Issues).Should().Be(1);
        ItemMovementTimeline.Count(timeline, MovementFilter.Transfers).Should().Be(1);
        ItemMovementTimeline.Count(timeline, MovementFilter.Adjustments).Should().Be(1);
        ItemMovementTimeline.Filters.Should().HaveCount(5);
    }

    [Fact]
    public void Timeline_Display_QuantityColourChipDocumentAndNote()
    {
        var timeline = ItemMovementTimeline.Build(SampleMovements(), 412.5m);

        ItemMovementTimeline.Quantity(timeline[0], Lt).Should().Be("−18,0");
        ItemMovementTimeline.Quantity(timeline[2], Lt).Should().Be("12,5");
        ItemMovementTimeline.Quantity(timeline[3], Lt).Should().Be("+300,0");
        ItemMovementTimeline.QtyClass(timeline[0].Delta).Should().Be("ic-qty--out");
        ItemMovementTimeline.QtyClass(timeline[2].Delta).Should().Be("ic-qty--move");
        ItemMovementTimeline.QtyClass(timeline[3].Delta).Should().Be("ic-qty--in");
        ItemMovementTimeline.ChipClass(MovementKind.Receipt).Should().Be("chip chip--ok");
        ItemMovementTimeline.ChipClass(MovementKind.Opening).Should().Be("chip chip--delivered");
        ItemMovementTimeline.DotClass(MovementKind.Transfer).Should().Be("ic-dot--slate");
        ItemMovementTimeline.DocumentHref(timeline[3].Source).Should().Be("inbound/shipments/14");
        ItemMovementTimeline.DocumentHref(timeline[2].Source).Should().BeNull();
        ItemMovementTimeline.DocumentLabel(timeline[1].Source).Should().Be("7F0C1B2A");
        ItemMovementTimeline.DocumentLabel(timeline[2].Source).Should().Be("PRK-2611-052");
        ItemMovementTimeline.Note(timeline[1]).Should().Be("DAMAGE");
        ItemMovementTimeline.Note(timeline[0]).Should().BeNull();
    }

    [Theory]
    [InlineData("Dispatch", "SHIPPING", "CUSTOMER", MovementKind.Issue)]
    [InlineData("Other", "SUPPLIER", "A-01", MovementKind.Receipt)]
    [InlineData("Other", "A-01", "SCRAP", MovementKind.Issue)]
    [InlineData("Other", "A-01", "A-02", MovementKind.Transfer)]
    public void Timeline_Classify_KnownTypesAndFallbackByDelta(string type, string from, string to, MovementKind expected)
    {
        var movement = Movement(T0, type, from, to, 1m);

        ItemMovementTimeline.Classify(movement, ItemMovementTimeline.Delta(movement)).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, ItemCardTabs.Locations)]
    [InlineData("", ItemCardTabs.Locations)]
    [InlineData("JUDEJIMAI", ItemCardTabs.Movements)]
    [InlineData(" kainos ", ItemCardTabs.Prices)]
    [InlineData("unknown", ItemCardTabs.Locations)]
    public void Tabs_ParseQueryValue(string? query, string expected)
    {
        ItemCardTabs.Parse(query).Should().Be(expected);
    }

    [Fact]
    public void Tabs_DefaultTabHasNoQuery_AndEveryTabHasALabel()
    {
        ItemCardTabs.ToQuery(ItemCardTabs.Locations).Should().BeNull();
        ItemCardTabs.ToQuery(ItemCardTabs.Data).Should().Be("duomenys");
        ItemCardTabs.All.Select(ItemCardTabs.LabelKey).Should().OnlyHaveUniqueItems();
        ItemCardTabs.LabelKey(ItemCardTabs.Prices).Should().Be("AdminItemDetail.Tab.PricesSuppliers");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("system", true)]
    [InlineData("a3fc4742704af1d58daceee7223ae665614b4f314f75461be5bbdcb84c8fdb9e", true)]
    [InlineData("6f1d4b6e-8c35-4c33-9d4b-0a0a0a0a0a0a", false)]
    [InlineData("D. Bykovas", false)]
    public void Text_SystemActors(string? actor, bool expected)
    {
        ItemCardText.IsSystemActor(actor).Should().Be(expected);
    }

    [Fact]
    public void Text_Actor_ShowsSystemForHashes_NameAsIs_AndShortTailForGuids()
    {
        string Unknown(string tail) => "user " + tail;

        ItemCardText.Actor("a3fc4742704af1d58daceee7223ae665614b4f314f75461be5bbdcb84c8fdb9e", "Sistema", Unknown).Should().Be("Sistema");
        ItemCardText.Actor(" D. Bykovas ", "Sistema", Unknown).Should().Be("D. Bykovas");
        ItemCardText.Actor("6f1d4b6e-8c35-4c33-9d4b-0a0a0a0a0a0a", "Sistema", Unknown).Should().Be("user 0a0a0a");
    }

    [Fact]
    public void Text_ChipsPluralsAndRoles()
    {
        ItemCardText.ReservationChipClass("PICKING").Should().Be("chip chip--low");
        ItemCardText.ReservationChipClass("Allocated").Should().Be("chip chip--reserved");
        ItemCardText.ReservationChipClass("PendingStock").Should().Be("chip chip--reserved");
        ItemCardText.ReservationChipClass(null).Should().Be("chip chip--locked");
        ItemCardText.IncomingChipClass("Partial").Should().Be("chip chip--reserved");
        ItemCardText.IncomingChipClass("Draft").Should().Be("chip chip--locked");
        ItemCardText.StatusChipClass("Active").Should().Be("chip chip--ok");
        ItemCardText.PluralKey("AdminItemDetail.Locations", 1, "lt").Should().Be("AdminItemDetail.Locations.One");
        ItemCardText.PluralKey("AdminItemDetail.Locations", 4, "lt").Should().Be("AdminItemDetail.Locations.Few");
        ItemCardText.PluralKey("AdminItemDetail.Locations", 10, "lt").Should().Be("AdminItemDetail.Locations.Many");
        ItemCardText.PluralKey("AdminItemDetail.Locations", 4, "en").Should().Be("AdminItemDetail.Locations.Other");
        ItemCardText.RoleKey(" Receiving ").Should().Be("AdminItemDetail.Role.Receiving");
        ItemCardText.RoleKey(null).Should().BeNull();
    }

    [Fact]
    public void Requests_CopyTheItem_AndChangeOnlyTheEditedField()
    {
        var item = new ItemDetailsDto
        {
            Id = 10275,
            InternalSKU = "BAV-RAL9006",
            Name = " Elegance atsvaras ",
            Description = " ",
            CategoryId = 1,
            BaseUoM = "M",
            BasePrice = 4.80m,
            PurchasePrice = 2.01m,
            MinQty = 150m,
            Status = "Active",
            ItemType = "Stock",
            CostType = "Freight",
            Tags = ["roletai"],
            CommodityCode = "76042990",
            OriginCountry = "pl",
            SupplementaryUnit = null,
            SupplementaryUnitFactor = 3m
        };

        var request = ItemCardRequests.FromItem(item);
        request.InternalSKU.Should().BeNull();
        request.Name.Should().Be("Elegance atsvaras");
        request.Description.Should().BeNull();
        request.CostType.Should().BeNull();
        request.OriginCountry.Should().Be("PL");
        request.SupplementaryUnitFactor.Should().BeNull();
        request.Tags.Should().Equal("roletai");
        request.CommodityCode.Should().Be("76042990");

        var priced = ItemCardRequests.WithBasePrice(item, 5.10m);
        priced.BasePrice.Should().Be(5.10m);
        priced.MinQty.Should().Be(150m);

        var minimum = ItemCardRequests.WithMinQty(item, null);
        minimum.MinQty.Should().BeNull();
        minimum.BasePrice.Should().Be(4.80m);
        minimum.PurchasePrice.Should().Be(2.01m);
    }

    [Theory]
    [InlineData(null, "A-04-1", 2, 1d, 10d, MoveProblem.SourceMissing)]
    [InlineData(1, " ", null, 1d, 10d, MoveProblem.TargetMissing)]
    [InlineData(1, "ZZ", null, 1d, 10d, MoveProblem.TargetUnknown)]
    [InlineData(1, "A-03-2", 1, 1d, 10d, MoveProblem.SameLocation)]
    [InlineData(1, "A-04-1", 2, null, 10d, MoveProblem.QtyMissing)]
    [InlineData(1, "A-04-1", 2, 0d, 10d, MoveProblem.QtyMissing)]
    [InlineData(1, "A-04-1", 2, 10.5d, 10d, MoveProblem.QtyTooHigh)]
    [InlineData(1, "A-04-1", 2, 10d, 10d, MoveProblem.None)]
    public void Dialog_ValidateMove(int? source, string? target, int? targetId, double? qty, double available, MoveProblem expected)
    {
        StockDialogRules.ValidateMove(source, target, targetId, (decimal?)qty, (decimal)available).Should().Be(expected);
    }

    [Fact]
    public void Dialog_MatchLocation_ByCodeThenBarcode()
    {
        var locations = new[]
        {
            new AdminLocationDto { Id = 1, Code = "A-04-1", Barcode = "LOC000041" },
            new AdminLocationDto { Id = 2, Code = "A-04-10", Barcode = "LOC000410" }
        };

        StockDialogRules.MatchLocation(locations, " a-04-1 ")!.Id.Should().Be(1);
        StockDialogRules.MatchLocation(locations, "loc000410")!.Id.Should().Be(2);
        StockDialogRules.MatchLocation(locations, "A-04").Should().BeNull();
        StockDialogRules.MatchLocation(locations, null).Should().BeNull();
    }

    [Fact]
    public void Dialog_Difference_IsCountedMinusSystem()
    {
        StockDialogRules.Difference(148m, 150m).Should().Be(-2m);
        StockDialogRules.Difference(null, 150m).Should().BeNull();
        StockDialogRules.CanExecuteAfterSubmit("APPROVED").Should().BeTrue();
        StockDialogRules.CanExecuteAfterSubmit("PENDING_APPROVAL").Should().BeFalse();
    }

    [Fact]
    public void Dialog_AdjustReasons_MapTheFourReasonsToExistingCodes()
    {
        var options = StockDialogRules.AdjustReasons(
        [
            Reason("inventory", "Inventory Correction"),
            Reason("DAMAGE", "Damage"),
            Reason("SYSTEM_ERROR", "System Error"),
            Reason("THEFT", "Theft"),
            Reason("FOUND", "Found", active: false)
        ]);

        options.Select(x => x.LabelKey).Should().Equal(
            "AdminItemDetail.AdjustReason.Count",
            "AdminItemDetail.AdjustReason.Damaged",
            "AdminItemDetail.AdjustReason.Found",
            "AdminItemDetail.AdjustReason.EntryError");
        options.Select(x => x.Code).Should().Equal("INVENTORY", "DAMAGE", "INVENTORY", "SYSTEM_ERROR");
    }

    [Fact]
    public void Dialog_AdjustReasons_FallBackToEveryActiveCode()
    {
        var options = StockDialogRules.AdjustReasons([Reason("THEFT", "Theft"), Reason("EXPIRED", "")]);

        options.Select(x => x.Code).Should().Equal("EXPIRED", "THEFT");
        options.Should().OnlyContain(x => x.LabelKey == null);
        options[0].Label.Should().Be("EXPIRED");
        options[1].Label.Should().Be("Theft");
    }

    [Fact]
    public async Task PanelState_LoadsValue_KeepsItOnError_AndResets()
    {
        var state = new PanelState<int>();

        await state.LoadAsync(() => Task.FromResult(7));
        state.HasValue.Should().BeTrue();
        state.Value.Should().Be(7);
        state.IsLoading.Should().BeFalse();

        await state.LoadAsync(() => Task.FromException<int>(new ApiException(new ProblemDetailsModel { TraceId = "t-1" }, 500)));
        state.Error.Should().NotBeNull();
        state.Error!.TraceId.Should().Be("t-1");
        state.Value.Should().Be(7);

        state.ClearError();
        state.Error.Should().BeNull();

        state.Reset();
        state.HasValue.Should().BeFalse();
        state.Value.Should().Be(0);
    }

    [Fact]
    public void Resources_HaveEveryGeneratedKey_InBothLanguages_AndTheSameItemCardKeys()
    {
        var generated = Enum.GetNames<MovementKind>().Select(x => "AdminItemDetail.MovementKind." + x)
            .Concat(Enum.GetNames<MovementFilter>().Select(x => "AdminItemDetail.MovementFilter." + x))
            .Concat(Enum.GetNames<MoveProblem>().Select(x => "AdminItemDetail.MoveDialog.Problem." + x))
            .Concat(ItemCardTabs.All.Select(ItemCardTabs.LabelKey))
            .Concat(new[] { "One", "Few", "Many", "Other" }.SelectMany(form => new[]
            {
                "AdminItemDetail.Locations." + form,
                "AdminItemDetail.Orders." + form,
                "AdminItemDetail.PhysicalInLocations." + form
            }))
            .Concat(new[] { "Receiving", "QcHold", "Quarantine", "Shipping", "Transit", "Storage" }.Select(x => ItemCardText.RoleKey(x)!))
            .ToList();

        var en = ResourceNames("WarehouseResources.resx");
        var lt = ResourceNames("WarehouseResources.lt.resx");

        generated.Where(x => !en.Contains(x)).Should().BeEmpty();
        generated.Where(x => !lt.Contains(x)).Should().BeEmpty();
        en.Where(x => x.StartsWith("AdminItemDetail.", StringComparison.Ordinal))
            .Should().BeEquivalentTo(lt.Where(x => x.StartsWith("AdminItemDetail.", StringComparison.Ordinal)));
    }

    private static HashSet<string> ResourceNames(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "i18n", "README.md")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root holds docs/i18n/README.md");
        var path = Path.Combine(dir!.FullName, "src", "Modules", "Warehouse", "LKvitai.MES.Modules.Warehouse.WebUI", "Resources", file);
        return System.Xml.Linq.XDocument.Load(path).Root!.Elements("data")
            .Select(x => x.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static AvailableStockItemDto Row(string location, decimal physical, decimal reserved, decimal available, string? role = "Storage")
        => new()
        {
            WarehouseId = "VLN-1",
            LocationId = location.Length,
            Location = location,
            LocationRole = role,
            PhysicalQty = physical,
            ReservedQty = reserved,
            AvailableQty = available,
            LastUpdated = new DateTime(2026, 11, 24, 16, 5, 0, DateTimeKind.Utc)
        };

    private static ReasonCodeDto Reason(string code, string name, bool active = true)
        => new() { Code = code, Name = name, Active = active, Category = "ADJUSTMENT" };

    private static StockMovementRowDto Movement(DateTimeOffset at, string type, string? from, string? to, decimal qty, string reference = "", string? reason = null)
        => new()
        {
            Timestamp = at,
            MovementType = type,
            ItemId = 10275,
            FromLocationCode = from,
            ToLocationCode = to,
            Qty = qty,
            Reference = reference,
            Reason = reason,
            Operator = "D. Bykovas"
        };

    private static IReadOnlyList<StockMovementRowDto> SampleMovements() =>
    [
        Movement(T0, "Adjust", "ADJUSTMENT", "A-03-2", 132.5m, Guid.NewGuid().ToString(), ItemMovementTimeline.OpeningBalanceReason),
        Movement(T0.AddHours(1), "Receive", "SUPPLIER", "RECEIVING", 300m, "ISH-14", "Goods receipt"),
        Movement(T0.AddHours(3), "Transfer", "A-03-2", "GAM-02", 12.5m, "PRK-2611-052"),
        Movement(T0.AddHours(5), "Adjust", "A-03-3", "ADJUSTMENT", 2m, "7f0c1b2a-0000-4000-8000-000000000001", "DAMAGE"),
        Movement(T0.AddHours(20), "Pick", "A-03-3", string.Empty, 18m, "KVT-261124-031")
    ];
}
