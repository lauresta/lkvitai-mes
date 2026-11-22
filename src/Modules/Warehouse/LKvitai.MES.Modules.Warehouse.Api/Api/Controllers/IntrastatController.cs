using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

public sealed record IntrastatReportDto(
    int Year, int Month, IReadOnlyList<IntrastatRow> Rows, decimal TotalValueEur, decimal TotalNetMassKg,
    decimal TotalQuantity, IReadOnlyList<string> Warnings, decimal TotalReturnValueEur,
    bool IsZeroReport);

public sealed record IntrastatDeclarantDto(
    string? VatCode, string? Title, string? Street, string? ZipCode, string? ContactPerson, string? Email, string? Phone);

public sealed record IntrastatXmlVersionDto(
    int Id, int Year, int Month, int Version, DateTimeOffset GeneratedAt, string GeneratedBy, bool IsZeroReport,
    int RowCount, decimal TotalValueEur, decimal TotalNetMassKg, string Sha256, DateTimeOffset? FiledAt, string? FiledBy);

/// <summary>Blueprint 4.9 Intrastat arrivals report (posted EU acquisitions of a calendar month by invoice date, invoice value; supplier returns as separate rows) and its IDAIS XML versions.</summary>
[ApiController]
[Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
[Route("api/warehouse/v1/intrastat")]
public sealed class IntrastatController : ControllerBase
{
    private readonly WarehouseDbContext _db;
    private readonly IntrastatService _service;

    public IntrastatController(WarehouseDbContext db, IntrastatService service)
    {
        _db = db;
        _service = service;
    }

    private string CurrentUser => User.Identity?.Name ?? "unknown";

    private static bool InvalidPeriod(int year, int month) => year is < 2000 or > 2100 || month is < 1 or > 12;

    private IActionResult PeriodRequired()
        => Problem(detail: "year and month are required.", statusCode: StatusCodes.Status400BadRequest);

    [HttpGet("arrivals")]
    public async Task<IActionResult> ArrivalsAsync([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (InvalidPeriod(year, month)) return PeriodRequired();
        return Ok(await BuildAsync(year, month, ct));
    }

    [HttpGet("arrivals.csv")]
    public async Task<IActionResult> ArrivalsCsvAsync([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (InvalidPeriod(year, month)) return PeriodRequired();
        var report = await BuildAsync(year, month, ct);
        var bytes = new System.Text.UTF8Encoding(true).GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(IntrastatReport.ToCsv(report.Rows))).ToArray();
        return File(bytes, "text/csv", $"intrastat-arrivals-{year:0000}-{month:00}.csv");
    }

    [HttpGet("declarant")]
    public async Task<IActionResult> GetDeclarantAsync(CancellationToken ct)
        => Ok(ToDto(await _service.GetDeclarantAsync(ct)));

    [HttpPut("declarant")]
    public async Task<IActionResult> SaveDeclarantAsync([FromBody] IntrastatDeclarantDto dto, CancellationToken ct)
    {
        var vat = dto.VatCode?.Trim();
        if (!string.IsNullOrEmpty(vat) && !IntrastatXmlWriter.IsVatCode(vat))
            return Problem(detail: "The VAT code must start with two country letters (up to 14 characters).", statusCode: StatusCodes.Status400BadRequest);
        var saved = await _service.SaveDeclarantAsync(new IntrastatDeclarant
        {
            VatCode = dto.VatCode, Title = dto.Title, Street = dto.Street, ZipCode = dto.ZipCode,
            ContactPerson = dto.ContactPerson, Email = dto.Email, Phone = dto.Phone
        }, CurrentUser, ct);
        return Ok(ToDto(saved));
    }

    /// <summary>Generates the XML of the month: the next version when the content changed, otherwise the latest version again.</summary>
    [HttpPost("arrivals.xml")]
    public async Task<IActionResult> ArrivalsXmlAsync([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (InvalidPeriod(year, month)) return PeriodRequired();
        var outcome = await _service.GenerateAsync(year, month, CurrentUser, ct);
        if (outcome.Export is null)
            return Problem(detail: string.Join(" ", outcome.Result.Problems.Select(p => p.Text)),
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: string.Join(",", outcome.Result.Problems.Select(p => p.Code)));
        return XmlFile(outcome.Export);
    }

    [HttpGet("xml-versions")]
    public async Task<IActionResult> XmlVersionsAsync([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (InvalidPeriod(year, month)) return PeriodRequired();
        var list = await _db.IntrastatXmlExports.AsNoTracking()
            .Where(e => e.Year == year && e.Month == month)
            .OrderByDescending(e => e.Version)
            .Select(e => new IntrastatXmlVersionDto(e.Id, e.Year, e.Month, e.Version, e.GeneratedAt, e.GeneratedBy,
                e.IsZeroReport, e.RowCount, e.TotalValueEur, e.TotalNetMassKg, e.Sha256, e.FiledAt, e.FiledBy))
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("xml-versions/{id:int}/file")]
    public async Task<IActionResult> XmlVersionFileAsync(int id, CancellationToken ct)
    {
        var export = await _db.IntrastatXmlExports.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return export is null ? NotFound() : XmlFile(export);
    }

    [HttpPost("xml-versions/{id:int}/filed")]
    public async Task<IActionResult> MarkFiledAsync(int id, CancellationToken ct)
    {
        var export = await _service.MarkFiledAsync(id, CurrentUser, ct);
        return export is null ? NotFound() : NoContent();
    }

    private FileContentResult XmlFile(IntrastatXmlExport export)
        => File(System.Text.Encoding.UTF8.GetBytes(export.Xml), "application/xml",
            $"intrastat-arrivals-{export.Year:0000}-{export.Month:00}-v{export.Version}.xml");

    private static IntrastatDeclarantDto ToDto(IntrastatDeclarant d)
        => new(d.VatCode, d.Title, d.Street, d.ZipCode, d.ContactPerson, d.Email, d.Phone);

    private async Task<IntrastatReportDto> BuildAsync(int year, int month, CancellationToken ct)
    {
        var (rows, warnings) = await _service.LoadAsync(year, month, ct);
        var arrivals = rows.Where(r => !r.IsReturn).ToList();
        return new IntrastatReportDto(year, month, rows, arrivals.Sum(r => r.ValueEur), arrivals.Sum(r => r.NetMassKg),
            arrivals.Sum(r => r.Quantity), warnings, rows.Where(r => r.IsReturn).Sum(r => r.ValueEur),
            IntrastatReport.IsZeroReport(rows, warnings));
    }
}
