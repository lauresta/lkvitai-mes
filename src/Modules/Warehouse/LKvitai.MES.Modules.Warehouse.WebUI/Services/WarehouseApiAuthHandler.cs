using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class WarehouseApiAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IConfiguration _configuration;

    public WarehouseApiAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authenticationStateProvider,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _authenticationStateProvider = authenticationStateProvider;
        _configuration = configuration;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        PrefixApiBasePath(request);

        var token = _httpContextAccessor.HttpContext?.User.FindFirstValue("warehouse_access_token");
        if (string.IsNullOrWhiteSpace(token))
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            token = state.User.FindFirstValue("warehouse_access_token");
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private void PrefixApiBasePath(HttpRequestMessage request)
    {
        var basePath = ResolveApiBasePath();
        if (string.IsNullOrWhiteSpace(basePath) || request.RequestUri is null || !request.RequestUri.IsAbsoluteUri)
        {
            return;
        }

        var normalizedBasePath = basePath.StartsWith("/", StringComparison.Ordinal)
            ? basePath.TrimEnd('/')
            : $"/{basePath.TrimEnd('/')}";

        if (request.RequestUri.AbsolutePath.StartsWith(normalizedBasePath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var builder = new UriBuilder(request.RequestUri)
        {
            Path = $"{normalizedBasePath}{request.RequestUri.AbsolutePath}"
        };
        request.RequestUri = builder.Uri;
    }

    private string? ResolveApiBasePath()
    {
        var configured = _configuration["WarehouseApi:BasePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var baseUrl = _configuration["WarehouseApi:BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)
            ? null
            : uri.AbsolutePath;
    }
}
