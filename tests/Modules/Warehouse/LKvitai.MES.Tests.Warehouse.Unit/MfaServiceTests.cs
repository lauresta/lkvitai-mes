using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpNet;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class MfaServiceTests
{
    [Fact]
    [Trait("Category", "MFA")]
    public async Task EnrollAsync_WhenUserExists_ShouldReturnSecretQrAndTenBackupCodes()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");

        result.IsSuccess.Should().BeTrue();
        result.Value.ManualSecret.Should().NotBeNullOrWhiteSpace();
        result.Value.QrCodeDataUri.Should().StartWith("data:image/png;base64,");
        result.Value.BackupCodes.Should().HaveCount(10);
        result.Value.MfaEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task EnrollAsync_WhenUserMissing_ShouldFailNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.EnrollAsync(Guid.NewGuid(), "missing@example.com");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyEnrollmentAsync_WhenCodeMissing_ShouldFailValidation()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        _ = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var result = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, " ");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyEnrollmentAsync_WhenEnrollmentMissing_ShouldFailNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, "123456");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyEnrollmentAsync_WhenCodeInvalid_ShouldFailValidation()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        _ = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var result = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, "999999");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyEnrollmentAsync_WhenCodeValid_ShouldEnableMfa()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var enrollment = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var code = CreateTotp(enrollment.Value.ManualSecret);

        var result = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, code);
        var status = await sut.GetStatusAsync(fixture.KnownUserId);

        result.IsSuccess.Should().BeTrue();
        status.MfaEnabled.Should().BeTrue();
        status.MfaEnrolledAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyChallengeAsync_WhenChallengeTokenInvalid_ShouldFailUnauthorized()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.VerifyChallengeAsync(new MfaVerifyRequest("invalid", "123456", null));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.Unauthorized);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyChallengeAsync_WhenChallengeTokenExpired_ShouldFailUnauthorized()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var enrollment = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var code = CreateTotp(enrollment.Value.ManualSecret);
        _ = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, code);

        var expiredChallenge = $"{fixture.KnownUserId}|WarehouseManager|1|oauth";
        var verify = await sut.VerifyChallengeAsync(new MfaVerifyRequest(expiredChallenge, code, null));

        verify.IsSuccess.Should().BeFalse();
        verify.ErrorCode.Should().Be(DomainErrorCodes.Unauthorized);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyChallengeAsync_WhenTotpValid_ShouldReturnMfaAccessToken()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var enrollment = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var enrollmentCode = CreateTotp(enrollment.Value.ManualSecret);
        _ = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, enrollmentCode);

        var challengeToken = fixture.SessionTokenService.IssueChallengeToken(
            fixture.KnownUserId.ToString(),
            [WarehouseRoles.WarehouseManager],
            10);

        var code = CreateTotp(enrollment.Value.ManualSecret);
        var verify = await sut.VerifyChallengeAsync(new MfaVerifyRequest(challengeToken, code, null));

        verify.IsSuccess.Should().BeTrue();
        verify.Value.AccessToken.Should().Contain("|oauth|mfa");
        verify.Value.MfaVerified.Should().BeTrue();
        verify.Value.RemainingBackupCodes.Should().Be(10);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyChallengeAsync_WhenBackupCodeUsed_ShouldConsumeCode()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var enrollment = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var code = CreateTotp(enrollment.Value.ManualSecret);
        _ = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, code);

        var challengeToken = fixture.SessionTokenService.IssueChallengeToken(
            fixture.KnownUserId.ToString(),
            [WarehouseRoles.WarehouseManager],
            10);

        var backupCode = enrollment.Value.BackupCodes[0];
        var verify = await sut.VerifyChallengeAsync(new MfaVerifyRequest(challengeToken, null, backupCode));

        verify.IsSuccess.Should().BeTrue();
        verify.Value.RemainingBackupCodes.Should().Be(9);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyChallengeAsync_WhenBackupCodeReused_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var enrollment = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var code = CreateTotp(enrollment.Value.ManualSecret);
        _ = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, code);

        var backupCode = enrollment.Value.BackupCodes[0];
        var challengeToken1 = fixture.SessionTokenService.IssueChallengeToken(
            fixture.KnownUserId.ToString(),
            [WarehouseRoles.WarehouseManager],
            10);
        var first = await sut.VerifyChallengeAsync(new MfaVerifyRequest(challengeToken1, null, backupCode));

        var challengeToken2 = fixture.SessionTokenService.IssueChallengeToken(
            fixture.KnownUserId.ToString(),
            [WarehouseRoles.WarehouseManager],
            10);
        var second = await sut.VerifyChallengeAsync(new MfaVerifyRequest(challengeToken2, null, backupCode));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task VerifyChallengeAsync_WhenTooManyFailures_ShouldLockTemporarily()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db, new MfaOptions
        {
            MaxFailedAttempts = 2,
            LockoutMinutes = 5,
            RequiredRoles = [WarehouseRoles.WarehouseManager]
        });

        var enrollment = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var enrollCode = CreateTotp(enrollment.Value.ManualSecret);
        _ = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, enrollCode);

        var challengeToken = fixture.SessionTokenService.IssueChallengeToken(
            fixture.KnownUserId.ToString(),
            [WarehouseRoles.WarehouseManager],
            10);

        _ = await sut.VerifyChallengeAsync(new MfaVerifyRequest(challengeToken, "000000", null));
        var second = await sut.VerifyChallengeAsync(new MfaVerifyRequest(challengeToken, "111111", null));
        var valid = await sut.VerifyChallengeAsync(new MfaVerifyRequest(challengeToken, CreateTotp(enrollment.Value.ManualSecret), null));

        second.IsSuccess.Should().BeFalse();
        valid.IsSuccess.Should().BeFalse();
        valid.ErrorDetail.Should().ContainEquivalentOf("locked");
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task RegenerateBackupCodesAsync_WhenMfaNotEnabled_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        _ = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        var result = await sut.RegenerateBackupCodesAsync(fixture.KnownUserId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task RegenerateBackupCodesAsync_WhenMfaEnabled_ShouldReturnTenCodes()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var enrollment = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        _ = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, CreateTotp(enrollment.Value.ManualSecret));

        var result = await sut.RegenerateBackupCodesAsync(fixture.KnownUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(10);
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task ResetAsync_WhenMfaConfigured_ShouldDisableAndClearSecrets()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var enrollment = await sut.EnrollAsync(fixture.KnownUserId, "user@example.com");
        _ = await sut.VerifyEnrollmentAsync(fixture.KnownUserId, CreateTotp(enrollment.Value.ManualSecret));

        var reset = await sut.ResetAsync(fixture.KnownUserId);
        var status = await sut.GetStatusAsync(fixture.KnownUserId);

        reset.IsSuccess.Should().BeTrue();
        status.MfaEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "MFA")]
    public async Task GetStatusAsync_WhenNotEnrolled_ShouldReturnDefaultState()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var status = await sut.GetStatusAsync(fixture.KnownUserId);

        status.HasEnrollment.Should().BeFalse();
        status.MfaEnabled.Should().BeFalse();
        status.BackupCodeCount.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "MFA")]
    public void IsMfaRequired_ShouldRespectConfiguredRoles()
    {
        var fixture = new TestFixture();
        using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db, new MfaOptions
        {
            RequiredRoles = [WarehouseRoles.WarehouseAdmin]
        });

        sut.IsMfaRequired([WarehouseRoles.WarehouseAdmin]).Should().BeTrue();
        sut.IsMfaRequired([WarehouseRoles.Operator]).Should().BeFalse();
    }

    private static string CreateTotp(string manualSecret)
    {
        var totp = new Totp(Base32Encoding.ToBytes(manualSecret));
        return totp.ComputeTotp(DateTime.UtcNow);
    }

    private sealed class TestFixture
    {
        public TestFixture()
        {
            UserStore = new InMemoryAdminUserStore();
            KnownUserId = UserStore.GetAll().First().Id;
            SessionTokenService = new MfaSessionTokenService();
        }

        public InMemoryAdminUserStore UserStore { get; }
        public Guid KnownUserId { get; }
        public MfaSessionTokenService SessionTokenService { get; }

        public WarehouseDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseInMemoryDatabase($"mfa-tests-{Guid.NewGuid():N}")
                .Options;

            return new WarehouseDbContext(options);
        }

        public MfaService CreateService(WarehouseDbContext dbContext, MfaOptions? options = null)
        {
            return new MfaService(
                dbContext,
                UserStore,
                new EphemeralDataProtectionProvider(),
                SessionTokenService,
                new StaticOptionsMonitor<MfaOptions>(options ?? new MfaOptions()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<MfaService>.Instance);
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
