using System.Text.Json;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Stage W4 sender with the W8 (Intrastat) and W5 (supplier credits) fields.</summary>
public sealed class AgnumSumIntegrationTests
{
    private static readonly DateOnly Date = new(2026, 11, 20);

    [Fact]
    public void EuAcquisition_PutsTheIntrastatHeaderIntoThePayload_WithTheServiceFieldNames()
    {
        var shipment = new InboundShipment
        {
            VatTreatment = VatTreatment.EuAcquisition, TransactionNature = " 11 ", DeliveryTerms = "DAP", TransportMode = "30", DispatchCountry = "PL"
        };

        var built = AgnumPayloadBuilder.Build(new PostingPayloadInput(
            Guid.NewGuid(), 1, "FS 1", Date, 62, 493, 26, "EUR", 1m, 100m, 0m, "PVM1", "20110", [],
            Intrastat: PurchaseDocumentService.IntrastatHeader(shipment)));

        using var doc = JsonDocument.Parse(built.Json);
        var i = doc.RootElement.GetProperty("intrastat");
        i.GetProperty("nature").GetString().Should().Be("11");
        i.GetProperty("delivery_terms").GetString().Should().Be("DAP");
        i.GetProperty("transport").GetString().Should().Be("30");
        i.GetProperty("country").GetString().Should().Be("PL");
    }

    [Fact]
    public void DomesticPurchase_HasNoIntrastatBlock_AndKeepsItsHash()
    {
        var domestic = new InboundShipment { VatTreatment = VatTreatment.Domestic, DeliveryTerms = "DAP", TransactionNature = "11" };
        PurchaseDocumentService.IntrastatHeader(domestic).Should().BeNull();

        var built = AgnumPayloadBuilder.Build(new PostingPayloadInput(
            Guid.Empty, 1, "A", Date, 1, 2, 3, "EUR", 1m, 10m, 0m, null, "20110", [], Intrastat: null));
        built.Json.Should().NotContain("intrastat").And.NotContain("ref_doc");
    }

    [Fact]
    public async Task PostedCredit_GoesThroughTheOutbox_AsANegativeDocumentReferencingTheOriginal()
    {
        var credit = new SupplierCreditDocument
        {
            Id = 7, SupplierId = 3, CreditNumber = "CN-7", CreditDate = Date, OriginalInvoiceNumber = "INV-1",
            OriginalInvoiceDate = new DateOnly(2026, 10, 29), Currency = "EUR", NetAmount = 50m, VatAmount = 10.5m, GrossAmount = 60.5m,
            RateUnitsPerEur = 1m, PostingRevision = 1, AgnumKey = Guid.NewGuid()
        };

        var row = PurchaseDocumentService.CreditOutboxRow(credit, 62, 493, 26, "PVM1", "20110", "tester");

        row.DocumentType.Should().Be(OutboxDocumentType.SupplierCredit);
        row.GrossAmount.Should().Be(-60.5m, "the queue shows a credit with a minus sign");
        using (var doc = JsonDocument.Parse(row.PayloadJson))
        {
            var root = doc.RootElement;
            root.GetProperty("kind").GetString().Should().Be("credit");
            root.GetProperty("net").GetDecimal().Should().Be(50m, "the service takes positive sums and stores them negative");
            root.GetProperty("ref_doc_no").GetString().Should().Be("INV-1");
            root.GetProperty("ref_doc_date").GetString().Should().Be("2026-10-29");
        }

        await using var db = new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Suppliers.Add(new Supplier { Id = 3, Name = "Supplier", Code = "S3" });
        db.AgnumOutbox.Add(row);
        await db.SaveChangesAsync();

        var queue = await new AgnumOutboxService(db).ListAsync(null, true, 50, default);
        queue.Should().ContainSingle().Which.Should().Match<OutboxRowDto>(r =>
            r.DocumentType == OutboxDocumentType.SupplierCredit && r.DocumentNumber == "CN-7" && r.GrossAmount == -60.5m && r.Operation == "Post");
    }
}
