using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

public sealed record IntrastatReportDto(
    int Year, int Month, IReadOnlyList<IntrastatRow> Rows, decimal TotalValueEur, decimal TotalNetMassKg,
    decimal TotalQuantity, IReadOnlyList<string> Warnings);

/// <summary>Blueprint 4.9 Intrastat arrivals report (EU acquisitions of a calendar month, by invoice date).</summary>
[ApiController]
[Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
[Route("api/warehouse/v1/intrastat")]
public sealed class IntrastatController : ControllerBase
{
    private readonly WarehouseDbContext _db;

    public IntrastatController(WarehouseDbContext db) => _db = db;

    [HttpGet("arrivals")]
    public async Task<IActionResult> ArrivalsAsync([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            return BadRequest(new { detail = "year and month are required." });
        var report = await BuildAsync(year, month, ct);
        return Ok(report);
    }

    [HttpGet("arrivals.csv")]
    public async Task<IActionResult> ArrivalsCsvAsync([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            return BadRequest(new { detail = "year and month are required." });
        var report = await BuildAsync(year, month, ct);
        var bytes = new System.Text.UTF8Encoding(true).GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(IntrastatReport.ToCsv(report.Rows))).ToArray();
        return File(bytes, "text/csv", $"intrastat-arrivals-{year:0000}-{month:00}.csv");
    }

    private async Task<IntrastatReportDto> BuildAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var shipments = await _db.InboundShipments.AsNoTracking()
            .Include(s => s.Supplier)
            .Include(s => s.Lines).ThenInclude(l => l.Item)
            .Where(s => s.InvoiceDate >= from && s.InvoiceDate < to && s.DocumentKind == "Invoice")
            .ToListAsync(ct);

        var inputs = new List<IntrastatLineInput>();
        var warnings = new List<string>();
        foreach (var s in shipments)
        {
            var treatment = s.VatTreatment ?? (s.Supplier?.Kind == SupplierKind.Eu ? VatTreatment.EuAcquisition : null);
            if (treatment != VatTreatment.EuAcquisition) continue;
            var rate = s.Currency == "EUR" ? 1m
                : s.RateUnitsPerEur is > 0m ? PurchaseMath.RateEurPerUnit(s.RateUnitsPerEur.Value) : (decimal?)null;
            foreach (var l in s.Lines.Where(l => l.Item is { ItemType: ItemType.Stock }))
            {
                var qty = l.InvoiceQty ?? l.ExpectedQty;
                var value = l.FinalValueEur
                    ?? (rate is null ? (decimal?)null : PurchaseMath.ToEur(qty * (l.InvoiceUnitPrice ?? l.UnitPrice ?? 0m), rate.Value));
                if (string.IsNullOrWhiteSpace(l.Item!.CommodityCode) || string.IsNullOrWhiteSpace(s.DispatchCountry) || value is null)
                {
                    warnings.Add($"{s.InvoiceNumber ?? s.ReferenceNumber}|{l.Item.InternalSKU}");
                    continue;
                }

                inputs.Add(new IntrastatLineInput(l.Item.CommodityCode, s.DispatchCountry, qty, value.Value, l.Item.NetWeightKg));
            }
        }

        var rows = IntrastatReport.Build(inputs);
        return new IntrastatReportDto(year, month, rows, rows.Sum(r => r.ValueEur), rows.Sum(r => r.NetMassKg),
            rows.Sum(r => r.Quantity), warnings);
    }
}
