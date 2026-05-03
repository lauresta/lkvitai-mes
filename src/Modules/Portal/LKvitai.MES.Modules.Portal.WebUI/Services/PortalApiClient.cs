using System.Net.Http.Json;
using LKvitai.MES.Modules.Portal.WebUI.Models;

namespace LKvitai.MES.Modules.Portal.WebUI.Services;

/// <summary>
/// Thin Portal API client used by the Portal home dashboard. Wraps the
/// named <c>PortalApi</c> HttpClient (configured with the Portal API base
/// URL + <see cref="PortalApiAuthHandler"/>) and translates the API's
/// <c>PortalStatusResponse</c> / <c>PortalModuleResponse</c> /
/// <c>PortalNewsItemResponse</c> shapes into the WebUI-side view models
/// (<see cref="ModuleCard"/>, <see cref="NewsItem"/>) that <c>Home.razor</c>
/// already renders.
///
/// All three calls fail soft: on transport / parse errors we log and return
/// an empty list / null rather than crashing the Blazor circuit. The home
/// dashboard renders sane fallback content (empty modules grid, empty news,
/// "0.0.0" version) so a Portal API outage degrades the surface gracefully
/// instead of taking the whole Portal down.
/// </summary>
public sealed class PortalApiClient
{
    private const string ClientName = "PortalApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PortalApiClient> _logger;

    public PortalApiClient(IHttpClientFactory httpClientFactory, ILogger<PortalApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PortalStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        try
        {
            var dto = await client.GetFromJsonAsync<PortalStatusDto>(
                "api/portal/v1/status", cancellationToken).ConfigureAwait(false);
            if (dto is null) return null;
            return new PortalStatus(
                Module: dto.Module,
                Status: dto.Status,
                Version: dto.Version,
                ReleaseTag: dto.ReleaseTag,
                GitSha: dto.GitSha,
                BuildDate: dto.BuildDate,
                Environment: dto.Environment,
                Channel: dto.Channel);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                      or TaskCanceledException
                                      or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Portal API GET /status failed; falling back to local defaults");
            return null;
        }
    }

    public async Task<IReadOnlyList<ModuleCard>> GetModulesAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        try
        {
            var dtos = await client.GetFromJsonAsync<List<PortalModuleDto>>(
                "api/portal/v1/modules", cancellationToken).ConfigureAwait(false);
            if (dtos is null) return Array.Empty<ModuleCard>();

            return dtos
                .Select(d => new ModuleCard(
                    Key: d.Key,
                    Title: d.Title,
                    Category: d.Category,
                    Description: d.Description,
                    Status: ParseStatus(d.Status),
                    Url: d.Url,
                    Quarter: d.Quarter))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException
                                      or TaskCanceledException
                                      or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Portal API GET /modules failed; rendering empty modules grid");
            return Array.Empty<ModuleCard>();
        }
    }

    public async Task<IReadOnlyList<NewsItem>> GetNewsAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        try
        {
            var dtos = await client.GetFromJsonAsync<List<PortalNewsDto>>(
                "api/portal/v1/news", cancellationToken).ConfigureAwait(false);
            if (dtos is null) return Array.Empty<NewsItem>();

            return dtos
                .Select(d => new NewsItem(
                    Tag: d.Tag,
                    TagColor: d.TagColor,
                    TagBackground: d.TagBackground,
                    Title: d.Title,
                    Excerpt: d.Excerpt,
                    Date: d.Date))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException
                                      or TaskCanceledException
                                      or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Portal API GET /news failed; rendering empty news strip");
            return Array.Empty<NewsItem>();
        }
    }

    private static ModuleStatus ParseStatus(string raw) =>
        raw switch
        {
            "Active"     => ModuleStatus.Active,
            "Scaffolded" => ModuleStatus.Scaffolded,
            _            => ModuleStatus.Planned,
        };

    private sealed record PortalStatusDto(
        string Module,
        string Status,
        string? Version,
        string? ReleaseTag,
        string? GitSha,
        DateTimeOffset? BuildDate,
        string Environment,
        string Channel);

    private sealed record PortalModuleDto(
        string Key,
        string Title,
        string Category,
        string Description,
        string Status,
        string? Url,
        string? Quarter,
        IReadOnlyList<string>? RequiredRoles);

    private sealed record PortalNewsDto(
        string Tag,
        string TagColor,
        string TagBackground,
        string Title,
        string Excerpt,
        string Date,
        string? Url);
}

/// <summary>
/// WebUI-side view model for the Portal status panel. Mirrors the API
/// shape but stays a separate type so the Razor side doesn't reference
/// Portal.Api models directly.
/// </summary>
public sealed record PortalStatus(
    string Module,
    string Status,
    string? Version,
    string? ReleaseTag,
    string? GitSha,
    DateTimeOffset? BuildDate,
    string Environment,
    string Channel);
