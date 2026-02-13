using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AdminReasonCodesControllerTests
{
    [Fact]
    [Trait("Category", "ReasonCodes")]
    public void GetAsync_ShouldRequireManagerOrAdminPolicy()
    {
        var method = typeof(AdminReasonCodesController).GetMethod(nameof(AdminReasonCodesController.GetAsync));

        var authorize = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().Be(WarehousePolicies.ManagerOrAdmin);
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public void CreateUpdateDelete_ShouldRequireAdminPolicy()
    {
        var createAuthorize = GetAuthorize(nameof(AdminReasonCodesController.CreateAsync));
        var updateAuthorize = GetAuthorize(nameof(AdminReasonCodesController.UpdateAsync));
        var deleteAuthorize = GetAuthorize(nameof(AdminReasonCodesController.DeleteAsync));

        createAuthorize.Policy.Should().Be(WarehousePolicies.AdminOnly);
        updateAuthorize.Policy.Should().Be(WarehousePolicies.AdminOnly);
        deleteAuthorize.Policy.Should().Be(WarehousePolicies.AdminOnly);
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task CreateAsync_WhenPayloadMissing_ShouldReturnBadRequest()
    {
        var service = new Mock<IReasonCodeService>(MockBehavior.Strict);
        var controller = CreateController(service.Object);

        var result = await controller.CreateAsync(null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task DeleteAsync_WhenUsageExists_ShouldReturnBadRequest()
    {
        var service = new Mock<IReasonCodeService>(MockBehavior.Strict);
        service.Setup(x => x.DeleteAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(
                DomainErrorCodes.ValidationError,
                "Cannot delete reason code with usage history. Mark inactive instead."));

        var controller = CreateController(service.Object);

        var result = await controller.DeleteAsync(12, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static AdminReasonCodesController CreateController(IReasonCodeService service)
    {
        return new AdminReasonCodesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static AuthorizeAttribute GetAuthorize(string methodName)
    {
        return typeof(AdminReasonCodesController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>()
            .Single();
    }
}
