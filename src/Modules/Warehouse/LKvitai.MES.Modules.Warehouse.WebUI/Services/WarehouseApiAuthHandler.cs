using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class WarehouseApiAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public WarehouseApiAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authenticationStateProvider = authenticationStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
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
}
