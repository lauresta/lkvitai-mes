using Microsoft.Extensions.Options;

namespace LKvitai.MES.Api.Security;

public sealed class DevAuthService : IDevAuthService
{
    private readonly DevAuthOptions _options;
    private readonly ILogger<DevAuthService> _logger;

    public DevAuthService(
        IOptions<DevAuthOptions> options,
        ILogger<DevAuthService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public DevTokenResponse? GenerateToken(DevTokenRequest request)
    {
        if (!string.Equals(request.Username, _options.Username, StringComparison.Ordinal) ||
            !string.Equals(request.Password, _options.Password, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Dev token rejected: invalid credentials for user {Username}",
                SensitiveDataMasker.MaskText(request.Username));
            return null;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddHours(Math.Max(1, _options.TokenLifetimeHours));
        var token = $"{_options.UserId}|{_options.Roles}|{expiresAt.ToUnixTimeSeconds()}";

        _logger.LogInformation(
            "Dev token generated for user {Username}, expires {ExpiresAt}",
            SensitiveDataMasker.MaskText(request.Username),
            expiresAt);

        return new DevTokenResponse(token, expiresAt);
    }
}
