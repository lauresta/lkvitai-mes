namespace LKvitai.MES.Modules.Sales.WebUI;

/// <summary>
/// Pure UI model used by <see cref="Components.LkSelect"/>. Order data has moved
/// to <c>Sales.Contracts</c> (DTOs) + <c>Sales.Infrastructure</c> (stub) in S-1.
/// </summary>
public sealed record SelectOption(string Value, string Label);
