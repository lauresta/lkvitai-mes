using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class MfaControllerTests
{
    [Fact]
    [Trait("Category", "MFA")]
    public async Task EnrollAsync_WhenUserIdClaimInvalid_ShouldReturnUnauthorized()
    {
        var controller = CreateController(new Mock<IMfaService>(MockBehavior.Strict).Object, userIdClaim: "not-guid");

        var result = await controller.EnrollAsync();

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task EnrollAsync_WhenServiceSucceeds_ShouldReturnOk()
    {
        var mfa = new Mock<IMfaService>(MockBehavior.Strict);
        mfa.Setup(x => x.EnrollAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MfaEnrollmentDto>.Ok(new MfaEnrollmentDto("ABC", "data:image/png;base64,AAA", ["CODE1"], false)));

        var controller = CreateController(mfa.Object);

        var result = await controller.EnrollAsync();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<MfaController.MfaEnrollmentResponse>().Subject;
        payload.ManualSecret.Should().Be("ABC");
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyEnrollmentAsync_WhenCodeMissing_ShouldReturnBadRequest()
    {
        var controller = CreateController(new Mock<IMfaService>(MockBehavior.Strict).Object);

        var result = await controller.VerifyEnrollmentAsync(new MfaController.VerifyEnrollmentPayload(" "));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyAsync_WhenNoCodeOrBackup_ShouldReturnBadRequest()
    {
        var controller = CreateController(new Mock<IMfaService>(MockBehavior.Strict).Object);

        var result = await controller.VerifyAsync(new MfaController.VerifyMfaPayload("challenge", null, null));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task ResetAsync_WhenApprovalMissing_ShouldReturnBadRequest()
    {
        var controller = CreateController(new Mock<IMfaService>(MockBehavior.Strict).Object);

        var result = await controller.ResetAsync(Guid.NewGuid(), new MfaController.ResetMfaPayload(false, "not approved"));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task GetBackupCodesAsync_WhenRegenerate_ShouldReturnCodes()
    {
        var mfa = new Mock<IMfaService>(MockBehavior.Strict);
        mfa.Setup(x => x.RegenerateBackupCodesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Ok(["A", "B", "C"]));

        var controller = CreateController(mfa.Object);

        var result = await controller.GetBackupCodesAsync(regenerate: true);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<MfaController.BackupCodesResponse>().Subject;
        payload.BackupCodes.Should().HaveCount(3);
        payload.RemainingBackupCodes.Should().Be(3);
    }

    private static MfaController CreateController(IMfaService mfaService, string userIdClaim = "11111111-1111-1111-1111-111111111111")
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userIdClaim),
            new Claim(ClaimTypes.Name, "user@example.com")
        ], "test");

        return new MfaController(mfaService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }
}
