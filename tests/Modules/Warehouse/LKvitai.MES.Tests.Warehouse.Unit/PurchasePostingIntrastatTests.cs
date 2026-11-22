using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Posting checks of an EU acquisition: origin country, supplementary unit and Intrastat code lists.</summary>
public sealed class PurchasePostingIntrastatTests
{
    private static List<PostingProblem> Validate(Action<InboundShipment, Item>? change = null)
    {
        var item = new Item
        {
            Id = 10, InternalSKU = "SKU", ItemType = ItemType.Stock, CommodityCode = "84713000", NetWeightKg = 0.5m, OriginCountry = "CN"
        };
        var shipment = new InboundShipment
        {
            Id = 1, ReferenceNumber = "IN-1", SupplierId = 1, InvoiceNumber = "INV 1", InvoiceDate = new DateOnly(2026, 11, 20),
            Currency = "EUR", NetAmount = 1200m, VatAmount = 0m, VatTreatment = VatTreatment.EuAcquisition,
            DeliveryTerms = "DAP", TransportMode = "3", DispatchCountry = "PL", TransactionNature = "11",
            Lines = { new InboundShipmentLine { Id = 1, ItemId = 10, Item = item, ExpectedQty = 100m, UnitPrice = 12m } }
        };
        change?.Invoke(shipment, item);
        return PurchasePostingValidator.Validate(new PurchasePostingInput
        {
            Shipment = shipment,
            Supplier = new Supplier { AgnumClientId = 5, Kind = SupplierKind.Eu },
            CostTypes = new Dictionary<string, AdditionalCostType>(),
            ItemProfiles = new Dictionary<int, int?> { [10] = 1 },
            HasPostingRule = (_, _) => true,
            Rate = new PostingRate(FxRateStatus.Ok, 1m, new DateOnly(2026, 11, 20), "EUR", null)
        });
    }

    [Fact]
    public void CompleteEuAcquisition_HasNoProblems() => Validate().Should().BeEmpty();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C")]
    [InlineData("CHN")]
    [InlineData("C1")]
    public void MissingOrMalformedOriginCountry_Blocks(string? origin)
        => Validate((_, i) => i.OriginCountry = origin).Should().ContainSingle(p => p.Code == "IntrastatOriginCountryMissing");

    [Fact]
    public void OriginCountryIsNotRequired_ForDomesticPurchase()
        => Validate((s, i) => { s.VatTreatment = VatTreatment.Domestic; i.OriginCountry = null; })
            .Should().NotContain(p => p.Code == "IntrastatOriginCountryMissing");

    [Theory]
    [InlineData("11"), InlineData("12"), InlineData("21"), InlineData("22"), InlineData("23"), InlineData("31"), InlineData("32")]
    [InlineData("33"), InlineData("34"), InlineData("41"), InlineData("42"), InlineData("51"), InlineData("52"), InlineData("71")]
    [InlineData("72"), InlineData("80"), InlineData("91"), InlineData("99")]
    public void ValidNatureOfTransaction_Passes(string code)
        => Validate((s, _) => s.TransactionNature = code).Should().BeEmpty();

    [Theory]
    [InlineData("10"), InlineData("13"), InlineData("1"), InlineData("111"), InlineData("ab")]
    public void InvalidNatureOfTransaction_Blocks(string code)
        => Validate((s, _) => s.TransactionNature = code).Should().ContainSingle(p => p.Code == "IntrastatCodeInvalid");

    [Theory]
    [InlineData("1"), InlineData("2"), InlineData("3"), InlineData("4"), InlineData("5"), InlineData("7"), InlineData("8"), InlineData("9")]
    public void ValidTransportMode_Passes(string code)
        => Validate((s, _) => s.TransportMode = code).Should().BeEmpty();

    [Theory]
    [InlineData("10", "1"), InlineData("30", "3"), InlineData("90", "9"), InlineData(" 40 ", "4"), InlineData("3", "3")]
    public void LegacyTwoDigitTransportMode_IsNormalisedNotBlocked(string stored, string expected)
    {
        Validate((s, _) => s.TransportMode = stored).Should().BeEmpty();
        IntrastatCodes.NormalizeTransportMode(stored).Should().Be(expected);
    }

    [Theory]
    [InlineData("0"), InlineData("6"), InlineData("60"), InlineData("00"), InlineData("31"), InlineData("x")]
    public void InvalidTransportMode_Blocks(string code)
        => Validate((s, _) => s.TransportMode = code).Should().ContainSingle(p => p.Code == "IntrastatCodeInvalid");

    [Theory]
    [InlineData("EXW"), InlineData("FCA"), InlineData("CPT"), InlineData("CIP"), InlineData("DAP"), InlineData("DPU")]
    [InlineData("DDP"), InlineData("FAS"), InlineData("FOB"), InlineData("CFR"), InlineData("CIF"), InlineData("dap")]
    public void Incoterms2020_Pass(string code)
        => Validate((s, _) => s.DeliveryTerms = code).Should().BeEmpty();

    [Theory]
    [InlineData("DAT"), InlineData("DDU"), InlineData("ABC"), InlineData("XX")]
    public void CodesOutsideIncoterms2020_Block(string code)
        => Validate((s, _) => s.DeliveryTerms = code).Should().ContainSingle(p => p.Code == "IntrastatCodeInvalid");

    [Fact]
    public void EmptyHeaderCode_GivesTheRequiredMessage_NotTheInvalidOne()
    {
        var problems = Validate((s, _) => s.TransportMode = null);

        problems.Should().ContainSingle(p => p.Code == "IntrastatHeaderRequired");
        problems.Should().NotContain(p => p.Code == "IntrastatCodeInvalid");
    }

    [Fact]
    public void SupplementaryUnit_NeedsAFactorAndTheOtherWayRound()
    {
        Validate((_, i) => { i.SupplementaryUnit = "m2"; i.SupplementaryUnitFactor = 1.2m; }).Should().BeEmpty();
        Validate((_, i) => i.SupplementaryUnit = "m2").Should().ContainSingle(p => p.Code == "IntrastatSupplementaryUnitInvalid");
        Validate((_, i) => i.SupplementaryUnitFactor = 1.2m).Should().ContainSingle(p => p.Code == "IntrastatSupplementaryUnitInvalid");
        Validate((_, i) => { i.SupplementaryUnit = "m2"; i.SupplementaryUnitFactor = 0m; })
            .Should().ContainSingle(p => p.Code == "IntrastatSupplementaryUnitInvalid");
    }
}
