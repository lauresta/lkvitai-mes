using System.Net;
using System.Net.Http;
using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class OAuthControllerTests
{
    [Fact]
    [Trait("Category", "OAuth")]
    public async Task LoginAsync_WhenOAuthDisabled_ShouldReturnBadRequest()
    {
        var sut = CreateController(new OAuthOptions { Enabled = false });

        var result = await sut.LoginAsync();

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task LoginAsync_WhenConfigured_ShouldRedirectToProviderAuthorizeEndpoint()
    {
        var sut = CreateController(
            CreateOptions(),
            authorizationEndpoint: "https://issuer.example.com/authorize");

        var result = await sut.LoginAsync("/warehouse/admin/settings");

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("https://issuer.example.com/authorize");
        redirect.Url.Should().Contain("code_challenge=");
        redirect.Url.Should().Contain("state=");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task CallbackAsync_WhenCodeMissing_ShouldReturnBadRequest()
    {
        var sut = CreateController(CreateOptions());

        var result = await sut.CallbackAsync(null, "state");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task CallbackAsync_WhenStateInvalid_ShouldReturnBadRequest()
    {
        var sut = CreateController(CreateOptions());

        var result = await sut.CallbackAsync("code", "unknown-state");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task CallbackAsync_WhenTokenValidationFails_ShouldReturnUnauthorized()
    {
        var options = CreateOptions();
        var stateStore = new OAuthLoginStateStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var state = stateStore.Create("/return");

        var tokenValidator = new Mock<IOAuthTokenValidator>(MockBehavior.Strict);
        tokenValidator
            .Setup(x => x.ValidateAsync("jwt-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthTokenValidationResult(false, null, "Token expired", string.Empty, [], null));

        var sut = CreateController(
            options,
            stateStore: stateStore,
            tokenValidator: tokenValidator.Object,
            tokenEndpointHandler: _ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"jwt-token\",\"id_token\":\"jwt-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}")
                });

        var result = await sut.CallbackAsync("auth-code", state.State);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public async Task CallbackAsync_WhenValidationAndProvisioningSucceed_ShouldReturnTokenResponse()
    {
        var options = CreateOptions();
        var stateStore = new OAuthLoginStateStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var state = stateStore.Create("/return");

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "oauth-user-1"),
            new Claim(ClaimTypes.Role, WarehouseRoles.WarehouseManager)
        ]));

        var tokenValidator = new Mock<IOAuthTokenValidator>(MockBehavior.Strict);
        tokenValidator
            .Setup(x => x.ValidateAsync("jwt-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthTokenValidationResult(
                true,
                principal,
                string.Empty,
                "oauth-user-1",
                [WarehouseRoles.WarehouseManager],
                DateTimeOffset.UtcNow.AddHours(8)));

        var provisioning = new Mock<IOAuthUserProvisioningService>(MockBehavior.Strict);
        provisioning
            .Setup(x => x.Provision(principal, It.IsAny<IReadOnlyList<string>>()))
            .Returns(new OAuthProvisioningResult(true, "oauth-user-1", [WarehouseRoles.WarehouseManager], null));

        var sut = CreateController(
            options,
            stateStore: stateStore,
            tokenValidator: tokenValidator.Object,
            userProvisioningService: provisioning.Object,
            tokenEndpointHandler: _ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"jwt-token\",\"id_token\":\"jwt-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}")
                });

        var result = await sut.CallbackAsync("auth-code", state.State);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<OAuthController.OAuthCallbackResponse>().Subject;
        payload.Token.Should().Be("jwt-token");
        payload.UserId.Should().Be("oauth-user-1");
        payload.Roles.Should().Contain(WarehouseRoles.WarehouseManager);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void Logout_ShouldReturnNoContent()
    {
        var sut = CreateController(CreateOptions());

        var result = sut.Logout();

        result.Should().BeOfType<NoContentResult>();
    }

    private static OAuthController CreateController(
        OAuthOptions options,
        string authorizationEndpoint = "https://issuer.example.com/authorize",
        OAuthLoginStateStore? stateStore = null,
        IOAuthTokenValidator? tokenValidator = null,
        IOAuthUserProvisioningService? userProvisioningService = null,
        IMfaService? mfaService = null,
        IMfaSessionTokenService? mfaSessionTokenService = null,
        Func<HttpRequestMessage, HttpResponseMessage>? tokenEndpointHandler = null)
    {
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = "https://issuer.example.com",
            AuthorizationEndpoint = authorizationEndpoint,
            TokenEndpoint = "https://issuer.example.com/token"
        };

        var configProvider = new Mock<IOAuthOpenIdConfigurationProvider>(MockBehavior.Strict);
        configProvider
            .Setup(x => x.GetConfigurationAsync(It.IsAny<OAuthOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        var state = stateStore ?? new OAuthLoginStateStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        var validator = tokenValidator ?? new Mock<IOAuthTokenValidator>(MockBehavior.Strict).Object;
        var provisioning = userProvisioningService ?? new Mock<IOAuthUserProvisioningService>(MockBehavior.Strict).Object;
        var mfa = mfaService ?? BuildDefaultMfaService();
        var sessionTokenService = mfaSessionTokenService ?? new MfaSessionTokenService();

        var httpFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        if (tokenEndpointHandler is not null)
        {
            var client = new HttpClient(new StubHttpMessageHandler(tokenEndpointHandler));
            httpFactory.Setup(x => x.CreateClient("OAuthProvider")).Returns(client);
        }
        else
        {
            httpFactory.Setup(x => x.CreateClient("OAuthProvider")).Returns(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest))));
        }

        var controller = new OAuthController(
            configProvider.Object,
            state,
            validator,
            provisioning,
            mfa,
            sessionTokenService,
            new StaticOptionsMonitor<OAuthOptions>(options),
            httpFactory.Object,
            NullLoggerFactory.Instance.CreateLogger<OAuthController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost", 5000);

        return controller;
    }

    private static IMfaService BuildDefaultMfaService()
    {
        var mock = new Mock<IMfaService>(MockBehavior.Strict);
        mock.Setup(x => x.IsMfaRequired(It.IsAny<IReadOnlyList<string>>())).Returns(false);
        return mock.Object;
    }

    private static OAuthOptions CreateOptions()
    {
        return new OAuthOptions
        {
            Enabled = true,
            Provider = "AzureAD",
            Authority = "https://issuer.example.com",
            ClientId = "warehouse-client",
            ClientSecret = "secret",
            Scope = "openid profile email",
            CallbackPath = "/api/auth/oauth/callback",
            DefaultRole = WarehouseRoles.Operator,
            RoleMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Warehouse-Managers"] = WarehouseRoles.WarehouseManager
            }
        };
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
