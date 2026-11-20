using System.Net;
using System.Text;
using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Blueprint stage W2: purchase document, currency and additional costs.</summary>
public sealed class PurchaseDocumentW2Tests
{
    private static readonly StockValueDocument Doc = new("InboundShipment", "1", "IN-1", "test");

    private static WarehouseDbContext NewDb()
        => new(new DbContextOptionsBuilder<WarehouseDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // ---- value ledger scenarios ----

    private static ItemValuation Stock(decimal qty, decimal value)
    {
        var v = new ItemValuation();
        v.Apply(v.Inflow(10, StockValueKinds.Receipt, qty, value, Doc));
        return v;
    }

    private static IReadOnlyList<StockValueChanged> Post(ItemValuation v, decimal received, decimal difference)
    {
        var events = StockValueWriter.BuildMany(v, 10, StockValueRequest.ForTrueUp("SKU", received, difference, Doc));
        foreach (var e in events) v.Apply(e);
        return events;
    }

    [Fact]
    public void TwoReceiptsAt10And12_PostedAtInvoice1200_WritesTrueUp120()
    {
        // 60 pcs at 10 and 40 pcs at 12 = preliminary 1 080; the invoice is 100 x 12 = 1 200.
        var v = new ItemValuation();
        v.Apply(v.Inflow(10, StockValueKinds.Receipt, 60m, PurchaseMath.ToEur(60m * 10m, 1m), Doc));
        v.Apply(v.Inflow(10, StockValueKinds.Receipt, 40m, PurchaseMath.ToEur(40m * 12m, 1m), Doc));
        v.ValueEur.Should().Be(1080m);

        var final = PurchaseMath.Compute(1200m, 0m, 1m,
            [new PurchaseLineInput(1, 100m, 12m, false)], []).LineFinalEur[1];
        var events = Post(v, 100m, PurchaseMath.ReceivedPortion(final, 100m, 100m) - 1080m);

        events.Should().ContainSingle().Which.Should().Match<StockValueChanged>(
            e => e.Kind == StockValueKinds.ReceiptTrueUp && e.ValueDeltaEur == 120m);
        v.ValueEur.Should().Be(1200m);
    }

    [Fact]
    public void Received100At10_Issued80_PostedAt11_SplitsTrueUpBetweenStockAndPriceDifference()
    {
        var v = Stock(100m, 1000m);
        v.Apply(v.Outflow(10, StockValueKinds.Issue, 80m, Doc)!);
        v.ValuedQty.Should().Be(20m);
        v.ValueEur.Should().Be(200m);

        var events = Post(v, 100m, 1100m - 1000m);

        events.Should().HaveCount(2);
        events[0].Kind.Should().Be(StockValueKinds.ReceiptTrueUp);
        events[0].ValueDeltaEur.Should().Be(20m);
        events[1].Kind.Should().Be(StockValueKinds.PriceDifference);
        events[1].OffLedgerEur.Should().Be(80m);
        events[1].ValueDeltaEur.Should().Be(0m, "the price difference is not stock value");
        v.ValueEur.Should().Be(220m);
    }

    [Fact]
    public void TrueUp_NeverMakesStockValueNegative()
    {
        var v = Stock(100m, 1000m);
        v.Apply(v.Outflow(10, StockValueKinds.Issue, 10m, Doc)!); // 90 pcs, 900
        var events = Post(v, 100m, -5000m);
        v.ValueEur.Should().BeGreaterThanOrEqualTo(0m);
        events.Sum(e => e.ValueDeltaEur + e.OffLedgerEur).Should().Be(-5000m);
    }

    // ---- currency ----

    [Fact]
    public void Pln100000At4_2650_GivesRate0_23446659_AndNetEur23446_66()
    {
        var totals = PurchaseMath.Compute(100000m, 0m, 4.2650m,
            [new PurchaseLineInput(1, 1m, 100000m, false)], []);
        totals.RateEurPerUnit.Should().Be(0.23446659m);
        totals.NetEur.Should().Be(23446.66m);
        totals.LineFinalEur[1].Should().Be(23446.66m);
    }

    [Fact]
    public void RoundingIsAwayFromZero_AndPartsAlwaysAddUp()
    {
        PurchaseMath.Money(0.005m).Should().Be(0.01m);
        var parts = PurchaseMath.Allocate(100m, [1m, 1m, 1m]);
        parts.Sum().Should().Be(100m);
        parts.Max().Should().Be(33.34m, "the remainder goes to the largest part");
    }

    // ---- additional costs ----

    [Fact]
    public void FreightExpense_DoesNotChangeStockValue_AndFreightInCostIsAllocatedByValue()
    {
        PurchaseLineInput[] lines = [new(1, 10m, 100m, false), new(2, 10m, 300m, false)]; // 1 000 + 3 000
        var expense = PurchaseMath.Compute(4100m, 0m, 1m, lines, [new PurchaseCostInput(7, 100m, false)]);
        expense.ExpensesEur.Should().Be(100m);
        expense.LineFinalEur[1].Should().Be(1000m);
        expense.LineFinalEur[2].Should().Be(3000m);

        var inCost = PurchaseMath.Compute(4100m, 0m, 1m, lines, [new PurchaseCostInput(7, 100m, true)]);
        inCost.LineFinalEur[1].Should().Be(1025m);
        inCost.LineFinalEur[2].Should().Be(3075m);
        inCost.LineFinalEur.Values.Sum().Should().Be(4100m);
    }

    // ---- posting checks ----

    private static PurchasePostingInput Input(
        InboundShipment shipment, Supplier? supplier = null, PostingRate? rate = null, bool taken = false,
        Dictionary<int, int?>? profiles = null, bool rule = true)
        => new()
        {
            Shipment = shipment,
            Supplier = supplier ?? new Supplier { AgnumClientId = 5, Kind = SupplierKind.Domestic },
            CostTypes = new Dictionary<string, AdditionalCostType>(StringComparer.OrdinalIgnoreCase)
            {
                ["Freight"] = new() { Code = "Freight", IncludedInCost = true },
                ["Courier"] = new() { Code = "Courier", IncludedInCost = false, ExpenseAccount = "6001" }
            },
            ItemProfiles = profiles ?? shipment.Lines.ToDictionary(l => l.ItemId, _ => (int?)1),
            HasPostingRule = (_, _) => rule,
            Rate = rate ?? new PostingRate(FxRateStatus.Ok, 1m, new DateOnly(2026, 11, 20), "EUR", null),
            InvoiceNumberTaken = taken
        };

    private static InboundShipment Shipment(decimal net = 1200m, decimal price = 12m)
    {
        var item = new Item { Id = 10, InternalSKU = "SKU", ItemType = ItemType.Stock };
        return new InboundShipment
        {
            Id = 1, ReferenceNumber = "IN-1", SupplierId = 1, InvoiceNumber = "INV 1", InvoiceDate = new DateOnly(2026, 11, 20),
            Currency = "EUR", NetAmount = net, VatAmount = net * 0.21m,
            Lines = { new InboundShipmentLine { Id = 1, ItemId = 10, Item = item, ExpectedQty = 100m, ReceivedQty = 100m, UnitPrice = price, BookedValueEur = 1080m } }
        };
    }

    [Fact]
    public void Post_IsBlocked_WhenLinesAndHeaderDifferBy001()
    {
        PurchasePostingValidator.Validate(Input(Shipment(1200m))).Should().BeEmpty();

        var problems = PurchasePostingValidator.Validate(Input(Shipment(1200.01m)));
        problems.Should().ContainSingle(p => p.Code == "LinesTotalMismatch");

        var withCost = Shipment(1200m);
        withCost.AdditionalCosts.Add(new InboundShipmentAdditionalCost { Id = 1, CostType = "Freight", Amount = 100m });
        PurchasePostingValidator.Validate(Input(withCost)).Should().ContainSingle(p => p.Code == "LinesTotalMismatch");
        withCost.NetAmount = 1300m;
        PurchasePostingValidator.Validate(Input(withCost)).Should().BeEmpty();
    }

    [Fact]
    public void Post_Checks_NumberDateSupplierRateAccountsAndRule()
    {
        var s = Shipment();
        s.InvoiceNumber = null;
        s.InvoiceDate = null;
        PurchasePostingValidator.Validate(Input(s)).Select(p => p.Code)
            .Should().Contain(["InvoiceNumberRequired", "InvoiceDateRequired"]);

        PurchasePostingValidator.Validate(Input(Shipment(), new Supplier { AgnumClientId = null, Kind = SupplierKind.Domestic }))
            .Should().Contain(p => p.Code == "SupplierNotPostable");
        var manual = Shipment();
        manual.AccountingRoute = "ManualInAgnum";
        PurchasePostingValidator.Validate(Input(manual, new Supplier { AgnumClientId = null })).Should().BeEmpty();

        PurchasePostingValidator.Validate(Input(Shipment(), taken: true)).Should().Contain(p => p.Code == "InvoiceNumberTaken");
        PurchasePostingValidator.Validate(Input(Shipment(), rate: new PostingRate(FxRateStatus.NotPublishedToday, null, null, null, null)))
            .Should().Contain(p => p.Code == "RateNotPublishedToday");
        PurchasePostingValidator.Validate(Input(Shipment(), rate: new PostingRate(FxRateStatus.Unavailable, null, null, null, null)))
            .Should().Contain(p => p.Code == "RateMissing");
        PurchasePostingValidator.Validate(Input(Shipment(), rule: false)).Should().Contain(p => p.Code == "PostingRuleMissing");

        var mixed = Shipment();
        mixed.Lines.Add(new InboundShipmentLine { Id = 2, ItemId = 11, Item = new Item { Id = 11, InternalSKU = "B" }, ExpectedQty = 1m, UnitPrice = 0m });
        PurchasePostingValidator.Validate(Input(mixed, profiles: new() { [10] = 1, [11] = 2 }))
            .Should().Contain(p => p.Code == "MixedStockAccounts");
    }

    [Fact]
    public void RateSanity_BlocksAJumpOfMoreThan1_5Times_AndManualRateNeedsReason()
    {
        var s = Shipment();
        s.Currency = "PLN";
        var okRate = new PostingRate(FxRateStatus.Ok, 4.2650m, new DateOnly(2026, 11, 20), "BankOfLithuania", null);
        var input = Input(s, rate: okRate);
        PurchasePostingValidator.Validate(new PurchasePostingInput
        {
            Shipment = s, Supplier = input.Supplier, CostTypes = input.CostTypes, ItemProfiles = input.ItemProfiles,
            HasPostingRule = input.HasPostingRule, Rate = okRate, PreviousRateUnitsPerEur = 4.30m
        }).Should().BeEmpty();
        PurchasePostingValidator.Validate(new PurchasePostingInput
        {
            Shipment = s, Supplier = input.Supplier, CostTypes = input.CostTypes, ItemProfiles = input.ItemProfiles,
            HasPostingRule = input.HasPostingRule, Rate = okRate, PreviousRateUnitsPerEur = 2.0m
        }).Should().Contain(p => p.Code == "RateSanity");
        PurchasePostingValidator.Validate(Input(s, rate: okRate with { Source = "Manual", ManualReason = " " }))
            .Should().Contain(p => p.Code == "RateReasonRequired");
    }

    // ---- posting a document end to end (entities + value requests) ----

    [Fact]
    public void Apply_SetsFinalValues_LocksTheDocument_AndWritesTrueUpForReceivedQuantity()
    {
        var s = Shipment();
        var input = Input(s);
        PurchasePostingValidator.Validate(input).Should().BeEmpty();

        var requests = PurchaseDocumentService.Apply(s, input, "tester", 1);

        s.PostingStatus.Should().Be("Posted");
        s.PostedBy.Should().Be("tester");
        s.NetEur.Should().Be(1200m);
        s.GrossEur.Should().Be(1452m);
        s.Lines.Single().FinalValueEur.Should().Be(1200m);
        s.Lines.Single().BookedValueEur.Should().Be(1200m);
        var request = requests.Should().ContainSingle().Subject;
        request.IsTrueUp.Should().BeTrue();
        request.ValueEur.Should().Be(120m);
        PurchasePostingValidator.Validate(input).Should().ContainSingle(p => p.Code == "AlreadyPosted");
    }

    [Fact]
    public void ReceiptAfterPosting_FollowsFinalValue_SoTheLineSumStaysExact()
    {
        // 3 pieces for 100.00: each receipt is the cumulative final portion minus what was booked.
        decimal booked = 0m;
        foreach (var cumulative in new[] { 1m, 2m, 3m })
        {
            var value = PurchaseMath.ReceivedPortion(100m, cumulative, 3m) - booked;
            booked += value;
        }

        booked.Should().Be(100m);
    }

    // ---- Bank of Lithuania rates ----

    private const string Xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <FxRates xmlns="http://www.lb.lt/WebServices/FxRates">
          <FxRate><Tp>EU</Tp><Dt>2026-11-13</Dt><CcyAmt><Ccy>EUR</Ccy><Amt>1</Amt></CcyAmt><CcyAmt><Ccy>PLN</Ccy><Amt>4.2650</Amt></CcyAmt></FxRate>
          <FxRate><Tp>EU</Tp><Dt>2026-11-13</Dt><CcyAmt><Ccy>EUR</Ccy><Amt>1</Amt></CcyAmt><CcyAmt><Ccy>USD</Ccy><Amt>1.1000</Amt></CcyAmt></FxRate>
        </FxRates>
        """;

    private sealed class FakeHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/xml") });
        }
    }

    private static LbFxRateProvider Provider(FakeHandler handler, WarehouseDbContext db, DateOnly today)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://fake.invalid/") }, db,
            new NullLogger<LbFxRateProvider>(), () => today);

    [Fact]
    public async Task FxProvider_ReadsRate_UsesLastPublishedDay_AndCaches()
    {
        await using var db = NewDb();
        var handler = new FakeHandler(Xml);
        var fx = Provider(handler, db, new DateOnly(2026, 11, 20));

        // 2026-11-15 is a Saturday: the answer carries the 13th.
        var result = await fx.GetRateAsync("pln", new DateOnly(2026, 11, 15));
        result.Status.Should().Be(FxRateStatus.Ok);
        result.Quote!.UnitsPerEur.Should().Be(4.2650m);
        result.Quote.RateDate.Should().Be(new DateOnly(2026, 11, 13));
        db.FxRates.Count().Should().Be(2);

        (await fx.GetRateAsync("PLN", new DateOnly(2026, 11, 13))).Quote!.UnitsPerEur.Should().Be(4.2650m);
        handler.Calls.Should().Be(1, "the second answer comes from fx_rates");
        (await fx.GetRateAsync("EUR", new DateOnly(2026, 11, 13))).Quote!.UnitsPerEur.Should().Be(1m);
        (await fx.GetPreviousRateAsync("PLN", new DateOnly(2026, 11, 20))).Should().Be(4.2650m);
    }

    [Fact]
    public async Task FxProvider_BlocksToday_WhenTodaysRateIsNotPublished_AndReportsOutages()
    {
        await using var db = NewDb();
        var today = new DateOnly(2026, 11, 15);
        (await Provider(new FakeHandler(Xml), db, today).GetRateAsync("PLN", today)).Status
            .Should().Be(FxRateStatus.NotPublishedToday);

        await using var db2 = NewDb();
        (await Provider(new FakeHandler("<x/>", HttpStatusCode.InternalServerError), db2, today)
            .GetRateAsync("PLN", new DateOnly(2026, 11, 13))).Status.Should().Be(FxRateStatus.Unavailable);
        await using var db3 = NewDb();
        (await Provider(new FakeHandler("not xml"), db3, today).GetRateAsync("PLN", new DateOnly(2026, 11, 13))).Status
            .Should().Be(FxRateStatus.Unavailable);
    }

    // ---- additional cost types and category guard ----

    private static AccountingConfigService Config(WarehouseDbContext db, params string[] debits)
    {
        var agnum = new Mock<IAgnumReferenceClient>();
        agnum.Setup(a => a.GetSchemesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<AgnumScheme>
        {
            new(1, "S1", debits.Select((d, i) => new AgnumSchemeRow(i + 1, "r", d, "4430")).ToList())
        });
        return new AccountingConfigService(db, agnum.Object, new AdditionalCostExpenseAccountSource(db),
            Options.Create(new AccountingOptions { VatAccount = "24101" }));
    }

    [Fact]
    public async Task CostType_Rules_ExpenseAccountOnlyWhenNotIncluded_AndVatIsForbidden()
    {
        await using var db = NewDb();
        var config = Config(db, "20110");
        var user = "acc";

        await config.Invoking(c => c.SaveCostTypeAsync(new() { Code = "VAT", Name = "VAT", IncludedInCost = true }, user, default))
            .Should().ThrowAsync<AccountingConfigValidationException>();
        await config.Invoking(c => c.SaveCostTypeAsync(new() { Code = "Duty", Name = "Duty", IncludedInCost = true, ExpenseAccount = "6001" }, user, default))
            .Should().ThrowAsync<AccountingConfigValidationException>();
        await config.Invoking(c => c.SaveCostTypeAsync(new() { Code = "Courier", Name = "Courier", IncludedInCost = false }, user, default))
            .Should().ThrowAsync<AccountingConfigValidationException>();

        var saved = await config.SaveCostTypeAsync(new() { Code = "Courier", Name = "Courier", IncludedInCost = false, ExpenseAccount = "6001", IsActive = true }, user, default);
        saved.Id.Should().NotBe(0);
        (await new AdditionalCostExpenseAccountSource(db).GetExpenseAccountsAsync(default)).Should().Equal("6001");
        db.AccountingConfigAudit.Should().ContainSingle(a => a.Entity == "AdditionalCostType");
    }

    [Fact]
    public async Task CostType_WithExpenseAccount_IsRejected_WhenAPostingSchemeHasNoRowForIt()
    {
        await using var db = NewDb();
        db.StockAccountProfiles.Add(new StockAccountProfile { Id = 1, Name = "Main", StockAccount = "20110", AgnumSndId = 1, IsDefault = true });
        db.AgnumPostingRules.Add(new AgnumPostingRule { StockAccountProfileId = 1, VatTreatment = VatTreatment.EuAcquisition, SchemeId = 1 });
        await db.SaveChangesAsync();

        await Config(db, "20110").Invoking(c => c.SaveCostTypeAsync(
                new() { Code = "Courier", Name = "Courier", IncludedInCost = false, ExpenseAccount = "6001", IsActive = true }, "acc", default))
            .Should().ThrowAsync<AccountingConfigValidationException>().WithMessage("*6001*");
        await Config(db, "20110", "6001").SaveCostTypeAsync(
            new() { Code = "Courier", Name = "Courier", IncludedInCost = false, ExpenseAccount = "6001", IsActive = true }, "acc", default);
    }

    [Fact]
    public async Task Category_CannotChange_WhileStockValueIsNotZero()
    {
        await using var db = NewDb();
        db.ItemCategories.AddRange(new ItemCategory { Id = 1, Code = "A", Name = "A" }, new ItemCategory { Id = 2, Code = "B", Name = "B", ParentCategoryId = 1 });
        db.Items.Add(new Item { Id = 10, InternalSKU = "S", Name = "S", CategoryId = 2, BaseUoM = "PCS" });
        db.OnHandValues.Add(new OnHandValue { Id = Guid.NewGuid(), ItemId = 10, ItemSku = "S", ItemName = "S", Qty = 5m, TotalValue = 50m });
        await db.SaveChangesAsync();

        (await StockValueGuard.ItemHasStockValueAsync(db, 10, default)).Should().BeTrue();
        (await StockValueGuard.CategoryTreeHasStockValueAsync(db, 1, default)).Should().BeTrue("a descendant category holds value");

        await Config(db).Invoking(c => c.SetCategoryProfileAsync(1, null, "acc", default))
            .Should().NotThrowAsync("the profile is already empty, so nothing changes");
        db.StockAccountProfiles.Add(new StockAccountProfile { Id = 3, Name = "P", StockAccount = "20110", AgnumSndId = 1 });
        await db.SaveChangesAsync();
        await Config(db).Invoking(c => c.SetCategoryProfileAsync(1, 3, "acc", default))
            .Should().ThrowAsync<AccountingConfigValidationException>().WithMessage("*stock value*");

        db.OnHandValues.Single().TotalValue = 0m;
        db.OnHandValues.Single().Qty = 0m;
        await db.SaveChangesAsync();
        (await StockValueGuard.ItemHasStockValueAsync(db, 10, default)).Should().BeFalse();
    }
}
