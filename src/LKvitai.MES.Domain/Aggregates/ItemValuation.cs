using LKvitai.MES.Contracts.Events;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Domain.Aggregates;

/// <summary>
/// Event-sourced valuation stream for a single inventory item.
/// </summary>
public sealed class ItemValuation
{
    private const int Scale = 4;

    public int ItemId { get; private set; }
    public decimal CurrentCost { get; private set; }
    public bool IsInitialized { get; private set; }
    public int Version { get; private set; }

    public static string StreamIdFor(int itemId)
    {
        if (itemId <= 0)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "ItemId must be greater than zero.");
        }

        return $"valuation-item-{itemId}";
    }

    public ValuationInitialized Initialize(
        int itemId,
        decimal initialCost,
        string reason,
        string initializedBy,
        Guid commandId,
        DateTime? timestampUtc = null)
    {
        if (IsInitialized)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Valuation is already initialized.");
        }

        if (itemId <= 0)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "ItemId must be greater than zero.");
        }

        if (initialCost < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "InitialCost must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Reason is required.");
        }

        if (string.IsNullOrWhiteSpace(initializedBy))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "InitializedBy is required.");
        }

        if (commandId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "CommandId is required.");
        }

        var eventTimestamp = EnsureUtc(timestampUtc ?? DateTime.UtcNow);

        return new ValuationInitialized
        {
            InventoryItemId = itemId,
            ItemId = Valuation.ToValuationItemId(itemId),
            InitialUnitCost = Normalize(initialCost),
            Reason = reason.Trim(),
            Source = "MANUAL",
            InitializedBy = initializedBy.Trim(),
            InitializedAt = eventTimestamp,
            Timestamp = eventTimestamp,
            CommandId = commandId
        };
    }

    public CostAdjusted AdjustCost(
        decimal newCost,
        string reason,
        string adjustedBy,
        string? approvedBy,
        Guid commandId,
        DateTime? timestampUtc = null)
    {
        EnsureInitialized();

        if (newCost < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "NewCost must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Reason is required.");
        }

        if (string.IsNullOrWhiteSpace(adjustedBy))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "AdjustedBy is required.");
        }

        if (commandId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "CommandId is required.");
        }

        var eventTimestamp = EnsureUtc(timestampUtc ?? DateTime.UtcNow);

        return new CostAdjusted
        {
            InventoryItemId = ItemId,
            ItemId = Valuation.ToValuationItemId(ItemId),
            OldUnitCost = CurrentCost,
            NewUnitCost = Normalize(newCost),
            Reason = reason.Trim(),
            AdjustedBy = adjustedBy.Trim(),
            ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? null : approvedBy.Trim(),
            AdjustedAt = eventTimestamp,
            Timestamp = eventTimestamp,
            CommandId = commandId
        };
    }

    public LandedCostApplied ApplyLandedCost(
        decimal freightCost,
        decimal dutyCost,
        decimal insuranceCost,
        Guid shipmentId,
        string appliedBy,
        Guid commandId,
        DateTime? timestampUtc = null)
    {
        EnsureInitialized();

        if (freightCost < 0m || dutyCost < 0m || insuranceCost < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Costs must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(appliedBy))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "AppliedBy is required.");
        }

        if (commandId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "CommandId is required.");
        }

        var eventTimestamp = EnsureUtc(timestampUtc ?? DateTime.UtcNow);
        var landed = Normalize(freightCost + dutyCost + insuranceCost);

        return new LandedCostApplied
        {
            ItemId = ItemId,
            FreightCost = Normalize(freightCost),
            DutyCost = Normalize(dutyCost),
            InsuranceCost = Normalize(insuranceCost),
            TotalLandedCost = landed,
            ShipmentId = shipmentId,
            AppliedBy = appliedBy.Trim(),
            Timestamp = eventTimestamp,
            CommandId = commandId
        };
    }

    public WrittenDown WriteDown(
        decimal newValue,
        string reason,
        string? approvedBy,
        Guid commandId,
        DateTime? timestampUtc = null)
    {
        EnsureInitialized();

        if (newValue < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "NewValue must be greater than or equal to zero.");
        }

        if (newValue >= CurrentCost)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Write-down must reduce value.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Reason is required.");
        }

        if (commandId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "CommandId is required.");
        }

        var delta = Normalize(CurrentCost - newValue);
        if (delta > 1000m && string.IsNullOrWhiteSpace(approvedBy))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Approval required for write-down > $1000.");
        }

        var eventTimestamp = EnsureUtc(timestampUtc ?? DateTime.UtcNow);

        return new WrittenDown
        {
            ItemId = ItemId,
            OldValue = CurrentCost,
            NewValue = Normalize(newValue),
            Reason = reason.Trim(),
            ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? string.Empty : approvedBy.Trim(),
            Timestamp = eventTimestamp,
            CommandId = commandId
        };
    }

    public void Apply(ValuationInitialized evt)
    {
        ItemId = evt.InventoryItemId > 0
            ? evt.InventoryItemId
            : Valuation.ToInventoryItemId(evt.ItemId);
        CurrentCost = Normalize(evt.InitialUnitCost);
        IsInitialized = true;
        Version++;
    }

    public void Apply(CostAdjusted evt)
    {
        EnsureEventItemId(evt.InventoryItemId, evt.ItemId);
        CurrentCost = Normalize(evt.NewUnitCost);
        Version++;
    }

    public void Apply(LandedCostApplied evt)
    {
        EnsureEventItemId(evt.ItemId, Guid.Empty);
        CurrentCost = Normalize(CurrentCost + evt.TotalLandedCost);
        Version++;
    }

    public void Apply(WrittenDown evt)
    {
        EnsureEventItemId(evt.ItemId, Guid.Empty);
        CurrentCost = Normalize(evt.NewValue);
        Version++;
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Valuation is not initialized.");
        }
    }

    private void EnsureEventItemId(int eventItemId, Guid legacyItemId)
    {
        var resolvedItemId = eventItemId;
        if (resolvedItemId <= 0 && legacyItemId != Guid.Empty)
        {
            resolvedItemId = Valuation.ToInventoryItemId(legacyItemId);
        }

        if (resolvedItemId <= 0 || resolvedItemId != ItemId)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Event item id does not match aggregate item id.");
        }
    }

    private static decimal Normalize(decimal value)
    {
        return decimal.Round(value, Scale, MidpointRounding.AwayFromZero);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
