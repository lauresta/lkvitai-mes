using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>Blueprint 4.6 tab "Sending", 4.7 queue screen, 4.8 un-post.</summary>
[ApiController]
[Route("api/warehouse/v1/agnum/outbox")]
[Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
public sealed class AgnumOutboxController : ControllerBase
{
    private readonly AgnumOutboxService _outbox;
    private readonly AgnumOutboxSender _sender;
    private readonly IAgnumReferenceClient _reference;
    private readonly PurchaseDocumentService _purchase;
    private readonly IConfiguration _configuration;

    public AgnumOutboxController(AgnumOutboxService outbox, AgnumOutboxSender sender, IAgnumReferenceClient reference,
        PurchaseDocumentService purchase, IConfiguration configuration)
    {
        _outbox = outbox;
        _sender = sender;
        _reference = reference;
        _purchase = purchase;
        _configuration = configuration;
    }

    public sealed record SettingsRequest(bool Enabled, DateOnly? StartDate);
    public sealed record ManualRequest(string? Reason);
    public sealed record UnpostRequest(string DocumentType, int DocumentId);
    public sealed record UnpostResponse(string Outcome, IReadOnlyList<PostingProblemDto> Problems);
    public sealed record PostingProblemDto(string Code, string Text);
    public sealed record ConnectionResult(bool Ok, string? Message, int Warehouses);

    private string User => HttpContext.User.Identity?.Name ?? "unknown";

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _outbox.GetSettingsDtoAsync(_configuration.GetValue<string>("Agnum:Api:BaseUrl") ?? _configuration.GetValue<string>("AGNUM_API_BASE_URL"), ct));

    [HttpPut("settings")]
    public Task<IActionResult> SaveSettings([FromBody] SettingsRequest r, CancellationToken ct)
        => Guard(async () =>
        {
            await _outbox.SaveSettingsAsync(r.Enabled, r.StartDate, User, ct);
            return NoContent();
        });

    [HttpPost("check-connection")]
    public async Task<IActionResult> CheckConnection(CancellationToken ct)
    {
        try
        {
            var warehouses = await _reference.GetWarehousesAsync(ct);
            return Ok(new ConnectionResult(true, null, warehouses.Count));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Ok(new ConnectionResult(false, ex.Message, 0));
        }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] bool openOnly = false, [FromQuery] int take = 200, CancellationToken ct = default)
        => Ok(await _outbox.ListAsync(status, openOnly, take, ct));

    [HttpGet("documents/{documentType}/{documentId:int}")]
    public async Task<IActionResult> ForDocument(string documentType, int documentId, CancellationToken ct)
        => Ok(await _outbox.ForDocumentAsync(documentType, documentId, ct));

    [HttpGet("findings")]
    public async Task<IActionResult> Findings(CancellationToken ct) => Ok(await _outbox.FindingsAsync(ct));

    [HttpPost("{id:long}/retry")]
    public Task<IActionResult> Retry(long id, CancellationToken ct)
        => Guard(async () => { await _outbox.RetryAsync(id, ct); return NoContent(); });

    [HttpPost("{id:long}/mark-manual")]
    public Task<IActionResult> MarkManual(long id, [FromBody] ManualRequest r, CancellationToken ct)
        => Guard(async () => { await _outbox.MarkManualAsync(id, r.Reason ?? string.Empty, User, ct); return NoContent(); });

    /// <summary>Sends the queue now instead of waiting for the next minute.</summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct) => Ok(await _sender.RunOnceAsync(ct));

    [HttpPost("unpost")]
    public Task<IActionResult> Unpost([FromBody] UnpostRequest r, CancellationToken ct)
        => Guard(async () =>
        {
            if (r.DocumentType is not (OutboxDocumentType.Shipment or OutboxDocumentType.CostDocument))
                return BadRequest(new ProblemDetails { Title = "Unknown document type", Status = 400 });
            var result = await _purchase.UnpostAsync(r.DocumentType, r.DocumentId, User, ct);
            if (result.NotFound) return NotFound();
            var problems = result.Problems.Select(p => new PostingProblemDto(p.Code, p.Text)).ToList();
            return result.Outcome == PurchaseDocumentService.UnpostResult.Blocked
                ? UnprocessableEntity(new ProblemDetails
                {
                    Title = "The document was not un-posted",
                    Detail = string.Join("\n", problems.Select(p => $"{p.Code}|{p.Text}")),
                    Status = StatusCodes.Status422UnprocessableEntity
                })
                : Ok(new UnpostResponse(result.Outcome, problems));
        });

    private async Task<IActionResult> Guard(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (OutboxActionException ex)
        {
            return UnprocessableEntity(new ProblemDetails { Title = "Not allowed", Detail = ex.Message, Status = 422 });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Not found", Detail = ex.Message, Status = 404 });
        }
    }
}
