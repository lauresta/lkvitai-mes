namespace LKvitai.MES.Modules.Shopfloor.Domain.Entities;

/// <summary>
/// A product type synced (read-only) from the legacy LKvitaiDb. Rows are never
/// deleted; ones absent from the latest sync are tombstoned via
/// <see cref="RemovedAt"/>.
/// </summary>
public sealed class LegacyProductType
{
    public string Code { get; private set; } = string.Empty;
    public string KindName { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset LastSyncedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }

    public bool Removed => RemovedAt is not null;

    private LegacyProductType() { }

    public LegacyProductType(string code, string kindName, string name, DateTimeOffset syncedAt)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("code is required.", nameof(code))
            : code.Trim();
        ApplySync(kindName, name, syncedAt);
    }

    /// <summary>Upsert from a sync result: refresh names, mark present.</summary>
    public void ApplySync(string kindName, string name, DateTimeOffset syncedAt)
    {
        KindName = kindName?.Trim() ?? string.Empty;
        Name = name?.Trim() ?? string.Empty;
        LastSyncedAt = syncedAt;
        RemovedAt = null;
    }

    /// <summary>Tombstone a row absent from the latest sync result.</summary>
    public void MarkRemoved(DateTimeOffset syncedAt)
    {
        RemovedAt ??= syncedAt;
    }
}
