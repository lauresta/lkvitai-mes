using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class DevAuthServiceTests
{
    [Fact]
    public void GenerateToken_WithValidCredentials_ShouldReturnTokenAndExpiry()
    {
        var sut = CreateSut();

        var result = sut.GenerateToken(new DevTokenRequest("admin", "Admin123!"));

        result.Should().NotBeNull();
        result!.Token.Should().StartWith("admin-dev|");
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(23));
    }

    [Fact]
    public void GenerateToken_WithInvalidCredentials_ShouldReturnNull()
    {
        var sut = CreateSut();

        var result = sut.GenerateToken(new DevTokenRequest("admin", "wrong"));

        result.Should().BeNull();
    }

    private static DevAuthService CreateSut()
    {
        var options = Options.Create(new DevAuthOptions
        {
            Username = "admin",
            Password = "Admin123!",
            UserId = "admin-dev",
            Roles = "Operator,WarehouseAdmin",
            TokenLifetimeHours = 24
        });

        var logger = NullLoggerFactory.Instance.CreateLogger<DevAuthService>();
        return new DevAuthService(options, logger);
    }
}
