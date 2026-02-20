using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/labels")]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
public sealed class LabelsController : ControllerBase
{
    private readonly ILabelPrintOrchestrator _labelPrintOrchestrator;
    private readonly LabelTemplateEngine _labelTemplateEngine;
    private readonly ILabelPrintQueueProcessor _labelPrintQueueProcessor;

    public LabelsController(
        ILabelPrintOrchestrator labelPrintOrchestrator,
        LabelTemplateEngine labelTemplateEngine,
        ILabelPrintQueueProcessor labelPrintQueueProcessor)
    {
        _labelPrintOrchestrator = labelPrintOrchestrator;
        _labelTemplateEngine = labelTemplateEngine;
        _labelPrintQueueProcessor = labelPrintQueueProcessor;
    }

    [HttpPost("print")]
    public async Task<IActionResult> PrintAsync(
        [FromBody] PrintLabelRequest? request,
        CancellationToken cancellationToken = default)
    {
        var templateType = request?.ResolveTemplateType();
        if (request is null || string.IsNullOrWhiteSpace(templateType))
        {
            return ValidationFailure("templateType is required.");
        }

        var data = MapRequestData(request.Data);

        try
        {
            var result = await _labelPrintOrchestrator.PrintAsync(
                templateType!,
                data,
                cancellationToken);

            if (string.Equals(result.Status, "PDF_FALLBACK", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new PrintLabelResponse(
                    "FAILED",
                    result.PdfUrl,
                    result.Message));
            }

            if (string.Equals(result.Status, "QUEUED", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new PrintLabelResponse(
                    "QUEUED",
                    null,
                    result.Message));
            }

            return Ok(new PrintLabelResponse(result.Status, result.PdfUrl));
        }
        catch (InvalidOperationException ex)
        {
            return ValidationFailure(ex.Message);
        }
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewAsync(
        [FromBody] PreviewLabelRequest? request,
        CancellationToken cancellationToken = default)
    {
        var templateType = request?.ResolveTemplateType();
        if (request is null || string.IsNullOrWhiteSpace(templateType))
        {
            return ValidationFailure("templateType is required.");
        }

        var data = MapRequestData(request.Data);

        try
        {
            var preview = await _labelPrintOrchestrator.GeneratePreviewAsync(
                templateType!,
                data,
                cancellationToken);

            return File(preview.Content, preview.ContentType, preview.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationFailure(ex.Message);
        }
    }

    [HttpGet("templates")]
    public IActionResult GetTemplates()
    {
        var templates = _labelTemplateEngine.GetTemplates()
            .Select(x => new LabelTemplateResponse(
                _labelTemplateEngine.ToApiTemplateType(x.Type),
                x.ZplTemplate))
            .ToList();

        return Ok(templates);
    }

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewLegacyAsync(
        [FromQuery] string? labelType,
        CancellationToken cancellationToken = default)
    {
        var data = Request.Query
            .Where(x => !string.Equals(x.Key, "labelType", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        try
        {
            var preview = await _labelPrintOrchestrator.GeneratePreviewAsync(
                labelType ?? string.Empty,
                data,
                cancellationToken);

            return File(preview.Content, preview.ContentType, preview.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationFailure(ex.Message);
        }
    }

    [HttpGet("queue")]
    public IActionResult GetQueue()
    {
        var items = _labelPrintQueueProcessor
            .GetPendingAndFailed()
            .Select(ToQueueResponse)
            .ToList();

        return Ok(items);
    }

    [HttpPost("queue/{id:guid}/retry")]
    public async Task<IActionResult> RetryQueueItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _labelPrintQueueProcessor.RetryNowAsync(id, cancellationToken);
        if (!result.Found || result.Item is null)
        {
            return NotFound();
        }

        return Ok(ToQueueResponse(result.Item));
    }

    [HttpGet("pdf/{fileName}")]
    public async Task<IActionResult> GetPdfAsync(
        [FromRoute] string fileName,
        CancellationToken cancellationToken = default)
    {
        var file = await _labelPrintOrchestrator.GetPdfAsync(fileName, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private static Dictionary<string, string> MapRequestData(IDictionary<string, JsonElement>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in source)
        {
            data[entry.Key] = entry.Value.ValueKind switch
            {
                JsonValueKind.String => entry.Value.GetString() ?? string.Empty,
                JsonValueKind.Undefined => string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => entry.Value.GetRawText()
            };
        }

        return data;
    }

    public sealed record PrintLabelRequest(
        string? TemplateType,
        IDictionary<string, JsonElement>? Data,
        string? LabelType = null)
    {
        public string? ResolveTemplateType()
        {
            if (!string.IsNullOrWhiteSpace(TemplateType))
            {
                return TemplateType;
            }

            return LabelType;
        }
    }

    public sealed record PreviewLabelRequest(
        string? TemplateType,
        IDictionary<string, JsonElement>? Data,
        string? LabelType = null)
    {
        public string? ResolveTemplateType()
        {
            if (!string.IsNullOrWhiteSpace(TemplateType))
            {
                return TemplateType;
            }

            return LabelType;
        }
    }

    public sealed record PrintLabelResponse(
        string Status,
        string? PdfUrl,
        string? Message = null);

    public sealed record LabelTemplateResponse(
        string TemplateType,
        string ZplTemplate);

    public sealed record PrintQueueItemResponse(
        Guid Id,
        string TemplateType,
        string DataJson,
        string Status,
        int RetryCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastAttemptAt,
        string ErrorMessage);

    private static PrintQueueItemResponse ToQueueResponse(PrintQueueItem item)
    {
        return new PrintQueueItemResponse(
            item.Id,
            item.TemplateType,
            item.DataJson,
            item.Status.ToString().ToUpperInvariant(),
            item.RetryCount,
            item.CreatedAt,
            item.LastAttemptAt,
            item.ErrorMessage);
    }
}
