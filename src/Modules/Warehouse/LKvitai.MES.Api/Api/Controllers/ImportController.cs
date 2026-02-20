using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Models;
using LKvitai.MES.Infrastructure.Imports;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
[Route("api/warehouse/v1/admin/import")]
public sealed class ImportController : ControllerBase
{
    private readonly IExcelTemplateService _templateService;
    private readonly IMasterDataImportService _importService;

    public ImportController(
        IExcelTemplateService templateService,
        IMasterDataImportService importService)
    {
        _templateService = templateService;
        _importService = importService;
    }

    [HttpGet("{entityType}/template")]
    public async Task<IActionResult> DownloadTemplateAsync(
        string entityType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await _templateService.GenerateTemplateAsync(entityType, cancellationToken);
            var fileName = $"{entityType.ToLowerInvariant()}-template.xlsx";
            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Failure(Result.Fail(DomainErrorCodes.ValidationError, $"Unsupported entity type '{entityType}'."));
        }
    }

    [HttpPost("{entityType}")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> ImportAsync(
        string entityType,
        [FromQuery] bool dryRun = true,
        [FromForm] IFormFile? file = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return Failure(Result.Fail(DomainErrorCodes.ValidationError, "Import file is required."));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.ImportAsync(entityType, stream, dryRun, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Failure(Result.Fail(DomainErrorCodes.ValidationError, $"Unsupported entity type '{entityType}'."));
        }
    }

    [HttpPost("error-report")]
    public IActionResult BuildErrorReport([FromBody] IReadOnlyList<ImportErrorReport> errors)
    {
        var report = _importService.GenerateErrorReport(errors);
        return File(
            report,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "import-errors.xlsx");
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
}
