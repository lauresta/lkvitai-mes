namespace LKvitai.MES.BuildingBlocks.WebUI.Services;

/// <summary>
/// Snapshot of the build identity injected into a WebUI container at deploy
/// time. Read by every module's <c>MainLayout</c> so the shared
/// <see cref="Components.PortalModuleShell"/> env-badge can render a real
/// "VER" instead of the em-dash placeholder. Source values (<c>APP_VERSION</c>,
/// <c>GIT_SHA</c>, <c>BUILD_DATE</c>) are produced by the
/// <c>build-and-push.yml</c> workflow and forwarded into the container as
/// <c>--build-arg</c> → <c>ENV</c> by each module's Dockerfile (Sales/Frontline
/// /Portal). Locally, when none of them are set, <see cref="DisplayVersion"/>
/// returns "dev" rather than the em-dash so a `dotnet run` session still shows
/// SOMETHING in the badge.
/// </summary>
public sealed record BuildVersion(string? Version, string? GitSha, DateTimeOffset? BuildDate)
{
    /// <summary>"—" when no version is wired (the explicit "value not set"
    /// signal the shell already uses). When APP_VERSION is the literal
    /// "0.1.0-dev" — the local-dev sentinel — collapse to "dev" so the env
    /// badge stays compact in the corner of the topbar. Otherwise return the
    /// configured version verbatim.</summary>
    public string DisplayVersion
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Version)) return "—";
            if (string.Equals(Version, "0.1.0-dev", StringComparison.OrdinalIgnoreCase)) return "dev";
            return Version;
        }
    }

    /// <summary>Short 7-char SHA suitable for the Build panel's "Commit" row.
    /// Returns null when no git sha is wired.</summary>
    public string? ShortSha => string.IsNullOrWhiteSpace(GitSha)
        ? null
        : (GitSha.Length <= 7 ? GitSha : GitSha[..7]);
}
