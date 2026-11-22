using System.Xml.Linq;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// IDAIS XML of the arrivals declaration. The XSD (INTRASTATReportSubmission.xsd) is not in the repository, so these
/// tests check the structure described in the filling instruction, not schema validity.
/// </summary>
public sealed class IntrastatXmlTests
{
    private static readonly IntrastatDeclarant Declarant = new()
    {
        VatCode = "lt123456789", Title = "Example UAB", Street = "Gatve 1, Vilnius", ZipCode = "01100",
        ContactPerson = "Jonas Jonaitis", Email = "info@example.test", Phone = "+37060000000"
    };

    private static IntrastatRow Row(
        string unit = "", decimal value = 1234.5m, decimal mass = 12.3456m, decimal? suppQty = null, bool isReturn = false)
        => new("84713000", "PL", "CN", isReturn ? "21" : "11", "DAP", "3", unit.Length == 0 ? null : unit, isReturn,
            value, mass, 10m, suppQty, 1300m, 2);

    private static XDocument Parse(string xml) => XDocument.Parse(xml);

    private static string Item(XDocument doc, string name)
        => doc.Descendants("INTRASTATReportItem").First().Element(name)!.Value;

    [Fact]
    public void NormalMonth_WritesTheDeclarationInFormatOrder()
    {
        var result = IntrastatXmlWriter.Write(Declarant, 2026, 11, [Row(), Row() with { CommodityCode = "39269097" }], []);

        result.Problems.Should().BeEmpty();
        var doc = Parse(result.Xml!);
        doc.Root!.Name.LocalName.Should().Be("INTRASTATReportSubmission");
        doc.Root.Elements().Select(e => e.Name.LocalName).Should().Equal("SubmitterType", "VATPayer", "ReportPreparation", "INTRASTATReport");
        doc.Root.Element("SubmitterType")!.Value.Should().Be("VAT_PAYER");
        doc.Root.Element("VATPayer")!.Element("IdentifierVAT")!.Value.Should().Be("LT123456789");
        var report = doc.Root.Element("INTRASTATReport")!;
        report.Elements().Select(e => e.Name.LocalName).Should().Equal(
            "ReportingYear", "ReportingMonth", "TradeDirection", "ZeroTradeIndicator", "INTRASTATReportItemList");
        report.Element("TradeDirection")!.Value.Should().Be("IMPORT");
        report.Element("ZeroTradeIndicator")!.Value.Should().Be("false");
        doc.Descendants("ReportItemNumber").Select(e => e.Value).Should().Equal("1", "2");
        doc.Descendants("INTRASTATReportItem").First().Elements().Select(e => e.Name.LocalName).Should().Equal(
            "ReportItemNumber", "CombinedNomenclatureCode", "CountryOfDispatchOrDestinationCode", "CountryOfOriginCode",
            "NatureOfTransactionCode", "DeliveryTermsCode", "ModeOfTransportCode", "NetMass", "InvoicedAmount");
        result.ItemCount.Should().Be(2);
        result.TotalValueEur.Should().Be(2469.0m);
    }

    [Fact]
    public void ImportItem_HasNoPartnerNoRegionAndNoStatisticalValue()
    {
        var doc = Parse(IntrastatXmlWriter.Write(Declarant, 2026, 11, [Row()], []).Xml!);

        var names = doc.Descendants("INTRASTATReportItem").First().Elements().Select(e => e.Name.LocalName).ToList();
        names.Should().NotContain(["PartnerIdentifier", "RegionCode", "StatisticalValue"]);
    }

    [Fact]
    public void NumbersAreFormattedWithInvariantPointAndFixedPrecision()
    {
        var doc = Parse(IntrastatXmlWriter.Write(Declarant, 2026, 11,
            [Row(unit: "MTK", value: 1234.5m, mass: 12.3456m, suppQty: 7.5m)], []).Xml!);

        Item(doc, "InvoicedAmount").Should().Be("1234.50");
        Item(doc, "NetMass").Should().Be("12.346");
        Item(doc, "QuantityInSupplementaryUnitOfMeasure").Should().Be("7.5");
        Item(doc, "SupplementaryUnitOfMeasureCode").Should().Be("MTK");
        IntrastatXmlWriter.Decimal3(3m).Should().Be("3");
        IntrastatXmlWriter.Decimal3(0.0005m).Should().Be("0.001");
    }

    [Fact]
    public void ZeroMonth_WritesTheZeroIndicatorAndNoItemList()
    {
        var result = IntrastatXmlWriter.Write(Declarant, 2026, 12, [], []);

        result.IsZeroReport.Should().BeTrue();
        var report = Parse(result.Xml!).Root!.Element("INTRASTATReport")!;
        report.Element("ZeroTradeIndicator")!.Value.Should().Be("true");
        report.Element("INTRASTATReportItemList").Should().BeNull();
        report.Element("ReportingMonth")!.Value.Should().Be("12");
    }

    [Fact]
    public void SkippedLines_RefuseTheFile()
    {
        var result = IntrastatXmlWriter.Write(Declarant, 2026, 11, [Row()], ["INV-1|SKU-1"]);

        result.Xml.Should().BeNull();
        result.Problems.Select(p => p.Code).Should().Equal("IncompleteLines");
    }

    [Fact]
    public void ZeroReportIsNotWritten_WhenLinesWereSkipped()
    {
        var result = IntrastatXmlWriter.Write(Declarant, 2026, 11, [], ["INV-1|SKU-1"]);

        result.Xml.Should().BeNull();
        result.IsZeroReport.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("LT123456789", null)]
    [InlineData("", "Example UAB")]
    public void MissingDeclarant_RefusesTheFile(string? vat, string? title)
    {
        var result = IntrastatXmlWriter.Write(new IntrastatDeclarant { VatCode = vat, Title = title }, 2026, 11, [Row()], []);

        result.Xml.Should().BeNull();
        result.Problems.Should().ContainSingle(p => p.Code == "DeclarantMissing");
    }

    [Fact]
    public void NoDeclarantAtAll_RefusesTheFile()
        => IntrastatXmlWriter.Write(null, 2026, 11, [], []).Problems.Should().ContainSingle(p => p.Code == "DeclarantMissing");

    [Theory]
    [InlineData("123456789", false)]
    [InlineData("LT", false)]
    [InlineData("LT123456789012345", false)]
    [InlineData("LT 123", false)]
    [InlineData("LT123456789", true)]
    public void VatCodeShape(string code, bool valid) => IntrastatXmlWriter.IsVatCode(code).Should().Be(valid);

    [Fact]
    public void ReturnRows_AreLeftOutOfTheFileAndListedInANote()
    {
        var result = IntrastatXmlWriter.Write(Declarant, 2026, 11, [Row(), Row(value: 50m, isReturn: true)], []);

        result.ItemCount.Should().Be(1);
        Parse(result.Xml!).Descendants("INTRASTATReportItem").Should().HaveCount(1);
        result.Notes.Should().ContainSingle().Which.Should().Contain("50.00");
    }

    [Fact]
    public void MonthWithOnlyReturns_IsRefused()
        => IntrastatXmlWriter.Write(Declarant, 2026, 11, [Row(isReturn: true)], []).Problems
            .Should().ContainSingle(p => p.Code == "OnlyReturns");

    [Fact]
    public void SupplementaryUnitThatIsNotAThreeCharacterCode_IsRefused()
        => IntrastatXmlWriter.Write(Declarant, 2026, 11, [Row(unit: "m2", suppQty: 1m)], []).Problems
            .Should().ContainSingle(p => p.Code == "SupplementaryUnitCode");

    [Fact]
    public void XmlIsUtf8AndEscapesText()
    {
        var declarant = new IntrastatDeclarant { VatCode = "LT123456789", Title = "A & B <UAB>" };

        var xml = IntrastatXmlWriter.Write(declarant, 2026, 11, [], []).Xml!;

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        Parse(xml).Descendants("Title").Single().Value.Should().Be("A & B <UAB>");
    }

    // ---- versions ----

    private static async Task<(WarehouseDbContext Db, IntrastatService Service)> ServiceWithPostedMonthAsync(string country = "PL")
    {
        var db = new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.IntrastatDeclarants.Add(Declarant);
        db.Suppliers.Add(new Supplier { Id = 1, Kind = SupplierKind.Eu });
        var item = new Item
        {
            Id = 1, InternalSKU = "SKU", ItemType = ItemType.Stock, CommodityCode = "84713000", OriginCountry = "CN", NetWeightKg = 0.5m
        };
        db.InboundShipments.Add(new InboundShipment
        {
            Id = 1, SupplierId = 1, ReferenceNumber = "IN-1", InvoiceNumber = "INV-1", InvoiceDate = new DateOnly(2026, 11, 10), DocumentKind = "Invoice",
            PostingStatus = "Posted", VatTreatment = VatTreatment.EuAcquisition, Currency = "EUR", DeliveryTerms = "DAP",
            TransportMode = "3", DispatchCountry = country, TransactionNature = "11",
            Lines = { new InboundShipmentLine { Id = 1, ItemId = 1, Item = item, ExpectedQty = 10m, InvoiceQty = 10m, UnitPrice = 5m } }
        });
        await db.SaveChangesAsync();
        return (db, new IntrastatService(db));
    }

    [Fact]
    public async Task Versions_SameContentKeepsTheVersion_ChangedContentIsTheNextVersion()
    {
        var (db, service) = await ServiceWithPostedMonthAsync();

        var first = await service.GenerateAsync(2026, 11, "anna", CancellationToken.None);
        var again = await service.GenerateAsync(2026, 11, "anna", CancellationToken.None);
        db.InboundShipments.Single().DispatchCountry = "DE";
        await db.SaveChangesAsync();
        var correction = await service.GenerateAsync(2026, 11, "boris", CancellationToken.None);

        first.Export!.Version.Should().Be(1);
        first.IsNewVersion.Should().BeTrue();
        again.IsNewVersion.Should().BeFalse();
        again.Export!.Id.Should().Be(first.Export.Id);
        correction.Export!.Version.Should().Be(2);
        correction.Export.GeneratedBy.Should().Be("boris");
        correction.Export.Sha256.Should().NotBe(first.Export.Sha256).And.HaveLength(64);
        first.Export.RowCount.Should().Be(1);
        first.Export.TotalValueEur.Should().Be(50m);
        (await db.IntrastatXmlExports.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Versions_AreNumberedPerMonth_AndRefusalStoresNothing()
    {
        var (db, service) = await ServiceWithPostedMonthAsync();

        var zeroMonth = await service.GenerateAsync(2026, 12, "anna", CancellationToken.None);
        db.IntrastatDeclarants.RemoveRange(db.IntrastatDeclarants);
        await db.SaveChangesAsync();
        var refused = await service.GenerateAsync(2026, 11, "anna", CancellationToken.None);

        zeroMonth.Export!.Version.Should().Be(1);
        zeroMonth.Export.IsZeroReport.Should().BeTrue();
        refused.Export.Should().BeNull();
        (await db.IntrastatXmlExports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task MarkFiled_SetsTheDateOnce()
    {
        var (_, service) = await ServiceWithPostedMonthAsync();
        var export = (await service.GenerateAsync(2026, 11, "anna", CancellationToken.None)).Export!;

        var first = await service.MarkFiledAsync(export.Id, "anna", CancellationToken.None);
        var filedAt = first!.FiledAt;
        var second = await service.MarkFiledAsync(export.Id, "boris", CancellationToken.None);

        filedAt.Should().NotBeNull();
        second!.FiledAt.Should().Be(filedAt);
        second.FiledBy.Should().Be("anna");
    }
}
