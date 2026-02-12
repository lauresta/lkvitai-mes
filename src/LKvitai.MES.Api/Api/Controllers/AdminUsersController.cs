using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Route("api/warehouse/v1/admin/users")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserStore _userStore;
    private readonly ICurrentUserService _currentUserService;

    public AdminUsersController(
        IAdminUserStore userStore,
        ICurrentUserService currentUserService)
    {
        _userStore = userStore;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public IActionResult GetAsync()
    {
        return Ok(_userStore.GetAll());
    }

    [HttpPost]
    public IActionResult CreateAsync([FromBody] CreateUserRequest? request)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        if (!_userStore.TryCreate(
                new CreateAdminUserRequest(
                    request.Username,
                    request.Email,
                    request.Password,
                    request.Roles,
                    request.Status),
                out var created,
                out var error))
        {
            return ValidationFailure(error ?? "Invalid request.");
        }

        HttpContext.RequestServices
            .GetRequiredService<ILogger<AdminUsersController>>()
            .LogInformation(
                "User created: {Username}, Roles: {Roles}, CreatedBy: {AdminUsername}",
                created!.Username,
                string.Join(",", created.Roles),
                _currentUserService.GetCurrentUserId());

        return Created($"/api/admin/users/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    public IActionResult UpdateAsync(Guid id, [FromBody] UpdateUserRequest? request)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        if (!_userStore.TryUpdate(
                id,
                new UpdateAdminUserRequest(request.Roles, request.Status, request.Email),
                out var updated,
                out var error))
        {
            if (string.Equals(error, "User not found.", StringComparison.Ordinal))
            {
                return Failure(Result.Fail(DomainErrorCodes.NotFound, error ?? "User not found."));
            }

            return ValidationFailure(error ?? "Invalid request.");
        }

        HttpContext.RequestServices
            .GetRequiredService<ILogger<AdminUsersController>>()
            .LogInformation(
                "User updated: {Username}, Roles: {Roles}, UpdatedBy: {AdminUsername}",
                updated!.Username,
                string.Join(",", updated.Roles),
                _currentUserService.GetCurrentUserId());

        return Ok(updated);
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

    public sealed record CreateUserRequest(
        string Username,
        string Email,
        string Password,
        IReadOnlyList<string> Roles,
        string Status);

    public sealed record UpdateUserRequest(
        IReadOnlyList<string> Roles,
        string Status,
        string? Email = null);
}
