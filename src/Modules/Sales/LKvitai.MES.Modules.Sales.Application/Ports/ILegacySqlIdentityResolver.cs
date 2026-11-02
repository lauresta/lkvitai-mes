namespace LKvitai.MES.Modules.Sales.Application.Ports;

/// <summary>Resolves an authenticated request's active, server-owned Cikada mapping.
/// Implementations must never take the SQL login from request bodies or query strings.</summary>
public interface ILegacySqlIdentityResolver
{
    Task<LegacySqlIdentity?> ResolveAsync(CancellationToken cancellationToken);
}

/// <param name="Login">An active mapping to an existing SQL or Windows login with native access.</param>
/// <param name="AllowServiceLogin">Explicit administrator mapping may use the
/// original service login. Ordinary mappings must never receive this exception.</param>
public sealed record LegacySqlIdentity(string Login, bool AllowServiceLogin = false);
