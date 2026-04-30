namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Circuit-scoped snapshot of the Portal auth cookie captured during the
/// initial Blazor Server HTTP handshake (see <c>_Host.cshtml</c>).
///
/// Why this exists: <c>IHttpContextAccessor.HttpContext</c> is only guaranteed
/// during the initial HTTP request that upgrades the connection to SignalR.
/// Once the Blazor circuit takes over, <c>HttpContext</c> may be <see langword="null"/>
/// or already disposed, so reading cookies from it during component
/// interactions (e.g. a button click that calls Sales API) is unreliable.
///
/// Registered as <see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped"/>,
/// which in Blazor Server equals one instance per circuit. The value is
/// captured once in <c>_Host.cshtml</c> while <c>HttpContext</c> is still live
/// and is then read by components for the lifetime of the circuit.
///
/// Deliberately not used from a <see cref="DelegatingHandler"/>:
/// <c>IHttpClientFactory</c> creates its own internal DI scope for handler-chain
/// rotation (default 2 minutes) that is *shared* across all callers, which
/// would collapse per-circuit state into a single shared slot — so the cookie
/// is attached at the call site instead.
/// </summary>
public sealed class PortalAuthCookieState
{
    /// <summary>
    /// The raw cookie value for <see cref="BuildingBlocks.PortalAuth.PortalAuthDefaults.CookieName"/>,
    /// or <see langword="null"/> when the circuit was established by an
    /// anonymous visitor.
    /// </summary>
    public string? CookieValue { get; set; }
}
