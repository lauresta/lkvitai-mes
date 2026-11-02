using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

/// <summary>Short-lived, purpose-bound server-to-server identity for user administration
/// and Sales mapping lookup. Never accepts the legacy unsigned role headers as proof.</summary>
public sealed class CikadaUserBridgeProof(IDataProtectionProvider provider, TimeProvider clock)
{
    public const string HeaderName = "X-Cikada-User-Proof";
    public const string ManageUsers = "warehouse-user-administration";
    public const string LookupLegacySql = "sales-legacy-sql-lookup";
    private readonly IDataProtector _protector = provider.CreateProtector("LKvitai.MES.Cikada.UserBridge.v1");
    public sealed record Identity(int Version, Guid UserId, string Purpose, DateTimeOffset IssuedUtc, DateTimeOffset ExpiresUtc, Guid Nonce);

    public string Create(Guid userId, string purpose)
    {
        if(userId == Guid.Empty || !ValidPurpose(purpose))throw new ArgumentException("Invalid bridge identity.");
        var now=clock.GetUtcNow();
        return _protector.Protect(JsonSerializer.Serialize(new Identity(1,userId,purpose,now,now.AddMinutes(2),Guid.NewGuid())));
    }
    public Identity? Read(string? proof, string purpose)
    {
        if(string.IsNullOrEmpty(proof) || proof.Length>8192 || !ValidPurpose(purpose))return null;
        try
        {
            var identity=JsonSerializer.Deserialize<Identity>(_protector.Unprotect(proof));
            var now=clock.GetUtcNow();
            return identity is {Version:1} && identity.UserId!=Guid.Empty && identity.Nonce!=Guid.Empty
                && identity.Purpose==purpose && identity.IssuedUtc<=now.AddSeconds(30)
                && identity.ExpiresUtc>now && identity.ExpiresUtc>identity.IssuedUtc
                && identity.ExpiresUtc-identity.IssuedUtc<=TimeSpan.FromMinutes(2) ? identity:null;
        }
        catch(Exception e)when(e is CryptographicException or JsonException or FormatException or ArgumentException){return null;}
    }
    private static bool ValidPurpose(string purpose)=>purpose is ManageUsers or LookupLegacySql;
}
