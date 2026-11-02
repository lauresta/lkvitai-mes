using System.Net.Http.Json;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class AdminUsersClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider _authentication;
    private readonly LKvitai.MES.BuildingBlocks.PortalAuth.CikadaUserBridgeProof _proof;
    private readonly ILogger<AdminUsersClient>? _logger;
    private readonly CikadaSsoAccountLookup? _ssoAccounts;

    public AdminUsersClient(IHttpClientFactory factory,
        Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authentication,
        LKvitai.MES.BuildingBlocks.PortalAuth.CikadaUserBridgeProof proof,
        ILogger<AdminUsersClient>? logger=null, CikadaSsoAccountLookup? ssoAccounts=null)
    { _factory=factory;_authentication=authentication;_proof=proof;_logger=logger;_ssoAccounts=ssoAccounts; }

    public Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken=default)
        =>Send<IReadOnlyList<AdminUserDto>>(HttpMethod.Get,"/api/warehouse/v1/admin/users",null,cancellationToken);
    public Task<AdminUserDto> CreateUserAsync(CreateAdminUserRequestDto request,CancellationToken cancellationToken=default)
        =>Send<AdminUserDto>(HttpMethod.Post,"/api/warehouse/v1/admin/users",request,cancellationToken);
    public Task<AdminUserDto> UpdateUserAsync(Guid id,UpdateAdminUserRequestDto request,CancellationToken cancellationToken=default)
        =>Send<AdminUserDto>(HttpMethod.Put,$"/api/warehouse/v1/admin/users/{id}",request,cancellationToken);

    private Task<T> Send<T>(HttpMethod method,string url,object? body,CancellationToken ct)=>SendAndReadAsync<T>(async()=>
    {
        var user=(await _authentication.GetAuthenticationStateAsync()).User;
        var identities=user.Identities.Where(x=>x.IsAuthenticated).ToArray();
        if(identities.Length!=1 || identities[0].AuthenticationType!="Cookies")
            throw new InvalidOperationException("Sign in with your Cikada account to manage users.");
        Guid actor;
        if (user.FindFirst("auth_source")?.Value == CikadaJwtValidator.AuthSource)
            actor = _ssoAccounts is null ? Guid.Empty : (await _ssoAccounts.ResolveAsync(user, ct))?.UserId ?? Guid.Empty;
        else Guid.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out actor);
        if (actor == Guid.Empty) throw new InvalidOperationException("Sign in with your Cikada account to manage users.");
        var token=user.FindFirst("warehouse_access_token")?.Value;
        if(string.IsNullOrWhiteSpace(token))throw new InvalidOperationException("Renew your Cikada session before managing users.");
        using var request=new HttpRequestMessage(method,url);
        request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        request.Headers.Add(LKvitai.MES.BuildingBlocks.PortalAuth.CikadaUserBridgeProof.HeaderName,
            _proof.Create(actor,LKvitai.MES.BuildingBlocks.PortalAuth.CikadaUserBridgeProof.ManageUsers));
        if(body is not null)request.Content=JsonContent.Create(body);
        return await _factory.CreateClient("WarehouseUserAdminApi").SendAsync(request,ct);
    });

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            _logger?.LogError(
                "Admin users API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
                response.RequestMessage?.Method.Method ?? "UNKNOWN",
                response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
                (int)response.StatusCode,
                problem?.ErrorCode ?? "UNKNOWN",
                problem?.TraceId ?? "UNKNOWN",
                problem?.Detail ?? "n/a");

            throw new ApiException(problem, (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync();
        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }
}
