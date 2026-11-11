namespace LKvitai.MES.Modules.Warehouse.Application.Projections;

public interface IOnHandValueRebuildService
{
    Task<OnHandValuePreview> PreviewAsync(CancellationToken ct = default);
    // False means either checksum has changed (or the shadow no longer exists).
    Task<bool> ApplyAsync(OnHandValueApply request, ProjectionAuditActor actor, CancellationToken ct = default);
}

public sealed record OnHandValueAmounts(decimal Qty, decimal UnitCost, decimal TotalValue);
public sealed record OnHandValueChange(string Sku, string Change, OnHandValueAmounts? Before, OnHandValueAmounts? After);
public sealed record OnHandValuePreview(string ProductionChecksum, string ShadowChecksum, IReadOnlyList<OnHandValueChange> Rows);
public sealed record OnHandValueApply(string ProductionChecksum, string ShadowChecksum, string Reason);
public sealed record ProjectionAuditActor(string UserId, string IpAddress, string UserAgent);
