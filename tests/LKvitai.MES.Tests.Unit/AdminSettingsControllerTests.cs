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

public class AdminSettingsControllerTests
{
    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public void UpdateAsync_ShouldRequireAdminPolicy()
    {
        var method = typeof(AdminSettingsController).GetMethod(nameof(AdminSettingsController.UpdateAsync));

        method.Should().NotBeNull();
        var authorize = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().Be(WarehousePolicies.AdminOnly);
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public void GetAsync_ShouldRequireOperatorPolicy()
    {
        var method = typeof(AdminSettingsController).GetMethod(nameof(AdminSettingsController.GetAsync));

        method.Should().NotBeNull();
        var authorize = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().Be(WarehousePolicies.OperatorOrAbove);
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenRequestMissing_ShouldReturnBadRequest()
    {
        var service = new Mock<IWarehouseSettingsService>(MockBehavior.Strict);
        var controller = new AdminSettingsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.UpdateAsync(request: null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenValidationFails_ShouldReturnBadRequest()
    {
        var service = new Mock<IWarehouseSettingsService>(MockBehavior.Strict);
        service
            .Setup(x => x.UpdateAsync(It.IsAny<UpdateWarehouseSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<WarehouseSettingsDto>.Fail(DomainErrorCodes.ValidationError, "Capacity threshold must be 0-100%."));

        var controller = new AdminSettingsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.UpdateAsync(
            new AdminSettingsController.UpdateSettingsPayload(120, "FEFO", 10, 50, true),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
