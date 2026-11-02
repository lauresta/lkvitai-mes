using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

namespace LKvitai.MES.Modules.Sales.Api.Security;

/// <summary>Resolve each authenticated request against the current durable Cikada
/// account. Client-supplied names/logins and stale role claims do not select SQL identity.</summary>
public sealed class CikadaLegacySqlIdentityResolver(
    IHttpContextAccessor accessor, IHttpClientFactory factory, CikadaUserBridgeProof proof) : ILegacySqlIdentityResolver
{
    private Task<LegacySqlIdentity?>? _resolved;
    public Task<LegacySqlIdentity?> ResolveAsync(CancellationToken ct)=>_resolved??=ResolveCoreAsync(ct);
    private async Task<LegacySqlIdentity?> ResolveCoreAsync(CancellationToken ct)
    {
        var user=accessor.HttpContext?.User;
        var authenticatedIdentity = user?.Identities.SingleOrDefault(identity => identity.IsAuthenticated);
        var subject = SalesCredentialProtector.Subject(authenticatedIdentity);
        if (authenticatedIdentity?.AuthenticationType is not (SalesCredentialProtector.Scheme or SalesCredentialProtector.SsoScheme)
            || subject is null)
            throw new OrderEntryException("AUTHENTICATED_ACTOR_REQUIRED","Sign in to use Sales.",401);
        var actor = subject.Value;
        using var request=new HttpRequestMessage(HttpMethod.Get,"/api/internal/legacy-sql-identity");
        request.Headers.Add(CikadaUserBridgeProof.HeaderName,proof.Create(actor,CikadaUserBridgeProof.LookupLegacySql));
        try
        {
            using var response=await factory.CreateClient("CikadaUsers").SendAsync(request,ct);
            if(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                throw new OrderEntryException("LEGACY_MAPPING_REQUIRED","Your active Cikada account needs a legacy SQL login mapping. Contact an administrator.",403);
            if(!response.IsSuccessStatusCode)throw new OrderEntryException("IDENTITY_SERVICE_UNAVAILABLE","The user access service is unavailable. Try again.",503);
            var identity=await response.Content.ReadFromJsonAsync<Mapping>(cancellationToken:ct);
            if(identity?.UserId!=actor || string.IsNullOrWhiteSpace(identity.LegacySqlLogin))
                throw new OrderEntryException("LEGACY_MAPPING_REQUIRED","Your active Cikada account needs a legacy SQL login mapping. Contact an administrator.",403);
            return new LegacySqlIdentity(identity.LegacySqlLogin,AllowServiceLogin:string.Equals(identity.LegacySqlLogin,"sa",StringComparison.OrdinalIgnoreCase));
        }
        catch(OperationCanceledException)when(!ct.IsCancellationRequested)
        {throw new OrderEntryException("IDENTITY_SERVICE_UNAVAILABLE","The user access service is unavailable. Try again.",503);}
        catch(Exception e)when(e is HttpRequestException or System.Text.Json.JsonException)
        {throw new OrderEntryException("IDENTITY_SERVICE_UNAVAILABLE","The user access service is unavailable. Try again.",503);}
    }
    private sealed record Mapping(Guid UserId,string? LegacySqlLogin);
}
