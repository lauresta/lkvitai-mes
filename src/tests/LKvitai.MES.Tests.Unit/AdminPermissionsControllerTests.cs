using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Api.Services;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AdminPermissionsControllerTests
{
    [Fact]
    [Trait("Category", "Permission")]
    public async Task GetPermissionsAsync_ShouldReturnOk()
    {
        var roleService = new Mock<IRoleManagementService>(MockBehavior.Strict);
        roleService.Setup(x => x.GetPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PermissionCatalogDto(1, "ITEM", "READ", "ALL")]);

        var sut = CreateController(roleService.Object);
        var result = await sut.GetPermissionsAsync();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task CheckPermissionAsync_WhenBodyMissing_ShouldReturnBadRequest()
    {
        var sut = CreateController(new Mock<IRoleManagementService>(MockBehavior.Strict).Object);

        var result = await sut.CheckPermissionAsync(null);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task CheckPermissionAsync_WhenValid_ShouldReturnAllowedResponse()
    {
        var roleService = new Mock<IRoleManagementService>(MockBehavior.Strict);
        roleService.Setup(x => x.CheckPermissionAsync(It.IsAny<Guid>(), "ITEM", "READ", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateController(roleService.Object);
        var result = await sut.CheckPermissionAsync(new AdminPermissionsController.CheckPermissionPayload(Guid.NewGuid(), "ITEM", "READ", null));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<AdminPermissionsController.PermissionCheckResponse>().Subject;
        payload.Allowed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task CheckPermissionAsync_WhenUserIdEmpty_ShouldReturnBadRequest()
    {
        var sut = CreateController(new Mock<IRoleManagementService>(MockBehavior.Strict).Object);
        var result = await sut.CheckPermissionAsync(new AdminPermissionsController.CheckPermissionPayload(Guid.Empty, "ITEM", "READ", null));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static AdminPermissionsController CreateController(IRoleManagementService roleManagementService)
    {
        return new AdminPermissionsController(roleManagementService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
