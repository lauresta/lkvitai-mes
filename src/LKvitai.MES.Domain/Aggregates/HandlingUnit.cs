using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Domain.Aggregates;

/// <summary>
/// HandlingUnit aggregate - State-based with event projection
/// State derived from StockMoved events
/// </summary>
public class HandlingUnit
{
    // Aggregate placeholder - business logic to be implemented
    // Per blueprint: Uses EF Core for state persistence, subscribes to StockMoved events
    
    public Guid HUId { get; private set; }
    public string LPN { get; private set; } = string.Empty;
    public HandlingUnitType Type { get; private set; }
    public HandlingUnitStatus Status { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? SealedAt { get; private set; }
    public int Version { get; private set; }
    
    public List<HandlingUnitLine> Lines { get; private set; } = new();
}

public enum HandlingUnitType
{
    PALLET,
    BOX,
    BAG,
    UNIT
}

public enum HandlingUnitStatus
{
    OPEN,
    SEALED,
    PICKED,
    EMPTY
}

public class HandlingUnitLine
{
    public Guid HUId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
