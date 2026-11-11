using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

/// <summary>A short-lived, test-only Warehouse identity shared by WebUI and API.</summary>
public sealed class WarehouseQaProof(IDataProtectionProvider provider, TimeProvider clock)
{
    public const string Prefix = "warehouse-qa:";
    public const string UserId = "warehouse-qa";
    public static readonly string[] Roles =
    [
        "Operator", "QCInspector", "WarehouseManager", "WarehouseAdmin",
        "InventoryAccountant", "CFO", "SalesAdmin", "PackingOperator", "DispatchClerk"
    ];
    private readonly IDataProtector _protector = provider.CreateProtector("LKvitai.MES.WarehouseQa.v1");

    private sealed record Payload(int Version, DateTimeOffset IssuedUtc, DateTimeOffset ExpiresUtc, Guid Nonce);

    public string Create(DateTimeOffset expiresUtc)
    {
        var now = clock.GetUtcNow();
        if (expiresUtc <= now || expiresUtc > now.AddHours(4))
            throw new ArgumentOutOfRangeException(nameof(expiresUtc));

        return Prefix + _protector.Protect(JsonSerializer.Serialize(
            new Payload(1, now, expiresUtc, Guid.NewGuid())));
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(Prefix, StringComparison.Ordinal)
            || token.Length > 8192)
            return false;

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(
                _protector.Unprotect(token[Prefix.Length..]));
            var now = clock.GetUtcNow();
            return payload is { Version: 1 }
                && payload.Nonce != Guid.Empty
                && payload.IssuedUtc <= now.AddSeconds(30)
                && payload.ExpiresUtc > now
                && payload.ExpiresUtc > payload.IssuedUtc
                && payload.ExpiresUtc - payload.IssuedUtc <= TimeSpan.FromHours(4);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or ArgumentException)
        {
            return false;
        }
    }
}
