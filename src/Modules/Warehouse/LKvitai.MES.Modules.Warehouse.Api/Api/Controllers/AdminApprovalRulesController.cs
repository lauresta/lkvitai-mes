using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/approval-rules")]
public sealed class AdminApprovalRulesController : ControllerBase
{
    private readonly IApprovalRuleService _approvalRuleService;

    public AdminApprovalRulesController(IApprovalRuleService approvalRuleService)
    {
        _approvalRuleService = approvalRuleService;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _approvalRuleService.GetAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertApprovalRulePayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _approvalRuleService.CreateAsync(
            new CreateApprovalRuleRequest(
                request.RuleType,
                request.ThresholdType,
                request.ThresholdValue,
                request.ApproverRole,
                request.Active,
                request.Priority),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Created($"/api/warehouse/v1/admin/approval-rules/{result.Value.Id}", result.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpsertApprovalRulePayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _approvalRuleService.UpdateAsync(
            id,
            new UpdateApprovalRuleRequest(
                request.RuleType,
                request.ThresholdType,
                request.ThresholdValue,
                request.ApproverRole,
                request.Active,
                request.Priority),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _approvalRuleService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return NoContent();
    }

    [HttpPost("evaluate")]
    [Authorize]
    public async Task<IActionResult> EvaluateAsync(
        [FromBody] EvaluateApprovalRulePayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _approvalRuleService.EvaluateAsync(
            new EvaluateApprovalRuleRequest(request.RuleType, request.Value),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ValidationFailure(result.ErrorDetail ?? result.Error);
        }

        return Ok(new EvaluateApprovalRuleResponse(result.Value.RequiresApproval, result.Value.ApproverRole));
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

    public sealed record UpsertApprovalRulePayload(
        string RuleType,
        string ThresholdType,
        decimal ThresholdValue,
        string ApproverRole,
        bool Active,
        int Priority);

    public sealed record EvaluateApprovalRulePayload(string RuleType, decimal Value);

    public sealed record EvaluateApprovalRuleResponse(bool RequiresApproval, string? ApproverRole);
}
