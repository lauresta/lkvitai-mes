using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>Blueprint 4.8: supplier credit notes and supplier returns linked to a posted purchase document.</summary>
[ApiController]
[Route("api/warehouse/v1/supplier-credits")]
public sealed class SupplierCreditsController : ControllerBase
{
    private readonly WarehouseDbContext _db;
    private readonly PurchaseDocumentService _purchase;
    private readonly ICurrentUserService _user;

    public SupplierCreditsController(WarehouseDbContext db, PurchaseDocumentService purchase, ICurrentUserService user)
    {
        _db = db;
        _purchase = purchase;
        _user = user;
    }

    public sealed record CreditLineRequest(int OriginalLineId, decimal ReturnQty, string? FromLocation, decimal NetAmount);

    public sealed record CreditRequest(
        int OriginalShipmentId, string DocumentKind, string CreditNumber, DateOnly CreditDate,
        decimal NetAmount, decimal VatAmount, string? Notes, bool Post, List<CreditLineRequest> Lines);

    public sealed record PostingProblemItem(string Code, string Text);

    public sealed record CreditLineDto(
        int Id, int OriginalLineId, int ItemId, string ItemSku, string ItemName, decimal ReturnQty, string? FromLocation,
        decimal NetAmount, decimal? NetEur);

    /// <summary>The AgnumNet/Vat/GrossEur amounts are negative; with the original invoice number and date they are what W4 sends.</summary>
    public sealed record CreditDto(
        int Id, string DocumentKind, int OriginalShipmentId, int SupplierId, string CreditNumber, DateOnly CreditDate,
        string? OriginalInvoiceNumber, DateOnly? OriginalInvoiceDate, string Currency,
        decimal NetAmount, decimal VatAmount, decimal GrossAmount, decimal? NetEur, decimal? VatEur, decimal? GrossEur,
        decimal? AgnumNetEur, decimal? AgnumVatEur, decimal? AgnumGrossEur,
        decimal? RateUnitsPerEur, string AccountingRoute, string? Notes, string PostingStatus,
        DateTimeOffset? PostedAt, string? PostedBy, int PostingRevision,
        IReadOnlyList<CreditLineDto> Lines, IReadOnlyList<PostingProblemItem> Problems);

    public sealed record PrefillLineDto(
        int OriginalLineId, int ItemId, string ItemSku, string ItemName, bool IsService,
        decimal ReceivedQty, decimal ReturnedQty, decimal InvoiceQty, decimal UnitPrice);

    public sealed record PrefillDto(
        int OriginalShipmentId, string ReferenceNumber, string SupplierName, string? InvoiceNumber, DateOnly? InvoiceDate,
        string Currency, decimal? NetAmount, decimal CreditedNet, IReadOnlyList<PrefillLineDto> Lines);

    [HttpGet("prefill/{shipmentId:int}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> Prefill(int shipmentId, CancellationToken ct)
    {
        var s = await _db.InboundShipments.AsNoTracking().Include(x => x.Supplier)
            .Include(x => x.Lines).ThenInclude(l => l.Item).FirstOrDefaultAsync(x => x.Id == shipmentId, ct);
        if (s is null) return NotFound();
        var posted = await _db.SupplierCreditLines.AsNoTracking()
            .Where(l => l.Credit!.OriginalShipmentId == shipmentId && l.Credit.PostingStatus == "Posted")
            .Select(l => new { l.OriginalLineId, l.ReturnQty }).ToListAsync(ct);
        var creditedNet = await _db.SupplierCreditDocuments.AsNoTracking()
            .Where(c => c.OriginalShipmentId == shipmentId && c.PostingStatus == "Posted").SumAsync(c => (decimal?)c.NetAmount, ct) ?? 0m;
        return Ok(new PrefillDto(s.Id, s.ReferenceNumber, s.Supplier?.Name ?? string.Empty, s.InvoiceNumber, s.InvoiceDate, s.Currency,
            s.NetAmount, creditedNet,
            s.Lines.OrderBy(l => l.Id).Select(l => new PrefillLineDto(
                l.Id, l.ItemId, l.Item?.InternalSKU ?? string.Empty, l.Item?.Name ?? string.Empty, l.Item?.ItemType == ItemType.Service,
                l.ReceivedQty, posted.Where(p => p.OriginalLineId == l.Id).Sum(p => p.ReturnQty),
                l.InvoiceQty ?? l.ExpectedQty, l.InvoiceUnitPrice ?? l.UnitPrice ?? 0m)).ToList()));
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> List([FromQuery] int? originalShipmentId, CancellationToken ct)
    {
        var query = _db.SupplierCreditDocuments.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Item).AsQueryable();
        if (originalShipmentId is not null) query = query.Where(c => c.OriginalShipmentId == originalShipmentId);
        return Ok((await query.OrderBy(c => c.Id).ToListAsync(ct)).Select(c => ToDto(c, [])).ToList());
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var credit = await _db.SupplierCreditDocuments.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (credit is null) return NotFound();
        IReadOnlyList<PostingProblem> problems = credit.PostingStatus == "Posted" ? [] : await _purchase.DryRunCreditAsync(id, ct);
        return Ok(ToDto(credit, problems));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> Create([FromBody] CreditRequest r, CancellationToken ct)
    {
        if (r.DocumentKind is not (SupplierCreditDocument.KindCreditNote or SupplierCreditDocument.KindSupplierReturn))
            return Problem("DocumentKind must be CreditNote or SupplierReturn.", statusCode: 400);
        var credit = new SupplierCreditDocument
        {
            DocumentKind = r.DocumentKind,
            OriginalShipmentId = r.OriginalShipmentId,
            CreditNumber = r.CreditNumber?.Trim() ?? string.Empty,
            CreditDate = r.CreditDate,
            NetAmount = r.NetAmount,
            VatAmount = r.VatAmount,
            Notes = r.Notes,
            Lines = r.Lines.Select(l => new SupplierCreditLine
            {
                OriginalLineId = l.OriginalLineId,
                ReturnQty = l.ReturnQty,
                FromLocation = l.FromLocation?.Trim(),
                NetAmount = l.NetAmount
            }).ToList()
        };

        var (created, problems, notFound) = await _purchase.CreateCreditAsync(credit, r.Post, (User.Identity?.Name ?? _user.GetCurrentUserId()), ct);
        if (notFound) return NotFound();
        if (problems.Count > 0) return Unprocessable(problems);
        return Ok(ToDto(await Reload(created!.Id, ct), []));
    }

    [HttpPost("{id:int}/post")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
    {
        var result = await _purchase.PostCreditAsync(id, (User.Identity?.Name ?? _user.GetCurrentUserId()), ct);
        if (result.NotFound) return NotFound();
        if (!result.Posted) return Unprocessable(result.Problems);
        return Ok(ToDto(await Reload(id, ct), []));
    }

    private Task<SupplierCreditDocument> Reload(int id, CancellationToken ct)
        => _db.SupplierCreditDocuments.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Item).FirstAsync(x => x.Id == id, ct);

    private IActionResult Unprocessable(IReadOnlyList<PostingProblem> problems)
        => UnprocessableEntity(new ProblemDetails
        {
            Title = "Document was not posted",
            Detail = string.Join("\n", problems.Select(p => $"{p.Code}|{p.Text}")),
            Status = StatusCodes.Status422UnprocessableEntity
        });

    private static CreditDto ToDto(SupplierCreditDocument c, IReadOnlyList<PostingProblem> problems) => new(
        c.Id, c.DocumentKind, c.OriginalShipmentId, c.SupplierId, c.CreditNumber, c.CreditDate,
        c.OriginalInvoiceNumber, c.OriginalInvoiceDate, c.Currency, c.NetAmount, c.VatAmount, c.GrossAmount,
        c.NetEur, c.VatEur, c.GrossEur, c.AgnumNetEur, c.AgnumVatEur, c.AgnumGrossEur,
        c.RateUnitsPerEur, c.AccountingRoute, c.Notes, c.PostingStatus, c.PostedAt, c.PostedBy, c.PostingRevision,
        c.Lines.OrderBy(l => l.Id).Select(l => new CreditLineDto(
            l.Id, l.OriginalLineId, l.ItemId, l.Item?.InternalSKU ?? string.Empty, l.Item?.Name ?? string.Empty,
            l.ReturnQty, l.FromLocation, l.NetAmount, l.NetEur)).ToList(),
        problems.Select(p => new PostingProblemItem(p.Code, p.Text)).ToList());
}
