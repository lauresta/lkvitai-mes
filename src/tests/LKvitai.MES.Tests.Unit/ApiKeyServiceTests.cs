using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class ApiKeyServiceTests
{
    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_WhenValid_ShouldReturnPlainKeyAndPersist()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.CreateAsync(
            new CreateApiKeyRequest("ERP Integration", ["read:items", "write:orders"], 120, null),
            "admin-user");

        result.IsSuccess.Should().BeTrue();
        result.Value.PlainKey.Should().StartWith("wh_");
        result.Value.Scopes.Should().Contain("read:items");
        result.Value.RateLimitPerMinute.Should().Be(120);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_ShouldStoreHashNotPlainText()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var create = await sut.CreateAsync(new CreateApiKeyRequest("WMS", ["read:items"], 100, null), "admin");

        var entity = await db.ApiKeys.SingleAsync();
        entity.KeyHash.Should().NotBe(create.Value.PlainKey);
        entity.KeyHash.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_WhenNameMissing_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.CreateAsync(new CreateApiKeyRequest(" ", ["read:items"], 100, null), "admin");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_WhenScopesMissing_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.CreateAsync(new CreateApiKeyRequest("ERP", [], 100, null), "admin");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_WhenScopeUnsupported_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["write:payments"], 100, null), "admin");

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("Unsupported scopes");
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_WhenRateLimitNotProvided_ShouldUseDefault100()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], null, null), "admin");

        result.IsSuccess.Should().BeTrue();
        result.Value.RateLimitPerMinute.Should().Be(100);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task CreateAsync_WhenDuplicateName_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        _ = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], 100, null), "admin");
        var second = await sut.CreateAsync(new CreateApiKeyRequest("erp", ["read:items"], 100, null), "admin");

        second.IsSuccess.Should().BeFalse();
        second.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task GetAllAsync_ShouldReturnCreatedKeys()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        _ = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], 100, null), "admin");
        _ = await sut.CreateAsync(new CreateApiKeyRequest("MES", ["read:stock"], 100, null), "admin");

        var all = await sut.GetAllAsync();

        all.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task ValidateAsync_WhenKeyValid_ShouldSucceed()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var created = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], 100, null), "admin");
        var validation = await sut.ValidateAsync(created.Value.PlainKey);

        validation.IsSuccess.Should().BeTrue();
        validation.Scopes.Should().Contain("read:items");
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task ValidateAsync_WhenKeyMissing_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var validation = await sut.ValidateAsync(" ");

        validation.IsSuccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task ValidateAsync_WhenKeyUnknown_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var validation = await sut.ValidateAsync("wh_unknown");

        validation.IsSuccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task ValidateAsync_WhenKeyExpired_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var created = await sut.CreateAsync(
            new CreateApiKeyRequest("ERP", ["read:items"], 100, DateTimeOffset.UtcNow.AddMinutes(-1)),
            "admin");

        var validation = await sut.ValidateAsync(created.Value.PlainKey);

        validation.IsSuccess.Should().BeFalse();
        validation.ErrorMessage.Should().Contain("expired");
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task ValidateAsync_ShouldUpdateLastUsedAt()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var created = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], 100, null), "admin");
        _ = await sut.ValidateAsync(created.Value.PlainKey);

        var entity = await db.ApiKeys.SingleAsync();
        entity.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task RotateAsync_WhenKeyExists_ShouldReturnNewPlainKeyAndGraceWindow()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var created = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], 100, null), "admin");
        var rotated = await sut.RotateAsync(created.Value.Id, "admin");

        rotated.IsSuccess.Should().BeTrue();
        rotated.Value.PlainKey.Should().NotBe(created.Value.PlainKey);
        rotated.Value.PreviousKeyGraceUntil.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task RotateAsync_OldKeyShouldRemainValidDuringGracePeriod()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var created = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], 100, null), "admin");
        _ = await sut.RotateAsync(created.Value.Id, "admin");

        var oldKeyValidation = await sut.ValidateAsync(created.Value.PlainKey);

        oldKeyValidation.IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task RotateAsync_OldKeyShouldFailAfterGracePeriodExpires()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var created = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], 100, null), "admin");
        _ = await sut.RotateAsync(created.Value.Id, "admin");

        var entity = await db.ApiKeys.SingleAsync();
        entity.PreviousKeyGraceUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var oldKeyValidation = await sut.ValidateAsync(created.Value.PlainKey);

        oldKeyValidation.IsSuccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task RotateAsync_WhenKeyMissing_ShouldFailNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.RotateAsync(9999, "admin");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task DeleteAsync_WhenKeyExists_ShouldDeactivate()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var created = await sut.CreateAsync(new CreateApiKeyRequest("ERP", ["read:items"], 100, null), "admin");
        var delete = await sut.DeleteAsync(created.Value.Id);
        var validation = await sut.ValidateAsync(created.Value.PlainKey);

        delete.IsSuccess.Should().BeTrue();
        validation.IsSuccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task DeleteAsync_WhenMissing_ShouldFailNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var sut = fixture.CreateService(db);

        var result = await sut.DeleteAsync(9999);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    private sealed class TestFixture
    {
        public WarehouseDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseInMemoryDatabase($"api-key-tests-{Guid.NewGuid():N}")
                .Options;

            return new WarehouseDbContext(options);
        }

        public ApiKeyService CreateService(WarehouseDbContext dbContext)
        {
            return new ApiKeyService(
                dbContext,
                new MemoryCache(new MemoryCacheOptions()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ApiKeyService>.Instance);
        }
    }
}
