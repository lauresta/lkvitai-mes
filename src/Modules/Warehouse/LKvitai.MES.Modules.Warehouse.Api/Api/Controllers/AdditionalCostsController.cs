using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>Blueprint 4.4: additional cost types for the shipment screens and cost invoices of other companies.</summary>
[ApiController]
[Route("api/warehouse/v1/additional-costs")]
public sealed class AdditionalCostsController : ControllerBase
{
    private readonly WarehouseDbContext _db;
    private readonly PurchaseDocumentService _purchase;
    private readonly ICurrentUserService _user;

    public AdditionalCostsController(WarehouseDbContext db, PurchaseDocumentService purchase, ICurrentUserService user)
    {
        _db = db;
        _purchase = purchase;
        _user = user;
    }

    public sealed record CostTypeDto(string Code, string Name, bool IncludedInCost, string? ExpenseAccount);

    public sealed record CostDocumentRequest(
        int SupplierId, string DocumentNumber, DateOnly DocumentDate, string? Currency,
        decimal NetAmount, decimal VatAmount, string? VatTreatment, string? AccountingRoute,
        int BasisShipmentId, string CostTypeCode, string? ManualReason = null);

    public sealed record CostDocumentDto(
        int Id, int SupplierId, string DocumentNumber, DateOnly DocumentDate, string Currency,
        decimal NetAmount, decimal VatAmount, decimal GrossAmount, decimal? NetEur, decimal? RateUnitsPerEur,
        string VatTreatment, string AccountingRoute, int BasisShipmentId, string CostTypeCode, string PostingStatus,
        string? ManualReason, string? QueueStatus, string? QueueError, string? SupplierName);

    public sealed record LookupItem(int Id, string Label);
    public sealed record CostLookups(IReadOnlyList<LookupItem> Suppliers, IReadOnlyList<LookupItem> Shipments);

    /// <summary>Pick-lists of the cost document screen: suppliers and posted shipments a cost can refer to.</summary>
    [HttpGet("lookups")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetLookups(CancellationToken ct)
    {
        var suppliers = await _db.Suppliers.AsNoTracking().OrderBy(s => s.Name).Take(2000)
            .Select(s => new LookupItem(s.Id, s.Code + " - " + s.Name)).ToListAsync(ct);
        var shipments = await _db.InboundShipments.AsNoTracking().Where(s => s.PostingStatus == "Posted")
            .OrderByDescending(s => s.Id).Take(500)
            .Select(s => new LookupItem(s.Id, s.ReferenceNumber + " / " + (s.InvoiceNumber ?? ""))).ToListAsync(ct);
        return Ok(new CostLookups(suppliers, shipments));
    }

    [HttpGet("types")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetTypes(CancellationToken ct)
        => Ok(await _db.AdditionalCostTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Id)
            .Select(t => new CostTypeDto(t.Code, t.Name, t.IncludedInCost, t.ExpenseAccount)).ToListAsync(ct));

    [HttpGet("documents")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetDocuments([FromQuery] int? basisShipmentId, CancellationToken ct)
    {
        var query = _db.AdditionalCostDocuments.AsNoTracking();
        if (basisShipmentId is not null) query = query.Where(d => d.BasisShipmentId == basisShipmentId);
        var docs = await query.OrderByDescending(d => d.Id).Take(500).ToListAsync(ct);
        var ids = docs.Select(d => d.Id).ToList();
        var queue = (await _db.AgnumOutbox.AsNoTracking().Where(r => r.DocumentType == "AdditionalCostDocument" && ids.Contains(r.DocumentId)).ToListAsync(ct))
            .GroupBy(r => r.DocumentId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());
        var supplierIds = docs.Select(d => d.SupplierId).Distinct().ToList();
        var names = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return Ok(docs.Select(d => ToDto(d, queue.GetValueOrDefault(d.Id), names.GetValueOrDefault(d.SupplierId))).ToList());
    }

    [HttpPost("documents")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> CreateDocument([FromBody] CostDocumentRequest r, CancellationToken ct)
    {
        if (r.NetAmount < 0m || r.VatAmount < 0m)
            return Problem("Amounts must be greater than or equal to 0.", statusCode: 400);
        if (!await _db.Suppliers.AnyAsync(s => s.Id == r.SupplierId, ct))
            return Problem("Supplier does not exist.", statusCode: 400);
        if (!await _db.InboundShipments.AnyAsync(s => s.Id == r.BasisShipmentId, ct))
            return Problem("Basis shipment does not exist.", statusCode: 400);
        if (!await _db.AdditionalCostTypes.AnyAsync(t => t.Code == r.CostTypeCode && t.IsActive, ct))
            return Problem($"Unknown additional cost type: {r.CostTypeCode}.", statusCode: 400);

        var treatment = Enum.TryParse<VatTreatment>(r.VatTreatment, true, out var parsed) ? parsed : VatTreatment.Domestic;
        var route = r.AccountingRoute == "ManualInAgnum" ? "ManualInAgnum" : "Auto";
        var doc = new AdditionalCostDocument
        {
            SupplierId = r.SupplierId,
            DocumentNumber = r.DocumentNumber.Trim(),
            DocumentDate = r.DocumentDate,
            Currency = string.IsNullOrWhiteSpace(r.Currency) ? "EUR" : r.Currency.Trim().ToUpperInvariant(),
            NetAmount = r.NetAmount,
            VatAmount = r.VatAmount,
            GrossAmount = r.NetAmount + r.VatAmount,
            VatTreatment = treatment,
            AccountingRoute = route,
            ManualReason = route == "ManualInAgnum" ? r.ManualReason?.Trim() : null,
            BasisShipmentId = r.BasisShipmentId,
            CostTypeCode = r.CostTypeCode,
            CreatedBy = _user.GetCurrentUserId()
        };
        _db.AdditionalCostDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(doc));
    }

    [HttpPost("documents/{id:int}/post")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> PostDocument(int id, CancellationToken ct)
    {
        var result = await _purchase.PostCostDocumentAsync(id, _user.GetCurrentUserId(), ct);
        if (result.NotFound) return NotFound();
        if (!result.Posted)
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Document was not posted",
                Detail = string.Join("\n", result.Problems.Select(p => $"{p.Code}|{p.Text}")),
                Status = StatusCodes.Status422UnprocessableEntity
            });
        return Ok(ToDto(await _db.AdditionalCostDocuments.AsNoTracking().FirstAsync(d => d.Id == id, ct)));
    }

    private static CostDocumentDto ToDto(AdditionalCostDocument d, AgnumDocumentOutbox? queue = null, string? supplier = null) => new(
        d.Id, d.SupplierId, d.DocumentNumber, d.DocumentDate, d.Currency, d.NetAmount, d.VatAmount, d.GrossAmount,
        d.NetEur, d.RateUnitsPerEur, d.VatTreatment.ToString(), d.AccountingRoute, d.BasisShipmentId, d.CostTypeCode, d.PostingStatus,
        d.ManualReason, queue?.Status, queue?.LastError, supplier);
}
