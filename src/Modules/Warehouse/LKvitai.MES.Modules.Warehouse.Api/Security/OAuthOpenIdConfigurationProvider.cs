using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace LKvitai.MES.Modules.Warehouse.Api.Security;

public interface IOAuthOpenIdConfigurationProvider
{
    Task<OpenIdConnectConfiguration> GetConfigurationAsync(OAuthOptions options, CancellationToken cancellationToken = default);
}

public sealed class OAuthOpenIdConfigurationProvider : IOAuthOpenIdConfigurationProvider
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<OAuthOpenIdConfigurationProvider> _logger;

    public OAuthOpenIdConfigurationProvider(
        IMemoryCache cache,
        ILogger<OAuthOpenIdConfigurationProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        OAuthOptions options,
        CancellationToken cancellationToken = default)
    {
        var authority = NormalizeAuthority(options.Authority);
        var cacheKey = $"oauth:metadata:{authority}";

        if (_cache.TryGetValue(cacheKey, out OpenIdConnectConfiguration? cached) && cached is not null)
        {
            return cached;
        }

        var metadataAddress = BuildMetadataAddress(authority);
        var documentRetriever = new HttpDocumentRetriever
        {
            RequireHttps = !options.AllowInsecureMetadata
        };

        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            documentRetriever);

        var configuration = await manager.GetConfigurationAsync(cancellationToken);

        _cache.Set(
            cacheKey,
            configuration,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });

        _logger.LogInformation(
            "OAuth metadata loaded and cached. Authority={Authority}, Issuer={Issuer}",
            authority,
            configuration.Issuer);

        return configuration;
    }

    private static string NormalizeAuthority(string authority)
    {
        return authority.Trim().TrimEnd('/');
    }

    private static string BuildMetadataAddress(string authority)
    {
        if (authority.EndsWith(".well-known/openid-configuration", StringComparison.OrdinalIgnoreCase))
        {
            return authority;
        }

        return $"{authority}/.well-known/openid-configuration";
    }
}
