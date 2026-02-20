using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AdminApprovalRulesControllerTests
{
    [Fact]
    [Trait("Category", "ApprovalRules")]
    public void CrudEndpoints_ShouldRequireAdminPolicy()
    {
        GetAuthorize(nameof(AdminApprovalRulesController.GetAsync)).Policy.Should().Be(WarehousePolicies.AdminOnly);
        GetAuthorize(nameof(AdminApprovalRulesController.CreateAsync)).Policy.Should().Be(WarehousePolicies.AdminOnly);
        GetAuthorize(nameof(AdminApprovalRulesController.UpdateAsync)).Policy.Should().Be(WarehousePolicies.AdminOnly);
        GetAuthorize(nameof(AdminApprovalRulesController.DeleteAsync)).Policy.Should().Be(WarehousePolicies.AdminOnly);
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public void EvaluateEndpoint_ShouldRequireAuthenticatedUser()
    {
        var authorize = GetAuthorize(nameof(AdminApprovalRulesController.EvaluateAsync));

        authorize.Policy.Should().BeNull();
        authorize.Roles.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task CreateAsync_WhenPayloadMissing_ShouldReturnBadRequest()
    {
        var service = new Mock<IApprovalRuleService>(MockBehavior.Strict);
        var controller = CreateController(service.Object);

        var result = await controller.CreateAsync(null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenRuleTypeInvalid_ShouldReturnBadRequest()
    {
        var service = new Mock<IApprovalRuleService>(MockBehavior.Strict);
        service.Setup(x => x.EvaluateAsync(It.IsAny<EvaluateApprovalRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApprovalRuleEvaluationDto>.Fail(
                DomainErrorCodes.ValidationError,
                "RuleType must be COST_ADJUSTMENT, WRITEDOWN, or TRANSFER."));

        var controller = CreateController(service.Object);

        var result = await controller.EvaluateAsync(
            new AdminApprovalRulesController.EvaluateApprovalRulePayload("BAD", 5),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static AdminApprovalRulesController CreateController(IApprovalRuleService service)
    {
        return new AdminApprovalRulesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static AuthorizeAttribute GetAuthorize(string methodName)
    {
        return typeof(AdminApprovalRulesController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>()
            .Single();
    }
}
