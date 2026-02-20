using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class OAuthTokenValidatorTests
{
    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenOAuthDisabled_ShouldFail()
    {
        var fixture = new TestFixture(new OAuthOptions { Enabled = false });

        var result = await fixture.Validator.ValidateAsync("token");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("OAuth is not enabled");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenProviderUnsupported_ShouldFail()
    {
        var fixture = new TestFixture(new OAuthOptions
        {
            Enabled = true,
            Provider = "Custom",
            Authority = fixtureAuthority,
            ClientId = fixtureClientId
        });

        var result = await fixture.Validator.ValidateAsync("token");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("AzureAD or Okta");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenAuthorityMissing_ShouldFail()
    {
        var fixture = new TestFixture(new OAuthOptions
        {
            Enabled = true,
            Provider = "AzureAD",
            Authority = string.Empty,
            ClientId = fixtureClientId
        });

        var result = await fixture.Validator.ValidateAsync("token");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("authority");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenClientIdMissing_ShouldFail()
    {
        var fixture = new TestFixture(new OAuthOptions
        {
            Enabled = true,
            Provider = "AzureAD",
            Authority = fixtureAuthority,
            ClientId = string.Empty
        });

        var result = await fixture.Validator.ValidateAsync("token");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("client id");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenTokenExpired_ShouldFailWithTokenExpired()
    {
        var fixture = TestFixture.CreateEnabled();
        var token = fixture.CreateSignedToken(
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-5),
            claims: [new Claim("sub", "expired-user")]);

        var result = await fixture.Validator.ValidateAsync(token);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Token expired");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenIssuerInvalid_ShouldFail()
    {
        var fixture = TestFixture.CreateEnabled();
        var token = fixture.CreateSignedToken(issuer: "https://unexpected-issuer", claims: [new Claim("sub", "bad-issuer")]);

        var result = await fixture.Validator.ValidateAsync(token);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenAudienceInvalid_ShouldFail()
    {
        var fixture = TestFixture.CreateEnabled();
        var token = fixture.CreateSignedToken(audience: "wrong-audience", claims: [new Claim("sub", "bad-audience")]);

        var result = await fixture.Validator.ValidateAsync(token);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenSignatureInvalid_ShouldFail()
    {
        var fixture = TestFixture.CreateEnabled();
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa) { KeyId = "different-key" };
        var token = fixture.CreateSignedToken(signingKey: otherKey, claims: [new Claim("sub", "bad-signature")]);

        var result = await fixture.Validator.ValidateAsync(token);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenValidTokenWithMappedGroup_ShouldSucceedAndMapRole()
    {
        var fixture = TestFixture.CreateEnabled();
        var token = fixture.CreateSignedToken(claims:
        [
            new Claim("sub", "user-1"),
            new Claim("groups", "Warehouse-Managers")
        ]);

        var result = await fixture.Validator.ValidateAsync(token);

        result.IsSuccess.Should().BeTrue();
        result.UserId.Should().Be("user-1");
        result.Roles.Should().Contain(WarehouseRoles.WarehouseManager);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenValidTokenWithoutRoleClaims_ShouldUseDefaultRole()
    {
        var fixture = TestFixture.CreateEnabled();
        var token = fixture.CreateSignedToken(claims: [new Claim("sub", "user-2")]);

        var result = await fixture.Validator.ValidateAsync(token);

        result.IsSuccess.Should().BeTrue();
        result.Roles.Should().Contain(WarehouseRoles.Operator);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenSubMissingAndNameIdentifierPresent_ShouldUseNameIdentifier()
    {
        var fixture = TestFixture.CreateEnabled();
        var token = fixture.CreateSignedToken(claims: [new Claim(ClaimTypes.NameIdentifier, "name-id-user")]);

        var result = await fixture.Validator.ValidateAsync(token);

        result.IsSuccess.Should().BeTrue();
        result.UserId.Should().Be("name-id-user");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task ValidateAsync_WhenRoleAliasAdmin_ShouldAddWarehouseAdmin()
    {
        var fixture = TestFixture.CreateEnabled();
        var token = fixture.CreateSignedToken(claims: [new Claim("role", "Admin"), new Claim("sub", "admin-user")]);

        var result = await fixture.Validator.ValidateAsync(token);

        result.IsSuccess.Should().BeTrue();
        result.Roles.Should().Contain("Admin");
        result.Roles.Should().Contain(WarehouseRoles.WarehouseAdmin);
    }

    private const string fixtureAuthority = "https://issuer.example.com";
    private const string fixtureClientId = "warehouse-client";

    private sealed class TestFixture
    {
        public TestFixture(OAuthOptions options)
        {
            Rsa = RSA.Create(2048);
            SigningKey = new RsaSecurityKey(Rsa) { KeyId = "test-key" };

            Configuration = new OpenIdConnectConfiguration
            {
                Issuer = fixtureAuthority,
                AuthorizationEndpoint = $"{fixtureAuthority}/authorize",
                TokenEndpoint = $"{fixtureAuthority}/token"
            };
            Configuration.SigningKeys.Add(SigningKey);

            Options = options;
            if (Options.RoleMappings.Count == 0)
            {
                Options.RoleMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Warehouse-Managers"] = WarehouseRoles.WarehouseManager,
                    ["Warehouse-Admins"] = WarehouseRoles.WarehouseAdmin
                };
            }

            ConfigProvider = new StubConfigurationProvider(Configuration);
            Validator = new OAuthTokenValidator(
                ConfigProvider,
                new OAuthRoleMapper(),
                new StaticOptionsMonitor<OAuthOptions>(Options),
                NullLoggerFactory.Instance.CreateLogger<OAuthTokenValidator>());
        }

        public OAuthTokenValidator Validator { get; }
        public OAuthOptions Options { get; }
        public OpenIdConnectConfiguration Configuration { get; }
        public IOAuthOpenIdConfigurationProvider ConfigProvider { get; }
        public RSA Rsa { get; }
        public RsaSecurityKey SigningKey { get; }

        public string CreateSignedToken(
            string? issuer = null,
            string? audience = null,
            SecurityKey? signingKey = null,
            DateTime? expiresAtUtc = null,
            IReadOnlyList<Claim>? claims = null)
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = issuer ?? fixtureAuthority,
                Audience = audience ?? fixtureClientId,
                IssuedAt = DateTime.UtcNow.AddHours(-1),
                NotBefore = DateTime.UtcNow.AddHours(-1),
                Expires = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(20),
                Subject = new ClaimsIdentity(claims ?? [new Claim("sub", "oauth-user")]),
                SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256)
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(descriptor);
            return handler.WriteToken(token);
        }

        public static TestFixture CreateEnabled()
        {
            return new TestFixture(new OAuthOptions
            {
                Enabled = true,
                Provider = "AzureAD",
                Authority = fixtureAuthority,
                ClientId = fixtureClientId,
                DefaultRole = WarehouseRoles.Operator,
                RoleClaimType = "groups",
                RoleMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Warehouse-Managers"] = WarehouseRoles.WarehouseManager,
                    ["Warehouse-Admins"] = WarehouseRoles.WarehouseAdmin
                }
            });
        }
    }

    private sealed class StubConfigurationProvider : IOAuthOpenIdConfigurationProvider
    {
        private readonly OpenIdConnectConfiguration _configuration;

        public StubConfigurationProvider(OpenIdConnectConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(OAuthOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(_configuration);
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
