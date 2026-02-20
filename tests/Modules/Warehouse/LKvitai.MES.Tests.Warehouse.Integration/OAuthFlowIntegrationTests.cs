using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class OAuthFlowIntegrationTests
{
    [Fact]
    [Trait("Category", "OAuth")]
    public async Task LoginThenCallback_WithMockProvider_ShouldProvisionUserAndReturnToken()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "oauth-test-key" };

        var openIdConfig = new OpenIdConnectConfiguration
        {
            Issuer = "https://mock-issuer.example.com",
            AuthorizationEndpoint = "https://mock-issuer.example.com/authorize",
            TokenEndpoint = "https://mock-issuer.example.com/token"
        };
        openIdConfig.SigningKeys.Add(signingKey);

        var options = new OAuthOptions
        {
            Enabled = true,
            Provider = "AzureAD",
            Authority = "https://mock-issuer.example.com",
            ClientId = "warehouse-client",
            ClientSecret = "secret",
            Scope = "openid profile email",
            CallbackPath = "/api/auth/oauth/callback",
            RoleClaimType = "groups",
            DefaultRole = WarehouseRoles.Operator,
            RoleMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Warehouse-Managers"] = WarehouseRoles.WarehouseManager
            }
        };

        var token = CreateJwt(signingKey, openIdConfig.Issuer, options.ClientId, new[]
        {
            new Claim("sub", "oauth-user-42"),
            new Claim("preferred_username", "oauth.user"),
            new Claim("email", "oauth.user@example.com"),
            new Claim("groups", "Warehouse-Managers")
        });

        var configProvider = new StubConfigurationProvider(openIdConfig);
        using var stateCache = new MemoryCache(new MemoryCacheOptions());
        var stateStore = new OAuthLoginStateStore(stateCache);

        var validator = new OAuthTokenValidator(
            configProvider,
            new OAuthRoleMapper(),
            new StaticOptionsMonitor<OAuthOptions>(options),
            NullLoggerFactory.Instance.CreateLogger<OAuthTokenValidator>());

        var adminUserStore = new InMemoryAdminUserStore();
        var provisioning = new OAuthUserProvisioningService(
            adminUserStore,
            NullLoggerFactory.Instance.CreateLogger<OAuthUserProvisioningService>());

        var httpFactory = new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"access_token\":\"{token}\",\"id_token\":\"{token}\",\"token_type\":\"Bearer\",\"expires_in\":3600}}")
            })));

        var controller = new OAuthController(
            configProvider,
            stateStore,
            validator,
            provisioning,
            new DisabledMfaService(),
            new MfaSessionTokenService(),
            new StaticOptionsMonitor<OAuthOptions>(options),
            httpFactory,
            NullLoggerFactory.Instance.CreateLogger<OAuthController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost", 5000);

        var login = await controller.LoginAsync("/warehouse/admin/settings");
        var redirect = login.Should().BeOfType<RedirectResult>().Subject;

        var uri = new Uri(redirect.Url!);
        var parsed = QueryHelpers.ParseQuery(uri.Query);
        var state = parsed["state"].ToString();

        var callback = await controller.CallbackAsync("auth-code", state);

        var ok = callback.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<OAuthController.OAuthCallbackResponse>().Subject;
        payload.UserId.Should().NotBeNullOrWhiteSpace();
        payload.Roles.Should().Contain(WarehouseRoles.WarehouseManager);

        adminUserStore.GetAll().Should().Contain(x => x.Username == "oauth.user");
    }

    private static string CreateJwt(SecurityKey signingKey, string issuer, string audience, IEnumerable<Claim> claims)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        });

        return handler.WriteToken(token);
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

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public StubHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    private sealed class DisabledMfaService : IMfaService
    {
        public Task<Result<MfaEnrollmentDto>> EnrollAsync(Guid userId, string userLabel, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> VerifyEnrollmentAsync(Guid userId, string code, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<MfaVerifyResultDto>> VerifyChallengeAsync(MfaVerifyRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<string>>> RegenerateBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> ResetAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MfaUserStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MfaUserStatusDto(false, false, null, 0));

        public bool IsMfaRequired(IReadOnlyList<string> roles) => false;

        public int GetChallengeTimeoutMinutes() => 10;

        public int GetSessionTimeoutHours() => 8;
    }
}
