using System.Text.Json;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/labels")]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
public sealed class LabelsController : ControllerBase
{
    private readonly ILabelPrintOrchestrator _labelPrintOrchestrator;

    public LabelsController(ILabelPrintOrchestrator labelPrintOrchestrator)
    {
        _labelPrintOrchestrator = labelPrintOrchestrator;
    }

    [HttpPost("print")]
    public async Task<IActionResult> PrintAsync(
        [FromBody] PrintLabelRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.LabelType))
        {
            return ValidationFailure("labelType is required.");
        }

        var data = MapRequestData(request.Data);

        try
        {
            var result = await _labelPrintOrchestrator.PrintAsync(
                request.LabelType,
                data,
                cancellationToken);

            return Ok(new PrintLabelResponse(result.Status, result.PdfUrl));
        }
        catch (InvalidOperationException ex)
        {
            return ValidationFailure(ex.Message);
        }
    }

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewAsync(
        [FromQuery] string labelType,
        CancellationToken cancellationToken = default)
    {
        var data = Request.Query
            .Where(x => !string.Equals(x.Key, "labelType", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        try
        {
            var preview = await _labelPrintOrchestrator.GeneratePreviewAsync(
                labelType,
                data,
                cancellationToken);

            return File(preview.Content, preview.ContentType, preview.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationFailure(ex.Message);
        }
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
        string LabelType,
        IDictionary<string, JsonElement>? Data);

    public sealed record PrintLabelResponse(
        string Status,
        string? PdfUrl);
}
