using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Finance.Api.BankPayments;

internal sealed record FinanceActor(Guid UserId, string LegacyLogin);
internal sealed record LegacyMapping(Guid UserId, string? LegacySqlLogin);

internal sealed class FinanceAccess(IHttpClientFactory clients, CikadaUserBridgeProof proof,
    CikadaSsoAccountLookup ssoAccounts, IConfiguration configuration)
{
    public async Task<FinanceActor?> ResolveAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var identities = user.Identities.Where(value => value.IsAuthenticated).ToArray();
        if (identities.Length != 1) return null;
        var identity = identities[0];
        if (identity?.AuthenticationType is not ("Cookies" or SalesCredentialProtector.SsoScheme)) return null;
        Guid id;
        if (identity.FindFirst("auth_source")?.Value == CikadaJwtValidator.AuthSource)
        {
            // The SSO subject is an external string, not the Cikada account GUID.
            // Resolve it through the same signed, current-account lookup used by Sales.
            var account = await ssoAccounts.ResolveAsync(user, ct);
            if (account is null) return null;
            id = account.UserId;
        }
        else if (identity.AuthenticationType == "Cookies" &&
                 Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var localId) &&
                 localId != Guid.Empty)
        {
            id = localId;
        }
        else return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/internal/legacy-sql-identity");
        request.Headers.Add(CikadaUserBridgeProof.HeaderName, proof.Create(id, CikadaUserBridgeProof.LookupLegacySql));
        using var response = await clients.CreateClient("CikadaUsers").SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var mapping = await response.Content.ReadFromJsonAsync<LegacyMapping>(cancellationToken: ct);
        var allowed = configuration.GetSection("Finance:AllowedLegacyLogins").Get<string[]>() ?? [];
        return mapping is { LegacySqlLogin: { Length: > 0 } login } && mapping.UserId == id &&
            allowed.Any(value => string.Equals(value, login, StringComparison.OrdinalIgnoreCase))
            ? new FinanceActor(id, login) : null;
    }

    public async Task<FinanceSqlLease> OpenAsync(FinanceActor actor, CancellationToken ct)
    {
        var configured = configuration.GetConnectionString("LKvitaiDb");
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidOperationException("Finance SQL connection is missing.");
        var builder = new SqlConnectionStringBuilder(configured) { Pooling = false, MultipleActiveResultSets = false, Enlist = false,
            ApplicationName = "Cikada Finance bank payments" };
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            // The login comes only from the active Cikada account lookup. Never accept it from the request body.
            using var switchCommand = new SqlCommand("EXECUTE AS LOGIN = @login", connection);
            switchCommand.Parameters.Add("@login", System.Data.SqlDbType.NVarChar, 128).Value = actor.LegacyLogin;
            await switchCommand.ExecuteNonQueryAsync(ct);
            using var identity = new SqlCommand("SELECT SUSER_SNAME()", connection);
            if (!string.Equals((string?)await identity.ExecuteScalarAsync(ct), actor.LegacyLogin,
                    StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("SQL identity mismatch.");
            return new FinanceSqlLease(connection);
        }
        catch { await connection.DisposeAsync(); throw; }
    }
}

internal sealed class FinanceSqlLease(SqlConnection connection) : IAsyncDisposable
{
    public SqlConnection Connection { get; } = connection;
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Connection.State == System.Data.ConnectionState.Open)
                await new SqlCommand("REVERT", Connection) { CommandTimeout = 5 }.ExecuteNonQueryAsync();
        }
        finally { await Connection.DisposeAsync(); }
    }
}
