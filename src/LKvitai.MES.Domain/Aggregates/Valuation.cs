using LKvitai.MES.Contracts.Events;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Domain.Aggregates;

/// <summary>
/// Valuation aggregate - Event sourced, manages stock cost adjustments
/// Financial interpretation decoupled from physical quantities
/// </summary>
public class Valuation
{
    private const int CostScale = 4;

    public string Id { get; set; } = string.Empty;
    public Guid ItemId { get; private set; }
    public decimal UnitCost { get; private set; }
    public DateTime? LastAdjustedAt { get; private set; }
    public string LastAdjustedBy { get; private set; } = string.Empty;
    public int Version { get; private set; }

    public static string StreamIdFor(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "ItemId is required.");
        }

        return $"valuation-{itemId:D}".ToLowerInvariant();
    }

    public static string StreamIdFor(int itemId)
    {
        return StreamIdFor(ToValuationItemId(itemId));
    }

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

    public ValuationInitialized Initialize(
        Guid itemId,
        decimal initialUnitCost,
        string source,
        string initializedBy,
        Guid commandId,
        Guid? inboundShipmentId = null,
        DateTime? initializedAt = null)
    {
        if (ItemId != Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Valuation is already initialized.");
        }

        if (itemId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "ItemId is required.");
        }

        if (initialUnitCost <= 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Initial unit cost must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Source is required.");
        }

        if (string.IsNullOrWhiteSpace(initializedBy))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "InitializedBy is required.");
        }

        if (commandId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "CommandId is required.");
        }

        return new ValuationInitialized
        {
            ItemId = itemId,
            InitialUnitCost = NormalizeCost(initialUnitCost),
            Source = source.Trim(),
            InboundShipmentId = inboundShipmentId,
            InitializedBy = initializedBy.Trim(),
            InitializedAt = EnsureUtc(initializedAt ?? DateTime.UtcNow),
            CommandId = commandId
        };
    }

    public CostAdjusted AdjustCost(
        decimal newUnitCost,
        string reason,
        string adjustedBy,
        Guid commandId,
        Guid? approverId = null,
        DateTime? adjustedAt = null)
    {
        EnsureInitialized();

        if (newUnitCost <= 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "New unit cost must be greater than zero.");
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

        return new CostAdjusted
        {
            ItemId = ItemId,
            OldUnitCost = UnitCost,
            NewUnitCost = NormalizeCost(newUnitCost),
            Reason = reason.Trim(),
            AdjustedBy = adjustedBy.Trim(),
            AdjustedAt = EnsureUtc(adjustedAt ?? DateTime.UtcNow),
            ApproverId = approverId,
            CommandId = commandId
        };
    }

    public LandedCostAllocated AllocateLandedCost(
        decimal landedCostPerUnit,
        Guid inboundShipmentId,
        string allocationMethod,
        string allocatedBy,
        Guid commandId,
        DateTime? allocatedAt = null)
    {
        EnsureInitialized();

        if (landedCostPerUnit <= 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "LandedCostPerUnit must be greater than zero.");
        }

        if (inboundShipmentId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "InboundShipmentId is required.");
        }

        if (string.IsNullOrWhiteSpace(allocationMethod))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Allocation method is required.");
        }

        if (string.IsNullOrWhiteSpace(allocatedBy))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "AllocatedBy is required.");
        }

        if (commandId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "CommandId is required.");
        }

        var normalizedLandedCost = NormalizeCost(landedCostPerUnit);
        var newUnitCost = NormalizeCost(UnitCost + normalizedLandedCost);

        return new LandedCostAllocated
        {
            ItemId = ItemId,
            OldUnitCost = UnitCost,
            LandedCostPerUnit = normalizedLandedCost,
            NewUnitCost = newUnitCost,
            InboundShipmentId = inboundShipmentId,
            AllocationMethod = allocationMethod.Trim(),
            AllocatedBy = allocatedBy.Trim(),
            AllocatedAt = EnsureUtc(allocatedAt ?? DateTime.UtcNow),
            CommandId = commandId
        };
    }

    public StockWrittenDown WriteDown(
        decimal writeDownPercentage,
        string reason,
        string approvedBy,
        decimal quantityAffected,
        Guid commandId,
        DateTime? approvedAt = null)
    {
        EnsureInitialized();

        if (writeDownPercentage <= 0m || writeDownPercentage >= 1m)
        {
            throw new DomainException(
                DomainErrorCodes.ValidationError,
                "WriteDownPercentage must be between 0 and 1 (exclusive).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Reason is required.");
        }

        if (string.IsNullOrWhiteSpace(approvedBy))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "ApprovedBy is required.");
        }

        if (quantityAffected <= 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "QuantityAffected must be greater than zero.");
        }

        if (commandId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "CommandId is required.");
        }

        var normalizedPercentage = NormalizeCost(writeDownPercentage);
        var newUnitCost = NormalizeCost(UnitCost * (1m - normalizedPercentage));
        var financialImpact = NormalizeCost(quantityAffected * (UnitCost - newUnitCost));

        return new StockWrittenDown
        {
            ItemId = ItemId,
            OldUnitCost = UnitCost,
            WriteDownPercentage = normalizedPercentage,
            NewUnitCost = newUnitCost,
            Reason = reason.Trim(),
            ApprovedBy = approvedBy.Trim(),
            ApprovedAt = EnsureUtc(approvedAt ?? DateTime.UtcNow),
            QuantityAffected = NormalizeCost(quantityAffected),
            FinancialImpact = financialImpact,
            CommandId = commandId
        };
    }

    public void EnsureExpectedVersion(int expectedVersion)
    {
        if (expectedVersion < 0)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Expected version must be non-negative.");
        }

        if (Version != expectedVersion)
        {
            throw new DomainException(
                DomainErrorCodes.ConcurrencyConflict,
                $"Concurrency conflict for item {ItemId}. Expected version {expectedVersion}, actual version {Version}.");
        }
    }

    public void Apply(ValuationInitialized e)
    {
        ItemId = e.ItemId;
        UnitCost = NormalizeCost(e.InitialUnitCost);
        LastAdjustedAt = EnsureUtc(e.InitializedAt);
        LastAdjustedBy = e.InitializedBy;
        Id = StreamIdFor(e.ItemId);
        Version++;
    }

    public void Apply(CostAdjusted e)
    {
        UnitCost = NormalizeCost(e.NewUnitCost);
        LastAdjustedAt = EnsureUtc(e.AdjustedAt);
        LastAdjustedBy = e.AdjustedBy;
        Version++;
    }

    public void Apply(LandedCostAllocated e)
    {
        UnitCost = NormalizeCost(e.NewUnitCost);
        LastAdjustedAt = EnsureUtc(e.AllocatedAt);
        LastAdjustedBy = e.AllocatedBy;
        Version++;
    }

    public void Apply(StockWrittenDown e)
    {
        UnitCost = NormalizeCost(e.NewUnitCost);
        LastAdjustedAt = EnsureUtc(e.ApprovedAt);
        LastAdjustedBy = e.ApprovedBy;
        Version++;
    }

    private void EnsureInitialized()
    {
        if (ItemId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Valuation is not initialized.");
        }
    }

    private static decimal NormalizeCost(decimal value)
    {
        return decimal.Round(value, CostScale, MidpointRounding.AwayFromZero);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
