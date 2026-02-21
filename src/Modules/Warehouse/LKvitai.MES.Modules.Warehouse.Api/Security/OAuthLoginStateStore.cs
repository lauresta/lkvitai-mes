using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace LKvitai.MES.Modules.Warehouse.Api.Security;

public interface IOAuthLoginStateStore
{
    OAuthLoginState Create(string? returnUrl);
    OAuthLoginState? Consume(string state);
}

public sealed class OAuthLoginStateStore : IOAuthLoginStateStore
{
    private readonly IMemoryCache _memoryCache;

    public OAuthLoginStateStore(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public OAuthLoginState Create(string? returnUrl)
    {
        var state = GenerateUrlSafeValue(32);
        var codeVerifier = GenerateUrlSafeValue(64);
        var loginState = new OAuthLoginState(state, codeVerifier, returnUrl, DateTimeOffset.UtcNow);

        _memoryCache.Set(
            BuildCacheKey(state),
            loginState,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

        return loginState;
    }

    public OAuthLoginState? Consume(string state)
    {
        var cacheKey = BuildCacheKey(state);
        if (!_memoryCache.TryGetValue(cacheKey, out OAuthLoginState? loginState) || loginState is null)
        {
            return null;
        }

        _memoryCache.Remove(cacheKey);
        return loginState;
    }

    public static string BuildCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string BuildCacheKey(string state)
        => $"oauth:state:{state}";

    private static string GenerateUrlSafeValue(int byteCount)
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    private static string Base64UrlEncode(byte[] input)
    {
        var encoded = Convert.ToBase64String(input);
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
