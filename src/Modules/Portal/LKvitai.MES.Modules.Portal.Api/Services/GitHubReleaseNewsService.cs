using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LKvitai.MES.Modules.Portal.Api.Configuration;
using LKvitai.MES.Modules.Portal.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Portal.Api.Services;

public sealed class GitHubReleaseNewsService
{
    private const string CacheKey = "portal:news:github-releases";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(6);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptionsSnapshot<PortalDashboardOptions> _fallbackOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitHubReleaseNewsService> _logger;

    public GitHubReleaseNewsService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptionsSnapshot<PortalDashboardOptions> fallbackOptions,
        IConfiguration configuration,
        ILogger<GitHubReleaseNewsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _fallbackOptions = fallbackOptions;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PortalNewsItemResponse>> GetAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<IReadOnlyList<PortalNewsItemResponse>>(CacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        try
        {
            var releases = await FetchReleasesAsync(cancellationToken);
            var mapped = releases
                .Take(6)
                .Select(ToNewsItem)
                .ToList();

            if (mapped.Count > 0)
            {
                _cache.Set(CacheKey, mapped, ResolveTtl());
                return mapped;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                      or TaskCanceledException
                                      or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "GitHub release news fetch failed; using configured Portal news fallback");
        }

        return _fallbackOptions.Value.News
            .Select(n => new PortalNewsItemResponse(
                Tag: n.Tag,
                TagColor: n.TagColor,
                TagBackground: n.TagBackground,
                Title: n.Title,
                Excerpt: n.Excerpt,
                Date: n.Date,
                Url: n.Url))
            .ToList();
    }

    private async Task<IReadOnlyList<GitHubReleaseDto>> FetchReleasesAsync(CancellationToken cancellationToken)
    {
        var owner = _configuration["PortalNews:GitHubOwner"] ?? "lauresta";
        var repo = _configuration["PortalNews:GitHubRepo"] ?? "lkvitai-mes";

        var client = _httpClientFactory.CreateClient("GitHubReleases");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{owner}/{repo}/releases");
        var token = _configuration["PortalNews:GitHubToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var releases = await response.Content.ReadFromJsonAsync<List<GitHubReleaseDto>>(cancellationToken);
        return releases ?? [];
    }

    private static PortalNewsItemResponse ToNewsItem(GitHubReleaseDto release)
    {
        var date = release.PublishedAt?.ToString("MMM d") ?? release.CreatedAt?.ToString("MMM d") ?? "release";
        return new PortalNewsItemResponse(
            Tag: release.TagName,
            TagColor: "var(--accent-700)",
            TagBackground: "var(--accent-50)",
            Title: string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            Excerpt: Excerpt(release.Body),
            Date: date,
            Url: release.HtmlUrl);
    }

    private TimeSpan ResolveTtl()
    {
        var raw = _configuration["PortalNews:CacheHours"];
        return double.TryParse(raw, out var hours) && hours > 0
            ? TimeSpan.FromHours(hours)
            : DefaultTtl;
    }

    private static string Excerpt(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "Release notes are available on GitHub.";
        }

        var normalized = body.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return normalized.Length <= 180 ? normalized : $"{normalized[..177]}...";
    }

    private sealed record GitHubReleaseDto(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt);
}
