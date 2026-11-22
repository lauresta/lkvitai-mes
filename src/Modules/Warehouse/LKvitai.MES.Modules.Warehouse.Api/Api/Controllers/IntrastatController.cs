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
    decimal TotalQuantity, IReadOnlyList<string> Warnings, decimal TotalReturnValueEur);

/// <summary>Blueprint 4.9 Intrastat arrivals report (posted EU acquisitions of a calendar month by invoice date, invoice value; supplier returns as separate rows).</summary>
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
            return Problem(detail: "year and month are required.", statusCode: StatusCodes.Status400BadRequest);
        var report = await BuildAsync(year, month, ct);
        return Ok(report);
    }

    [HttpGet("arrivals.csv")]
    public async Task<IActionResult> ArrivalsCsvAsync([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            return Problem(detail: "year and month are required.", statusCode: StatusCodes.Status400BadRequest);
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
            .Where(s => s.InvoiceDate >= from && s.InvoiceDate < to && s.DocumentKind == "Invoice" && s.PostingStatus == "Posted")
            .ToListAsync(ct);

        var credits = await _db.SupplierCreditDocuments.AsNoTracking()
            .Include(c => c.Lines).ThenInclude(l => l.Item)
            .Where(c => c.CreditDate >= from && c.CreditDate < to && c.PostingStatus == "Posted")
            .ToListAsync(ct);
        var originalIds = credits.Select(c => c.OriginalShipmentId).Distinct().ToList();
        var originals = originalIds.Count == 0
            ? new Dictionary<int, InboundShipment>()
            : await _db.InboundShipments.AsNoTracking()
                .Include(s => s.Supplier)
                .Where(s => originalIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);

        var collected = IntrastatReport.Collect(shipments, credits, originals);
        var rows = IntrastatReport.Build(collected.Lines);
        var arrivals = rows.Where(r => !r.IsReturn).ToList();
        return new IntrastatReportDto(year, month, rows, arrivals.Sum(r => r.ValueEur), arrivals.Sum(r => r.NetMassKg),
            arrivals.Sum(r => r.Quantity), collected.Warnings, rows.Where(r => r.IsReturn).Sum(r => r.ValueEur));
    }
}
