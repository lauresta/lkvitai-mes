using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class IntrastatReportTests
{
    private static IntrastatLineInput Line(
        string code = "84713000", string country = "PL", string origin = "CN", string nature = "11",
        string terms = "DAP", string mode = "3", string? unit = null, decimal qty = 1m, decimal? factor = null,
        decimal value = 10m, decimal? weight = 0.5m, decimal? landed = null, bool isReturn = false)
        => new(code, country, origin, nature, terms, mode, unit, qty, factor, value, weight, landed, isReturn);

    private static Item Stock(string sku = "SKU-1", string? cn = "84713000", string? origin = "CN", decimal? weight = 0.5m,
        string? unit = null, decimal? factor = null)
        => new()
        {
            Id = 1, InternalSKU = sku, ItemType = ItemType.Stock, CommodityCode = cn, OriginCountry = origin, NetWeightKg = weight,
            SupplementaryUnit = unit, SupplementaryUnitFactor = factor
        };

    private static InboundShipment Shipment(
        Item item, decimal qty = 10m, decimal unitPrice = 5m, decimal? invoicePrice = null, decimal? finalEur = null,
        string posting = "Posted", string currency = "EUR", decimal? unitsPerEur = null, string number = "INV-1")
        => new()
        {
            Id = 1, ReferenceNumber = "IN-1", InvoiceNumber = number, InvoiceDate = new DateOnly(2026, 11, 10), DocumentKind = "Invoice",
            PostingStatus = posting, VatTreatment = VatTreatment.EuAcquisition, Currency = currency, RateUnitsPerEur = unitsPerEur,
            DeliveryTerms = "DAP", TransportMode = "3", DispatchCountry = "PL", TransactionNature = "11",
            Lines =
            {
                new InboundShipmentLine
                {
                    Id = 1, ItemId = item.Id, Item = item, ExpectedQty = qty, InvoiceQty = qty, UnitPrice = unitPrice,
                    InvoiceUnitPrice = invoicePrice, FinalValueEur = finalEur
                }
            }
        };

    private static IntrastatCollected Collect(params InboundShipment[] shipments)
        => IntrastatReport.Collect(shipments, [], new Dictionary<int, InboundShipment>());

    [Fact]
    public void ValueIsInvoiceValue_NotLandedCost()
    {
        var shipment = Shipment(Stock(), qty: 10m, unitPrice: 5m, invoicePrice: 6m, finalEur: 75m);

        var collected = Collect(shipment);
        var row = IntrastatReport.Build(collected.Lines).Single();

        row.ValueEur.Should().Be(60.00m, "10 x invoice price 6, freight is not part of the declared value");
        row.LandedValueEur.Should().Be(75.00m);
    }

    [Fact]
    public void InvoiceValueFallsBackToOrderPrice_AndUsesTheDocumentRate()
    {
        var shipment = Shipment(Stock(), qty: 10m, unitPrice: 5m, invoicePrice: null, finalEur: 99m, currency: "USD", unitsPerEur: 1.25m);

        var row = IntrastatReport.Build(Collect(shipment).Lines).Single();

        row.ValueEur.Should().Be(40.00m, "10 x 5 USD at 1.25 USD per EUR");
    }

    [Fact]
    public void UnpostedDocuments_AreExcluded()
    {
        var collected = Collect(Shipment(Stock(), posting: "NotPosted"));

        collected.Lines.Should().BeEmpty();
        collected.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void DomesticDocuments_AreExcluded()
    {
        var shipment = Shipment(Stock());
        shipment.VatTreatment = VatTreatment.Domestic;

        Collect(shipment).Lines.Should().BeEmpty();
    }

    [Fact]
    public void LinesWithMissingDeclarationData_AreLeftOutWithAWarning()
    {
        var collected = Collect(
            Shipment(Stock(sku: "NO-ORIGIN", origin: null), number: "A"),
            Shipment(Stock(sku: "NO-CN", cn: null), number: "B"),
            Shipment(Stock(sku: "UNIT-NO-FACTOR", unit: "m2", factor: null), number: "C"),
            Shipment(Stock(sku: "OK"), number: "D"));

        collected.Lines.Should().HaveCount(1);
        collected.Warnings.Should().Equal("A|NO-ORIGIN", "B|NO-CN", "C|UNIT-NO-FACTOR");
    }

    [Fact]
    public void Grouping_SplitsByEveryDeclaredField_AndSumsTheRest()
    {
        var rows = IntrastatReport.Build(new[]
        {
            Line(value: 100.25m, qty: 10m),
            Line(value: 40.10m, qty: 4m, country: "pl"),
            Line(origin: "TR", value: 5m),
            Line(nature: "12", value: 6m),
            Line(terms: "EXW", value: 7m),
            Line(mode: "4", value: 8m),
            Line(unit: "p/st", factor: 1m, value: 9m),
            Line(code: "39269097", value: 11m),
            Line(country: "DE", value: 12m),
        });

        rows.Should().HaveCount(8);
        var merged = rows.Single(r => r.Lines == 2);
        merged.ValueEur.Should().Be(140.35m);
        merged.NetMassKg.Should().Be(7.000m);
        merged.Quantity.Should().Be(14m);
        rows.Sum(r => r.ValueEur).Should().Be(198.35m);
    }

    [Fact]
    public void SupplementaryQuantity_IsQtyTimesFactor_AndEmptyWithoutUnit()
    {
        var rows = IntrastatReport.Build(new[]
        {
            Line(unit: "m2", factor: 2.5m, qty: 4m),
            Line(unit: "m2", factor: 2.5m, qty: 2m),
            Line(code: "73181500", qty: 3m),
        });

        rows.Single(r => r.SupplementaryUnit == "m2").SupplementaryQuantity.Should().Be(15.000m);
        rows.Single(r => r.SupplementaryUnit is null).SupplementaryQuantity.Should().BeNull();
    }

    [Fact]
    public void SupplierReturn_IsASeparateRowFlaggedAsReturnWithCode21()
    {
        var item = Stock();
        var original = Shipment(item, qty: 10m, invoicePrice: 6m);
        var credit = new SupplierCreditDocument
        {
            Id = 7, DocumentKind = SupplierCreditDocument.KindSupplierReturn, OriginalShipmentId = original.Id,
            CreditNumber = "CR-1", CreditDate = new DateOnly(2026, 11, 20), Currency = "EUR", PostingStatus = "Posted",
            Lines = { new SupplierCreditLine { Id = 1, ItemId = item.Id, Item = item, ReturnQty = 2m, NetAmount = 12m, NetEur = 12m } }
        };
        var unposted = new SupplierCreditDocument
        {
            Id = 8, OriginalShipmentId = original.Id, CreditNumber = "CR-2", PostingStatus = "NotPosted",
            Lines = { new SupplierCreditLine { Id = 2, ItemId = item.Id, Item = item, ReturnQty = 1m, NetAmount = 6m, NetEur = 6m } }
        };

        var collected = IntrastatReport.Collect([original], [credit, unposted], new Dictionary<int, InboundShipment> { [original.Id] = original });
        var rows = IntrastatReport.Build(collected.Lines);

        rows.Should().HaveCount(2);
        rows[0].IsReturn.Should().BeFalse();
        rows[0].ValueEur.Should().Be(60m, "returns are not netted silently");
        rows[1].IsReturn.Should().BeTrue();
        rows[1].Nature.Should().Be("21");
        rows[1].ValueEur.Should().Be(12m);
        rows[1].NetMassKg.Should().Be(1.000m);
    }

    [Fact]
    public void PriceOnlyCredit_IsReportedAsWarning_NotAsReturn()
    {
        var item = Stock();
        var original = Shipment(item);
        var credit = new SupplierCreditDocument
        {
            Id = 7, OriginalShipmentId = original.Id, CreditNumber = "CR-1", PostingStatus = "Posted",
            Lines = { new SupplierCreditLine { Id = 1, ItemId = item.Id, Item = item, ReturnQty = 0m, NetAmount = 12m, NetEur = 12m } }
        };

        var collected = IntrastatReport.Collect([original], [credit], new Dictionary<int, InboundShipment> { [original.Id] = original });

        collected.Lines.Should().ContainSingle(l => !l.IsReturn);
        collected.Warnings.Should().Equal("CR-1|SKU-1");
    }

    [Fact]
    public void EmptyMonth_GivesNoRows()
    {
        IntrastatReport.Build(Collect().Lines).Should().BeEmpty();
    }

    [Fact]
    public void CsvHasAllDeclaredColumnsAndInvariantNumbers()
    {
        var csv = IntrastatReport.ToCsv(IntrastatReport.Build(new[]
        {
            Line(unit: "m2", factor: 2m, qty: 2m, value: 10m, weight: 0.25m, landed: 12.5m)
        }));

        csv.Should().Be(
            "CommodityCode;DispatchCountry;OriginCountry;NatureOfTransaction;DeliveryTerms;TransportMode;SupplementaryUnit;Return;NetMassKg;SupplementaryQuantity;Quantity;InvoiceValueEur;LandedCostEur\r\n"
            + "84713000;PL;CN;11;DAP;3;m2;0;0.500;4;2;10.00;12.50\r\n");
    }
}
