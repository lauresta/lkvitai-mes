using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/auth/oauth")]
public sealed class OAuthController : ControllerBase
{
    private readonly IOAuthOpenIdConfigurationProvider _configurationProvider;
    private readonly IOAuthLoginStateStore _stateStore;
    private readonly IOAuthTokenValidator _tokenValidator;
    private readonly IOAuthUserProvisioningService _userProvisioningService;
    private readonly IMfaService _mfaService;
    private readonly IMfaSessionTokenService _mfaSessionTokenService;
    private readonly IOptionsMonitor<OAuthOptions> _optionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(
        IOAuthOpenIdConfigurationProvider configurationProvider,
        IOAuthLoginStateStore stateStore,
        IOAuthTokenValidator tokenValidator,
        IOAuthUserProvisioningService userProvisioningService,
        IMfaService mfaService,
        IMfaSessionTokenService mfaSessionTokenService,
        IOptionsMonitor<OAuthOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory,
        ILogger<OAuthController> logger)
    {
        _configurationProvider = configurationProvider;
        _stateStore = stateStore;
        _tokenValidator = tokenValidator;
        _userProvisioningService = userProvisioningService;
        _mfaService = mfaService;
        _mfaSessionTokenService = mfaSessionTokenService;
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromQuery] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var configError = ValidateConfiguration(options);
        if (configError is not null)
        {
            return ValidationFailure(configError);
        }

        var metadata = await _configurationProvider.GetConfigurationAsync(options, cancellationToken);
        var state = _stateStore.Create(returnUrl);
        var codeChallenge = OAuthLoginStateStore.BuildCodeChallenge(state.CodeVerifier);
        var redirectUri = BuildRedirectUri(options);

        var query = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = options.Scope,
            ["state"] = state.State,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var authorizeUrl = BuildUrl(metadata.AuthorizationEndpoint, query);

        _logger.LogInformation(
            "OAuth login initiated. Provider={Provider}, RedirectUri={RedirectUri}, ReturnUrl={ReturnUrl}",
            options.Provider,
            redirectUri,
            returnUrl ?? string.Empty);

        return Redirect(authorizeUrl);
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> CallbackAsync(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var configError = ValidateConfiguration(options);
        if (configError is not null)
        {
            return ValidationFailure(configError);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            _logger.LogWarning("OAuth callback rejected: missing code/state.");
            return ValidationFailure("OAuth callback requires code and state.");
        }

        var loginState = _stateStore.Consume(state);
        if (loginState is null)
        {
            _logger.LogWarning("OAuth callback rejected: invalid or expired state.");
            return ValidationFailure("OAuth state is invalid or expired.");
        }

        try
        {
            var metadata = await _configurationProvider.GetConfigurationAsync(options, cancellationToken);
            var redirectUri = BuildRedirectUri(options);
            var tokenResponse = await ExchangeAuthorizationCodeAsync(
                metadata.TokenEndpoint,
                code,
                loginState.CodeVerifier,
                redirectUri,
                options,
                cancellationToken);

            var jwtToken = tokenResponse.IdToken ?? tokenResponse.AccessToken;
            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                _logger.LogWarning("OAuth callback token exchange did not return JWT token.");
                return ValidationFailure("OAuth token exchange failed.");
            }

            var validation = await _tokenValidator.ValidateAsync(jwtToken, cancellationToken);
            if (!validation.IsSuccess || validation.Principal is null)
            {
                _logger.LogWarning("OAuth callback token validation failed. Error={Error}", validation.ErrorMessage);
                return UnauthorizedFailure(validation.ErrorMessage);
            }

            var provisioning = _userProvisioningService.Provision(validation.Principal, validation.Roles);
            if (!provisioning.IsSuccess)
            {
                _logger.LogWarning("OAuth user provisioning failed. Error={Error}", provisioning.Error ?? "unknown");
                return ValidationFailure(provisioning.Error ?? "OAuth user provisioning failed.");
            }

            _logger.LogInformation(
                "OAuth login successful. UserId={UserId}, Roles={Roles}, Provider={Provider}",
                provisioning.UserId,
                string.Join(",", provisioning.Roles),
                options.Provider);

            var expiresAt = validation.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(Math.Max(1, options.SessionTimeoutHours));
            if (Guid.TryParse(provisioning.UserId, out var userId) && _mfaService.IsMfaRequired(provisioning.Roles))
            {
                var mfaStatus = await _mfaService.GetStatusAsync(userId, cancellationToken);
                var challengeTimeout = _mfaService.GetChallengeTimeoutMinutes();
                var challengeToken = _mfaSessionTokenService.IssueChallengeToken(
                    provisioning.UserId,
                    provisioning.Roles,
                    challengeTimeout);

                _logger.LogInformation(
                    "OAuth login requires MFA. UserId={UserId}, EnrollmentRequired={EnrollmentRequired}",
                    provisioning.UserId,
                    !mfaStatus.MfaEnabled);

                return Ok(new OAuthCallbackResponse(
                    string.Empty,
                    DateTimeOffset.UtcNow.AddMinutes(challengeTimeout),
                    provisioning.UserId,
                    provisioning.Roles,
                    true,
                    !mfaStatus.MfaEnabled,
                    challengeToken));
            }

            return Ok(new OAuthCallbackResponse(jwtToken, expiresAt, provisioning.UserId, provisioning.Roles));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OAuth callback failed while calling token endpoint.");
            return ValidationFailure("OAuth token exchange failed.");
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        _logger.LogInformation("OAuth logout requested by {UserId}", User.Identity?.Name ?? "anonymous");
        return NoContent();
    }

    private async Task<OAuthTokenEndpointResponse> ExchangeAuthorizationCodeAsync(
        string tokenEndpoint,
        string code,
        string codeVerifier,
        string redirectUri,
        OAuthOptions options,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        };

        if (!string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            payload["client_secret"] = options.ClientSecret;
        }

        var client = _httpClientFactory.CreateClient("OAuthProvider");
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(payload)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "OAuth token endpoint returned non-success status. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                body);

            throw new HttpRequestException("OAuth token endpoint request failed.");
        }

        var model = await response.Content.ReadFromJsonAsync<OAuthTokenEndpointResponse>(cancellationToken: cancellationToken);
        return model ?? new OAuthTokenEndpointResponse();
    }

    private string BuildRedirectUri(OAuthOptions options)
    {
        if (Uri.TryCreate(options.CallbackPath, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        var callbackPath = options.CallbackPath.StartsWith('/')
            ? options.CallbackPath
            : $"/{options.CallbackPath}";

        return $"{Request.Scheme}://{Request.Host}{callbackPath}";
    }

    private static string? ValidateConfiguration(OAuthOptions options)
    {
        if (!options.Enabled)
        {
            return "OAuth is disabled.";
        }

        if (!string.Equals(options.Provider, "AzureAD", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.Provider, "Okta", StringComparison.OrdinalIgnoreCase))
        {
            return "OAuth provider must be AzureAD or Okta.";
        }

        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            return "OAuth Authority is required.";
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            return "OAuth ClientId is required.";
        }

        return null;
    }

    private static string BuildUrl(string baseUrl, IReadOnlyDictionary<string, string> query)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var pairs = query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}");
        return $"{baseUrl}{separator}{string.Join("&", pairs)}";
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private ObjectResult UnauthorizedFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Unauthorized,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };
    }

    public sealed record OAuthCallbackResponse(
        string Token,
        DateTimeOffset ExpiresAt,
        string UserId,
        IReadOnlyList<string> Roles,
        bool MfaRequired = false,
        bool MfaEnrollmentRequired = false,
        string? ChallengeToken = null);

    private sealed class OAuthTokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
