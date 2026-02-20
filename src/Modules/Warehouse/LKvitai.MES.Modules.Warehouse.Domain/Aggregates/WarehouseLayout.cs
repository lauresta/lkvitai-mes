using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Domain.Aggregates;

/// <summary>
/// WarehouseLayout aggregate - State-based, defines physical topology
/// </summary>
public class WarehouseLayout
{
    // Aggregate placeholder - business logic to be implemented
    // Per blueprint: State-based with EF Core
    
    public Guid WarehouseId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
}
