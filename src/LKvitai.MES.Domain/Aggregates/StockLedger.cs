using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Domain.Aggregates;

/// <summary>
/// StockLedger aggregate - Event sourced, append-only ledger of stock movements
/// Single source of truth for all stock quantity changes
/// </summary>
public class StockLedger
{
    // Aggregate placeholder - business logic to be implemented
    // Per blueprint: Uses Marten expected-version append for concurrency control
    // Stream ID: stock-ledger-{warehouseId}
}
