using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

public sealed record SupplierSyncResult(int Created, int Updated, int MarkedMissing, int Restored, int CodeConflicts);

public sealed record SupplierPostingCheck(bool Allowed, string? Reason)
{
    public static readonly SupplierPostingCheck Ok = new(true, null);
}

/// <summary>Blueprint 4.11: suppliers are matched by AgnumClientId only; never by code.</summary>
public sealed class AgnumSupplierSyncService
{
    private static readonly HashSet<string> EuPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT","BE","BG","HR","CY","CZ","DE","DK","EE","EL","ES","FI","FR","HU","IE","IT","LV","LU","MT","NL","PL","PT","RO","SK","SI","SE"
    };

    private readonly WarehouseDbContext _db;
    private readonly IAgnumReferenceClient _agnum;
    private readonly ILogger<AgnumSupplierSyncService> _logger;

    public AgnumSupplierSyncService(WarehouseDbContext db, IAgnumReferenceClient agnum, ILogger<AgnumSupplierSyncService> logger)
    {
        _db = db;
        _agnum = agnum;
        _logger = logger;
    }

    public async Task<SupplierSyncResult> SyncAsync(CancellationToken ct = default)
    {
        var clients = (await _agnum.GetClientsAsync(ct)).Where(IsSupplier).Where(c => c.Id > 0).ToList();
        if (clients.Count == 0)
        {
            // An empty answer is far more likely an outage than "every supplier vanished".
            throw new AgnumApiUnavailableException("Agnum returned no suppliers; sync aborted without changes.");
        }

        var byAgnumId = await _db.Suppliers.Where(s => s.AgnumClientId != null)
            .ToDictionaryAsync(s => s.AgnumClientId!.Value, ct);
        var usedCodes = (await _db.Suppliers.Select(s => s.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int created = 0, updated = 0, restored = 0, conflicts = 0;
        var seen = new HashSet<int>();
        var now = DateTimeOffset.UtcNow;

        foreach (var client in clients)
        {
            if (!seen.Add(client.Id)) continue;
            var name = string.IsNullOrWhiteSpace(client.Name) ? client.Code.Trim() : client.Name.Trim();

            if (byAgnumId.TryGetValue(client.Id, out var supplier))
            {
                if (supplier.MissingInAgnum) restored++;
                supplier.Name = name;
                updated++;
            }
            else
            {
                var code = client.Code.Trim();
                if (code.Length == 0) continue;
                if (usedCodes.Contains(code))
                {
                    // Same code, different identity: do NOT link. Create a distinct supplier.
                    conflicts++;
                    code = $"{code}~{client.Id}";
                    _logger.LogWarning("Agnum client {ClientId} shares its code with another supplier; created separately.", client.Id);
                }

                supplier = new Supplier { AgnumClientId = client.Id, Code = code, Name = name, CreatedAt = now };
                usedCodes.Add(code);
                _db.Suppliers.Add(supplier);
                byAgnumId[client.Id] = supplier;
                created++;
            }

            supplier.CompanyCode = Merge(supplier.CompanyCode, client.CompanyCode);
            supplier.VatCode = Merge(supplier.VatCode, client.VatCode);
            supplier.Email = Merge(supplier.Email, client.Email);
            supplier.RegisteredAddress = Merge(supplier.RegisteredAddress, client.RegisteredAddress);
            supplier.Country = Merge(supplier.Country, client.Country);
            supplier.MissingInAgnum = false;
            supplier.IsAgnumCreditor = client.Krd is null ? null : string.Equals(client.Krd.Trim(), "Y", StringComparison.OrdinalIgnoreCase);
            supplier.LastAgnumSyncedAt = now;
            supplier.UpdatedAt = now;
            // Kind is filled only while empty, so a manual correction survives the next sync.
            supplier.Kind ??= ResolveKind(supplier.VatCode, supplier.Country);
        }

        var missing = 0;
        foreach (var (agnumId, supplier) in byAgnumId)
        {
            if (seen.Contains(agnumId) || supplier.MissingInAgnum) continue;
            supplier.MissingInAgnum = true;
            supplier.UpdatedAt = now;
            missing++;
        }

        await _db.SaveChangesAsync(ct);
        return new SupplierSyncResult(created, updated - restored, missing, restored, conflicts);
    }

    /// <summary>
    /// Blueprint 4.11 posting guard: a supplier without AgnumClientId, flagged MissingInAgnum, or known
    /// not to be a creditor cannot stand on an Auto-posted document.
    /// </summary>
    public static SupplierPostingCheck CheckPostable(Supplier supplier)
    {
        if (supplier.AgnumClientId is null) return new(false, "Supplier is not linked to an Agnum client.");
        if (supplier.MissingInAgnum) return new(false, "Supplier is missing in Agnum.");
        if (supplier.IsAgnumCreditor == false) return new(false, "Supplier is not a creditor in Agnum (KRD).");
        return SupplierPostingCheck.Ok;
    }

    public async Task<SupplierPostingCheck> CheckPostableAsync(int supplierId, CancellationToken ct = default)
    {
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId, ct);
        return supplier is null ? new(false, "Supplier not found.") : CheckPostable(supplier);
    }

    public static SupplierKind? ResolveKind(string? vatCode, string? country)
    {
        var prefix = vatCode is { Length: >= 2 } v && char.IsLetter(v[0]) && char.IsLetter(v[1]) ? v[..2].ToUpperInvariant() : null;
        if (prefix == "LT") return SupplierKind.Domestic;
        if (prefix is not null && EuPrefixes.Contains(prefix)) return SupplierKind.Eu;
        if (prefix is not null) return SupplierKind.NonEu;

        var c = country?.Trim();
        if (string.IsNullOrEmpty(c)) return null;
        if (c.Equals("LT", StringComparison.OrdinalIgnoreCase) || c.StartsWith("Lietuv", StringComparison.OrdinalIgnoreCase)
            || c.Equals("Lithuania", StringComparison.OrdinalIgnoreCase)) return SupplierKind.Domestic;
        if (c.Length == 2) return EuPrefixes.Contains(c) || c.Equals("GR", StringComparison.OrdinalIgnoreCase) ? SupplierKind.Eu : SupplierKind.NonEu;
        return null; // unknown country name: leave for manual selection
    }

    private static bool IsSupplier(AgnumClientDto client)
        => client.ClientRoles?.Any(x => string.Equals(x, "SUPPLIER", StringComparison.OrdinalIgnoreCase)) == true
           || client.PozymNumbers?.Contains(1) == true;

    private static string? Merge(string? existing, string? incoming)
        => string.IsNullOrWhiteSpace(incoming) ? existing : incoming.Trim();
}
