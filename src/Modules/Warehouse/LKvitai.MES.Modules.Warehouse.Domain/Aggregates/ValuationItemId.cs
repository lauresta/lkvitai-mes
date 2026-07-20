using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Domain.Aggregates;

/// <summary>
/// Deterministic int-Item-Id &lt;-&gt; Guid conversion used by the shared Valuation event
/// contracts (<c>ValuationInitialized</c>/<c>CostAdjusted</c> carry a legacy Guid ItemId
/// field alongside the int InventoryItemId field).
/// </summary>
public static class ValuationItemId
{
    public static Guid ToValuationItemId(int itemId)
    {
        if (itemId <= 0)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "ItemId must be greater than zero.");
        }

        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, itemId);
        return new Guid(bytes);
    }

    public static bool TryToInventoryItemId(Guid valuationItemId, out int itemId)
    {
        itemId = BitConverter.ToInt32(valuationItemId.ToByteArray(), 0);
        return itemId > 0;
    }

    public static int ToInventoryItemId(Guid valuationItemId)
    {
        if (!TryToInventoryItemId(valuationItemId, out var itemId))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Valuation ItemId is invalid.");
        }

        return itemId;
    }
}
