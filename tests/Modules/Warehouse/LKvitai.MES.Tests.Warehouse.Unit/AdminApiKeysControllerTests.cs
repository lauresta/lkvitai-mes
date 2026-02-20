using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AdminApiKeysControllerTests
{
    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task GetAsync_ShouldReturnOk()
    {
        var service = new Mock<IApiKeyService>(MockBehavior.Strict);
        service.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ApiKeyViewDto(1, "ERP", ["read:items"], null, true, 100, null, "admin", DateTimeOffset.UtcNow, null, null)]);

        var sut = CreateController(service.Object);
        var result = await sut.GetAsync();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_WhenBodyMissing_ShouldReturnBadRequest()
    {
        var sut = CreateController(new Mock<IApiKeyService>(MockBehavior.Strict).Object);
        var result = await sut.CreateAsync(null);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_WhenValid_ShouldReturnCreated()
    {
        var service = new Mock<IApiKeyService>(MockBehavior.Strict);
        service.Setup(x => x.CreateAsync(It.IsAny<CreateApiKeyRequest>(), "admin-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApiKeyCreatedDto>.Ok(new ApiKeyCreatedDto(5, "ERP", "wh_key", ["read:items"], null, true, 100, DateTimeOffset.UtcNow, null)));

        var sut = CreateController(service.Object);
        var payload = new AdminApiKeysController.CreateApiKeyPayload("ERP", ["read:items"], 100, null);
        var result = await sut.CreateAsync(payload);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task DeleteAsync_WhenNotFound_ShouldReturnNotFound()
    {
        var service = new Mock<IApiKeyService>(MockBehavior.Strict);
        service.Setup(x => x.DeleteAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(DomainErrorCodes.NotFound, "API key not found."));

        var sut = CreateController(service.Object);
        var result = await sut.DeleteAsync(99);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static AdminApiKeysController CreateController(IApiKeyService apiKeyService)
    {
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.Setup(x => x.GetCurrentUserId()).Returns("admin-user");

        return new AdminApiKeysController(apiKeyService, currentUser.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
