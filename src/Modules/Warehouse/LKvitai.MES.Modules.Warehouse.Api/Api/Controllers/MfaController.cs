using System.Security.Claims;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/auth/mfa")]
public sealed class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;

    public MfaController(IMfaService mfaService)
    {
        _mfaService = mfaService;
    }

    [HttpPost("enroll")]
    [Authorize]
    public async Task<IActionResult> EnrollAsync(CancellationToken cancellationToken = default)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedFailure("Authenticated user identifier is invalid.");
        }

        var userLabel = User.FindFirstValue(ClaimTypes.Email)
                        ?? User.FindFirstValue("preferred_username")
                        ?? User.FindFirstValue(ClaimTypes.Name)
                        ?? userId.ToString();

        var result = await _mfaService.EnrollAsync(userId, userLabel, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(new MfaEnrollmentResponse(
            result.Value.ManualSecret,
            result.Value.QrCodeDataUri,
            result.Value.BackupCodes,
            result.Value.MfaEnabled));
    }

    [HttpPost("verify-enrollment")]
    [Authorize]
    public async Task<IActionResult> VerifyEnrollmentAsync(
        [FromBody] VerifyEnrollmentPayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Code is required.");
        }

        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedFailure("Authenticated user identifier is invalid.");
        }

        var result = await _mfaService.VerifyEnrollmentAsync(userId, request.Code, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(new VerifyEnrollmentResponse(true));
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyAsync(
        [FromBody] VerifyMfaPayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Code) && string.IsNullOrWhiteSpace(request.BackupCode))
        {
            return ValidationFailure("Code or backupCode is required.");
        }

        var result = await _mfaService.VerifyChallengeAsync(
            new MfaVerifyRequest(request.ChallengeToken ?? string.Empty, request.Code, request.BackupCode),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(new VerifyMfaResponse(
            result.Value.AccessToken,
            result.Value.ExpiresAt,
            result.Value.MfaVerified,
            result.Value.RemainingBackupCodes));
    }

    [HttpPost("reset/{userId:guid}")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> ResetAsync(
        Guid userId,
        [FromBody] ResetMfaPayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !request.Approved)
        {
            return ValidationFailure("Admin approval confirmation is required.");
        }

        var result = await _mfaService.ResetAsync(userId, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return NoContent();
    }

    [HttpGet("backup-codes")]
    [Authorize]
    public async Task<IActionResult> GetBackupCodesAsync(
        [FromQuery] bool regenerate = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedFailure("Authenticated user identifier is invalid.");
        }

        if (regenerate)
        {
            var regenerateResult = await _mfaService.RegenerateBackupCodesAsync(userId, cancellationToken);
            if (!regenerateResult.IsSuccess)
            {
                return Failure(regenerateResult);
            }

            return Ok(new BackupCodesResponse(regenerateResult.Value, regenerateResult.Value.Count));
        }

        var status = await _mfaService.GetStatusAsync(userId, cancellationToken);
        return Ok(new BackupCodesStatusResponse(status.BackupCodeCount ?? 0, status.MfaEnabled, status.MfaEnrolledAt));
    }

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(raw, out userId);
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

    private ObjectResult UnauthorizedFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Unauthorized,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status401Unauthorized
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

    public sealed record VerifyEnrollmentPayload(string Code);

    public sealed record VerifyMfaPayload(string ChallengeToken, string? Code, string? BackupCode);

    public sealed record ResetMfaPayload(bool Approved, string? Reason);

    public sealed record MfaEnrollmentResponse(
        string ManualSecret,
        string QrCodeDataUri,
        IReadOnlyList<string> BackupCodes,
        bool MfaEnabled);

    public sealed record VerifyEnrollmentResponse(bool MfaEnabled);

    public sealed record VerifyMfaResponse(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        bool MfaVerified,
        int RemainingBackupCodes);

    public sealed record BackupCodesResponse(IReadOnlyList<string> BackupCodes, int RemainingBackupCodes);

    public sealed record BackupCodesStatusResponse(
        int RemainingBackupCodes,
        bool MfaEnabled,
        DateTimeOffset? MfaEnrolledAt);
}
