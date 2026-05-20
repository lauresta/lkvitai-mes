# Agnum Integration — Slice 2: Distribution Blueprint

Created: 2026-05-20
Status: **Ready for implementation**

## Goal

Allow a MES operator to distribute an Agnum virtual warehouse balance into a real physical MES location.
The distribution creates a `StockLedger` event (`RECEIPT` movement) in the physical warehouse and records
the distribution so the virtual balance shows how much has already been allocated.

## Business flow

1. Operator opens the **Agnum Balances** page (`/agnum/balances`).
2. They see virtual balance rows (product + qty) for a given `sndid` warehouse.
3. They click **Distribute** on a row, pick a physical location and quantity.
4. MES creates a `RECEIPT` stock movement for that SKU into that location.
5. The virtual balance row shows `DistributedQty` so the operator sees what is still unallocated.

---

## What NOT to do in Slice 2

- Do not touch old Agnum export files (frozen list in `MVP-SCOPE.md`).
- Do not implement 2D/3D search — that is Slice 3.
- Do not build saga or MassTransit state machine for distribution.
- Do not POST anything to the Agnum API.
- Do not lock the full virtual balance atomically (optimistic is fine for MVP).

---

## Architecture decision

The distribution spans two stores:
- `AgnumVirtualWarehouseBalance` lives in EF Core (`WarehouseDbContext`).
- `StockLedger` is event-sourced (Marten).

Use two sequential steps inside the command handler — no distributed transaction:
1. Issue `RecordStockMovementCommand` via MediatR → writes to `StockLedger`.
2. On success: write `AgnumBalanceDistribution` row to EF Core (separate `SaveChangesAsync`).

If step 2 fails after step 1 succeeds → stock is correct, distribution row is missing → operator can re-distribute (idempotency guard via `CommandId` on `RecordStockMovementCommand` prevents double stock entry).

---

## Data model changes

### New table: `agnum_balance_distributions`

Add entity `AgnumBalanceDistribution` to `AgnumImportEntities.cs`
(`src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/Entities/AgnumImportEntities.cs`):

```csharp
public class AgnumBalanceDistribution
{
    public Guid Id { get; set; }
    public Guid VirtualBalanceId { get; set; }         // FK → agnum_virtual_warehouse_balances
    public int SndId { get; set; }
    public int AgnumProductId { get; set; }
    public string Sku { get; set; } = string.Empty;    // Item.InternalSKU
    public string LocationCode { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Guid StockMovementCommandId { get; set; }   // CommandId sent to RecordStockMovementCommand
    public DateTime DistributedAt { get; set; }
    public string DistributedBy { get; set; } = string.Empty;

    public AgnumVirtualWarehouseBalance VirtualBalance { get; set; } = null!;
}
```

Add `DbSet` in `WarehouseDbContext.cs`:
```csharp
public DbSet<AgnumBalanceDistribution> AgnumBalanceDistributions => Set<AgnumBalanceDistribution>();
```

Add configuration in `OnModelCreating`:
```csharp
modelBuilder.Entity<AgnumBalanceDistribution>(entity =>
{
    entity.ToTable("agnum_balance_distributions");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Sku).HasMaxLength(100).IsRequired();
    entity.Property(e => e.LocationCode).HasMaxLength(50).IsRequired();
    entity.Property(e => e.WarehouseId).HasMaxLength(50).IsRequired();
    entity.Property(e => e.Quantity).HasPrecision(18, 4).IsRequired();
    entity.Property(e => e.DistributedBy).HasMaxLength(200).IsRequired();
    entity.HasIndex(e => e.VirtualBalanceId);
    entity.HasIndex(e => e.StockMovementCommandId).IsUnique();
    entity.HasIndex(e => new { e.SndId, e.AgnumProductId });
    entity.HasOne(e => e.VirtualBalance)
        .WithMany()
        .HasForeignKey(e => e.VirtualBalanceId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

### Migration

Create new EF migration file manually (same pattern as `20260518115100_AddAgnumImportTables.cs`).
Use timestamp `20260520100000_AddAgnumBalanceDistributions`.

The migration must:
- `[DbContext(typeof(WarehouseDbContext))]` attribute on the class — required (no Designer.cs).
- `CreateTable("agnum_balance_distributions", ...)` in `Up()`.
- `DropTable` in `Down()`.
- Use raw `migrationBuilder.Sql(...)` for any seed data — never `InsertData()` without Designer.cs.
- Update `WarehouseDbContextModelSnapshot.cs` to add the new entity block.

---

## Application layer

### Command: `DistributeAgnumBalanceCommand`

New file: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/DistributeAgnumBalanceCommand.cs`

```csharp
public record DistributeAgnumBalanceCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid VirtualBalanceId { get; init; }   // AgnumVirtualWarehouseBalance.Id
    public string LocationCode { get; init; } = string.Empty;  // target physical location
    public string WarehouseId { get; init; } = string.Empty;   // MES WarehouseId for StockLedger
    public decimal Quantity { get; init; }
    public Guid OperatorId { get; init; }
}
```

### Handler: `DistributeAgnumBalanceCommandHandler`

New file: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/DistributeAgnumBalanceCommandHandler.cs`

Dependencies (via constructor injection):
- `WarehouseDbContext` — read virtual balance, write distribution record
- `IMediator` — dispatch `RecordStockMovementCommand`
- `ILogger<DistributeAgnumBalanceCommandHandler>`

Logic:
```
1. Load AgnumVirtualWarehouseBalance by VirtualBalanceId. 404 if not found.
2. Verify Sku is not null/empty (product must be linked to an Item).
3. Compute already-distributed qty:
       alreadyDistributed = sum of AgnumBalanceDistribution.Quantity WHERE VirtualBalanceId = X
4. Guard: Quantity > 0 AND Quantity <= (virtualBalance.Quantity - alreadyDistributed).
5. Build RecordStockMovementCommand:
       CommandId    = Guid.NewGuid()          // new — for idempotency
       CorrelationId = command.CommandId
       WarehouseId  = command.WarehouseId
       SKU          = virtualBalance.Sku
       Quantity     = command.Quantity
       FromLocation = "AGNUM"                 // virtual source location
       ToLocation   = command.LocationCode
       MovementType = MovementType.Receipt    // "RECEIPT"
       OperatorId   = command.OperatorId
       Reason       = $"Agnum distribution sndid={virtualBalance.SndId}"
6. Send via IMediator. Return failure if Result.IsFailure.
7. Write AgnumBalanceDistribution row and SaveChangesAsync.
8. Return Result.Success().
```

---

## Port / interface

Add to `IAgnumBalanceImportService` or create a new port `IAgnumDistributionService`
in `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Ports/`.

Minimal interface:
```csharp
public interface IAgnumDistributionService
{
    Task<DistributionSummary> GetVirtualBalanceSummaryAsync(Guid virtualBalanceId, CancellationToken ct = default);
}

public record DistributionSummary(
    Guid VirtualBalanceId,
    decimal TotalQty,
    decimal DistributedQty,
    decimal RemainingQty,
    IReadOnlyList<AgnumBalanceDistributionDto> Distributions
);
```

---

## API layer

Add two endpoints to `AgnumImportController.cs`
(`src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/AgnumImportController.cs`):

```
POST  /agnum/balances/{id}/distribute
      Body: { locationCode, warehouseId, quantity, operatorId }
      → 200 {} on success, 400 on validation error, 404 if balance not found

GET   /agnum/balances/{id}/distributions
      → list of AgnumBalanceDistribution rows for this virtual balance
```

Also extend the existing `GET /agnum/balances` response (`AgnumVirtualBalanceRow` DTO) to include:
```csharp
public decimal DistributedQty { get; init; }
public decimal RemainingQty { get; init; }
```

Compute these in the query by left-joining `agnum_balance_distributions`:
```csharp
var distributedByBalance = await _db.AgnumBalanceDistributions
    .Where(d => d.SndId == sndId)
    .GroupBy(d => d.VirtualBalanceId)
    .Select(g => new { g.Key, Total = g.Sum(x => x.Quantity) })
    .ToListAsync(ct);
```

---

## WebUI changes

### `Balances.razor` — add Distribute button

Extend the existing `Balances.razor`
(`src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/Agnum/Balances.razor`):

- Add `DistributedQty` and `RemainingQty` columns to the balance table.
- Add a **Distribute** button per row (only active when `Sku != null`).
- On click: open an inline form or simple `<dialog>` with:
  - Location picker: `<select>` populated from `GET /locations?warehouseId=X&type=Bin,Shelf`
  - Warehouse picker: `<select>` populated from `GET /warehouses`
  - Quantity field (decimal, max = RemainingQty)
  - **Submit** → `POST /agnum/balances/{id}/distribute`
- After success: reload the balance row (or the full list).

### `AgnumClient.cs` service

Extend `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Services/AgnumClient.cs`:

```csharp
Task DistributeAsync(Guid virtualBalanceId, string locationCode, string warehouseId, decimal quantity, Guid operatorId, CancellationToken ct = default);
Task<List<AgnumBalanceDistributionDto>> GetDistributionsAsync(Guid virtualBalanceId, CancellationToken ct = default);
```

### `AgnumDtos.cs`

Extend `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Models/AgnumDtos.cs`:
- Add `DistributedQty` and `RemainingQty` to the existing balance row DTO.
- Add `AgnumBalanceDistributionDto` record.
- Add `DistributeRequest` record for the POST body.

---

## Unit tests

New test file: `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/AgnumDistributionCommandTests.cs`

Test cases:
1. `Distribute_ValidCommand_CreatesStockMovementAndDistributionRecord`
2. `Distribute_QuantityExceedsRemaining_ReturnsFailure`
3. `Distribute_SkuNotLinked_ReturnsFailure`
4. `Distribute_VirtualBalanceNotFound_ReturnsFailure`
5. `Distribute_StockMovementFails_DoesNotWriteDistributionRecord`

---

## Locations API prerequisite

The WebUI location picker needs `GET /locations?warehouseId=X`.
Check if this endpoint already exists in `LocationsController.cs`.
If it does not exist, add it. Signature:

```csharp
[HttpGet]
public async Task<IActionResult> GetLocations([FromQuery] string? warehouseId, [FromQuery] string? type, CancellationToken ct)
```

---

## Checklist for Codex

- [ ] Add `AgnumBalanceDistribution` entity to `AgnumImportEntities.cs`
- [ ] Add `DbSet` + `OnModelCreating` config in `WarehouseDbContext.cs`
- [ ] Create migration `20260520100000_AddAgnumBalanceDistributions.cs` with `[DbContext]` attribute, no `InsertData`
- [ ] Update `WarehouseDbContextModelSnapshot.cs`
- [ ] Create `DistributeAgnumBalanceCommand` + handler in `Application/Commands/`
- [ ] Extend `AgnumImportController` with distribute and distributions endpoints
- [ ] Extend `GET /agnum/balances` response with `DistributedQty` / `RemainingQty`
- [ ] Extend `Balances.razor` with distribute UI
- [ ] Extend `AgnumClient.cs` and `AgnumDtos.cs` in WebUI
- [ ] Write unit tests for handler
- [ ] Verify `GET /locations` endpoint exists; add if missing
- [ ] Do not modify any frozen files from `MVP-SCOPE.md`
