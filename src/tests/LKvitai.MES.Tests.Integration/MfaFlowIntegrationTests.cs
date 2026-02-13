using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class MfaFlowIntegrationTests
{
    [Fact]
    [Trait("Category", "MFA")]
    public async Task OAuthCallbackThenEnrollAndVerifyMfa_ShouldIssueMfaAccessToken()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "oauth-mfa-test-key" };

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

        var jwt = CreateJwt(signingKey, openIdConfig.Issuer, options.ClientId, new[]
        {
            new Claim("sub", "oauth-user-mfa"),
            new Claim("preferred_username", "oauth.user.mfa"),
            new Claim("email", "oauth.user.mfa@example.com"),
            new Claim("groups", "Warehouse-Managers")
        });

        var configurationProvider = new StubConfigurationProvider(openIdConfig);
        using var stateCache = new MemoryCache(new MemoryCacheOptions());
        var stateStore = new OAuthLoginStateStore(stateCache);

        var validator = new OAuthTokenValidator(
            configurationProvider,
            new OAuthRoleMapper(),
            new StaticOptionsMonitor<OAuthOptions>(options),
            NullLogger<OAuthTokenValidator>.Instance);

        var adminUserStore = new InMemoryAdminUserStore();
        var provisioning = new OAuthUserProvisioningService(
            adminUserStore,
            NullLogger<OAuthUserProvisioningService>.Instance);

        await using var dbContext = CreateDbContext();
        var mfaOptions = new MfaOptions
        {
            RequiredRoles = [WarehouseRoles.WarehouseManager],
            ChallengeTimeoutMinutes = 10,
            SessionTimeoutHours = 8,
            MaxFailedAttempts = 5,
            LockoutMinutes = 15
        };
        var sessionTokenService = new MfaSessionTokenService();
        var mfaService = new MfaService(
            dbContext,
            adminUserStore,
            new EphemeralDataProtectionProvider(),
            sessionTokenService,
            new StaticOptionsMonitor<MfaOptions>(mfaOptions),
            NullLogger<MfaService>.Instance);

        var httpFactory = new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"access_token\":\"{jwt}\",\"id_token\":\"{jwt}\",\"token_type\":\"Bearer\",\"expires_in\":3600}}")
            })));

        var controller = new OAuthController(
            configurationProvider,
            stateStore,
            validator,
            provisioning,
            mfaService,
            sessionTokenService,
            new StaticOptionsMonitor<OAuthOptions>(options),
            httpFactory,
            NullLogger<OAuthController>.Instance)
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
        var query = QueryHelpers.ParseQuery(uri.Query);
        var state = query["state"].ToString();

        var callback = await controller.CallbackAsync("auth-code", state);

        var ok = callback.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<OAuthController.OAuthCallbackResponse>().Subject;
        payload.MfaRequired.Should().BeTrue();
        payload.MfaEnrollmentRequired.Should().BeTrue();
        payload.ChallengeToken.Should().NotBeNullOrWhiteSpace();

        Guid.TryParse(payload.UserId, out var userId).Should().BeTrue();
        var enroll = await mfaService.EnrollAsync(userId, "oauth.user.mfa@example.com");
        enroll.IsSuccess.Should().BeTrue();

        var enrollmentCode = CreateTotp(enroll.Value.ManualSecret);
        var verifyEnrollment = await mfaService.VerifyEnrollmentAsync(userId, enrollmentCode);
        verifyEnrollment.IsSuccess.Should().BeTrue();

        var verify = await mfaService.VerifyChallengeAsync(new MfaVerifyRequest(
            payload.ChallengeToken!,
            CreateTotp(enroll.Value.ManualSecret),
            null));

        verify.IsSuccess.Should().BeTrue();
        sessionTokenService.TryParseToken(verify.Value.AccessToken, out var parsed).Should().BeTrue();
        parsed.MfaVerified.Should().BeTrue();
        parsed.AuthSource.Should().Be("oauth");
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"mfa-flow-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
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

    private static string CreateTotp(string manualSecret)
    {
        var totp = new Totp(Base32Encoding.ToBytes(manualSecret));
        return totp.ComputeTotp(DateTime.UtcNow);
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
}
