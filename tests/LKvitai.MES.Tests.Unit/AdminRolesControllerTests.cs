using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AdminRolesControllerTests
{
    [Fact]
    [Trait("Category", "RoleManagement")]
    public void Controller_ShouldRequireAdminPolicy()
    {
        var authorize = typeof(AdminRolesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().Be(WarehousePolicies.AdminOnly);
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task CreateRoleAsync_WhenPayloadMissing_ShouldReturnBadRequest()
    {
        var service = new Mock<IRoleManagementService>(MockBehavior.Strict);
        var controller = CreateController(service.Object);

        var result = await controller.CreateRoleAsync(null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task AssignRoleToUserAsync_WhenPayloadMissing_ShouldReturnBadRequest()
    {
        var service = new Mock<IRoleManagementService>(MockBehavior.Strict);
        var controller = CreateController(service.Object);

        var result = await controller.AssignRoleToUserAsync(Guid.NewGuid(), null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task DeleteRoleAsync_WhenSystemRole_ShouldReturnBadRequest()
    {
        var service = new Mock<IRoleManagementService>(MockBehavior.Strict);
        service.Setup(x => x.DeleteRoleAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(DomainErrorCodes.ValidationError, "Cannot delete system role"));

        var controller = CreateController(service.Object);

        var result = await controller.DeleteRoleAsync(1, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static AdminRolesController CreateController(IRoleManagementService service)
    {
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.Setup(x => x.GetCurrentUserId()).Returns("admin-user");

        return new AdminRolesController(service, currentUser.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
