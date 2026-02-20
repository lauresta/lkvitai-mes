using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/api-keys")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ICurrentUserService _currentUserService;

    public AdminApiKeysController(
        IApiKeyService apiKeyService,
        ICurrentUserService currentUserService)
    {
        _apiKeyService = apiKeyService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var items = await _apiKeyService.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateApiKeyPayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _apiKeyService.CreateAsync(
            new CreateApiKeyRequest(request.Name, request.Scopes ?? [], request.RateLimitPerMinute, request.ExpiresAt),
            _currentUserService.GetCurrentUserId(),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Created($"/api/warehouse/v1/admin/api-keys/{result.Value.Id}", result.Value);
    }

    [HttpPut("{id:int}/rotate")]
    public async Task<IActionResult> RotateAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _apiKeyService.RotateAsync(id, _currentUserService.GetCurrentUserId(), cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _apiKeyService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return NoContent();
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

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public sealed record CreateApiKeyPayload(
        string Name,
        IReadOnlyList<string> Scopes,
        int? RateLimitPerMinute,
        DateTimeOffset? ExpiresAt);
}
