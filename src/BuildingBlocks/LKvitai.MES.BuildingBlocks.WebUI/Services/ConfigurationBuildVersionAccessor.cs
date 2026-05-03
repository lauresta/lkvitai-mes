using Microsoft.Extensions.Configuration;

namespace LKvitai.MES.BuildingBlocks.WebUI.Services;

/// <summary>
/// Reads the deployed build identity from <see cref="IConfiguration"/>. The
/// keys (<c>APP_VERSION</c>, <c>GIT_SHA</c>, <c>BUILD_DATE</c>) are uppercase
/// because they're shipped as Linux env vars by each WebUI Dockerfile;
/// ASP.NET Core's environment-variable provider surfaces them on
/// <see cref="IConfiguration"/> with that exact casing.
///
/// <para>
/// The build pipeline (<c>build-and-push.yml</c>) computes:
///   <list type="bullet">
///     <item><c>APP_VERSION</c> — the latest tag, falling back to a short SHA.</item>
///     <item><c>GIT_SHA</c> — the full commit hash (we shorten it to 7 chars
///       in <see cref="BuildVersion.ShortSha"/>).</item>
///     <item><c>BUILD_DATE</c> — ISO-8601 UTC timestamp emitted by
///       <c>date -u +'%Y-%m-%dT%H:%M:%SZ'</c>.</item>
///   </list>
/// </para>
///
/// Robust against malformed dates: a parse failure logs nothing and returns
/// <c>null</c> for <see cref="BuildVersion.BuildDate"/>, so a corrupted
/// <c>BUILD_DATE</c> never takes down the topbar render.
/// </summary>
internal sealed class ConfigurationBuildVersionAccessor : IBuildVersionAccessor
{
    private readonly IConfiguration _configuration;

    public ConfigurationBuildVersionAccessor(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public BuildVersion Get()
    {
        var version = TrimToNull(_configuration["APP_VERSION"]);
        var sha     = TrimToNull(_configuration["GIT_SHA"]);
        var rawDate = TrimToNull(_configuration["BUILD_DATE"]);

        DateTimeOffset? builtAt = null;
        if (rawDate is not null && DateTimeOffset.TryParse(
                rawDate,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            builtAt = parsed;
        }

        return new BuildVersion(version, sha, builtAt);
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
