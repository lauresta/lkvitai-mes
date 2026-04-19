# Location Warehouse Ownership Fix — Codex Implementation Blueprint

**Branch:** `feature/warehouse-3d-rack-blueprint`
**Severity:** Architecture bug — domain model violation
**Root cause:** `Location` is a global entity with no warehouse FK; visualization loads all non-virtual locations regardless of selected warehouse.

---

## Diagnosis Summary

| File | Line | Problem |
|---|---|---|
| `MasterDataEntities.cs` | 1142 | `Location` has no `WarehouseId` or `WarehouseCode` |
| `WarehouseDbContext.cs` | 861 | `HasIndex(e => e.Code).IsUnique()` — global uniqueness must become per-warehouse |
| `WarehouseVisualizationController.cs` | 334 | `_dbContext.Locations.Where(x => !x.IsVirtual)` — no warehouse filter |
| `WarehouseVisualizationController.cs` | 550 | `LoadLayoutAsync` fallback queries ALL locations for dimension inference |
| `LocationsController.cs` | 109 | `CreateAsync` never sets `WarehouseId` |
| `LocationsController.cs` | 33 | `GetAsync` has no `warehouseId` filter parameter |
| `BinPlacementValidator.cs` | 29 | Validates against warehouse layout but never writes warehouse ownership back to Location |

---

## Target Domain Model

```
Warehouse (warehouses table, PK: WarehouseId Guid)
  └── many Locations  (FK: Location.WarehouseId → Warehouse.WarehouseId)
  └── one WarehouseLayout  (warehouse_layouts, keyed by WarehouseCode string)
```

`Location.WarehouseId` is nullable during migration. Required for all new non-virtual locations once migration is done.

---

## Implementation Tasks

---

### TASK 1 — Add `WarehouseId` to the `Location` entity

**File:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/MasterDataEntities.cs`

In the `Location` class (currently at line 1142), add one new property after the existing `LocationRole` property:

```csharp
// ADD after line: public string? LocationRole { get; set; }
public Guid? WarehouseId { get; set; }
```

The full property list for `Location` after this change ends with:
```csharp
public string? RackRowId { get; set; }
public int? ShelfLevelIndex { get; set; }
public int? SlotStart { get; set; }
public int? SlotSpan { get; set; }
public string? LocationRole { get; set; }
public Guid? WarehouseId { get; set; }        // ← NEW

public Location? ParentLocation { get; set; }
public ICollection<Location> Children { get; set; } = new List<Location>();
```

Do not add a navigation property `Warehouse` to `Location` — it is not needed and would pull a dependency on the `WarehouseLayoutAggregate` type into the entity.

---

### TASK 2 — Update EF Core model builder

**File:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/Persistence/WarehouseDbContext.cs`

Find the `modelBuilder.Entity<Location>` block (currently line 835). Make these three changes inside that block:

**2a. Add property mapping** after `entity.Property(e => e.LocationRole).HasMaxLength(20);` (currently line 860):

```csharp
entity.Property(e => e.WarehouseId);   // nullable Guid, no max length needed
```

**2b. Add FK relationship** after `entity.HasOne(e => e.ParentLocation)...` (currently line 870):

```csharp
entity.HasOne<WarehouseLayoutAggregate>()
    .WithMany()
    .HasForeignKey(e => e.WarehouseId)
    .OnDelete(DeleteBehavior.Restrict)
    .IsRequired(false);
```

Note: `WarehouseLayoutAggregate` is already imported at line 11 as a using alias. The FK references `warehouses.WarehouseId` (the Guid PK on that table, confirmed at DbContext line 163).

**2c. Replace the global unique index on Code** (currently line 861):

```csharp
// REMOVE:
entity.HasIndex(e => e.Code).IsUnique();

// REPLACE WITH:
entity.HasIndex(e => e.Code).IsUnique();   // keep for now; see migration note below
entity.HasIndex(e => e.WarehouseId);
entity.HasIndex(e => new { e.WarehouseId, e.Code })
    .HasDatabaseName("IX_Locations_WarehouseCode");
```

> **Note on uniqueness:** The global `Code` unique index is kept in this migration phase because existing data has a global unique constraint and backfill may not cover all rows. The per-warehouse composite index is added alongside it. Once backfill is complete and `WarehouseId NOT NULL` is enforced, a follow-up migration should drop the global unique and enforce `UNIQUE(WarehouseId, Code)` instead. That follow-up is **not** in this blueprint's scope.

---

### TASK 3 — Generate the EF Core migration

Run from the repo root:

```bash
dotnet ef migrations add AddWarehouseOwnershipToLocations \
  --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure \
  --startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api \
  --output-dir Persistence/Migrations
```

The generated migration `Up()` must contain:

```csharp
migrationBuilder.AddColumn<Guid>(
    name: "WarehouseId",
    schema: "public",
    table: "locations",
    type: "uuid",
    nullable: true);

migrationBuilder.AddForeignKey(
    name: "FK_locations_warehouses_WarehouseId",
    schema: "public",
    table: "locations",
    column: "WarehouseId",
    principalTable: "warehouses",
    principalColumn: "WarehouseId",
    onDelete: ReferentialAction.Restrict);

migrationBuilder.CreateIndex(
    name: "IX_Locations_WarehouseId",
    schema: "public",
    table: "locations",
    column: "WarehouseId");

migrationBuilder.CreateIndex(
    name: "IX_Locations_WarehouseCode",
    schema: "public",
    table: "locations",
    columns: new[] { "WarehouseId", "Code" });
```

The `Down()` must reverse all of these.

---

### TASK 4 — Backfill migration SQL (run inside the same EF migration)

After the `AddColumn` in `Up()`, add a `Sql()` backfill call:

```csharp
// Backfill: assign WarehouseId to locations that have RackRowId placement.
// Strategy: parse the rack_row_id prefix before the first '-' to match warehouse layout,
// then join through warehouse_layouts.WarehouseCode → warehouses.WarehouseId.
migrationBuilder.Sql(@"
UPDATE locations l
SET ""WarehouseId"" = w.""WarehouseId""
FROM warehouse_layouts wl
JOIN warehouses w ON w.""Code"" = wl.""WarehouseCode""
WHERE l.""RackRowId"" IS NOT NULL
  AND wl.""RacksJson"" IS NOT NULL
  AND wl.""RacksJson"" LIKE '%' || l.""RackRowId"" || '%';
");
```

> **Implementation note:** This heuristic matches rack row IDs by substring in the JSON blob. It is intentionally conservative — it only assigns warehouse ownership when there is a direct rack placement match. Locations without `RackRowId` (free-placed or virtual) remain `NULL`. After running the migration, query `SELECT COUNT(*), ""WarehouseId"" IS NULL FROM locations GROUP BY 2` to see how many remain unresolved and handle them manually before enforcing NOT NULL.

---

### TASK 5 — Fix `GetVisualization3dAsync` — warehouse-scoped location load

**File:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/WarehouseVisualizationController.cs`

**Current code** (around line 331):
```csharp
var locations = await EfAsync.ToListAsync(
    _dbContext.Locations
        .AsNoTracking()
        .Where(x => !x.IsVirtual)
        .OrderBy(x => x.Code),
    cancellationToken);

if (locations.Count == 0)
{
    locations = await EfAsync.ToListAsync(
        _dbContext.Locations
            .AsNoTracking()
            .OrderBy(x => x.Code),
        cancellationToken);
}
```

**Replace with:**
```csharp
// Resolve the warehouse entity to get its stable WarehouseId
var warehouseEntity = await EfAsync.FirstOrDefaultAsync(
    _dbContext.Warehouses.AsNoTracking(),
    x => x.Code == layout.WarehouseCode,
    cancellationToken);

List<Location> locations;
if (warehouseEntity is not null)
{
    // Proper warehouse-scoped load: only locations that belong to this warehouse
    locations = await EfAsync.ToListAsync(
        _dbContext.Locations
            .AsNoTracking()
            .Where(x => x.WarehouseId == warehouseEntity.WarehouseId && !x.IsVirtual)
            .OrderBy(x => x.Code),
        cancellationToken);

    // If warehouse exists but has no owned locations yet, return empty — do not fall
    // through to the global location pool. An empty warehouse is a valid state.
    // The auto-layout fallback from AvailableStockView still applies below.
}
else
{
    // Warehouse not found in warehouses table (legacy/migration gap) —
    // fall back to unowned non-virtual locations as a degraded path.
    // Log a warning so this is visible and can be cleaned up.
    _logger.LogWarning(
        "Warehouse '{WarehouseCode}' not found in warehouses table; falling back to unowned locations",
        layout.WarehouseCode);
    locations = await EfAsync.ToListAsync(
        _dbContext.Locations
            .AsNoTracking()
            .Where(x => !x.IsVirtual && x.WarehouseId == null)
            .OrderBy(x => x.Code),
        cancellationToken);
}
```

---

### TASK 6 — Fix `LoadLayoutAsync` fallback dimension inference

**File:** same controller, method `LoadLayoutAsync` (line 531)

**Current code** (lines 549–563): queries ALL locations globally to infer width/length/height when no layout record exists.

**Replace the fallback block** (everything from `var maxCoordinate = await...` through the `return new WarehouseLayout {...}`) with:

```csharp
// Fallback: infer dimensions only from locations that belong to THIS warehouse.
// If warehouseEntity is null, use a safe default — never infer from other warehouses' bins.
Guid? fallbackWarehouseId = null;
var fallbackWarehouse = await EfAsync.FirstOrDefaultAsync(
    _dbContext.Warehouses.AsNoTracking(),
    x => x.Code == normalizedWarehouseCode,
    cancellationToken);
fallbackWarehouseId = fallbackWarehouse?.WarehouseId;

IReadOnlyList<dynamic> maxCoordinate;
if (fallbackWarehouseId.HasValue)
{
    maxCoordinate = await EfAsync.ToListAsync(
        _dbContext.Locations
            .AsNoTracking()
            .Where(x => x.WarehouseId == fallbackWarehouseId.Value
                        && x.CoordinateX.HasValue
                        && x.CoordinateY.HasValue
                        && x.CoordinateZ.HasValue)
            .Select(x => new
            {
                X = x.CoordinateX!.Value,
                Y = x.CoordinateY!.Value,
                Z = x.CoordinateZ!.Value
            }),
        cancellationToken);
}
else
{
    // Unknown warehouse — return safe default dimensions.
    maxCoordinate = [];
    _logger.LogWarning(
        "LoadLayoutAsync: warehouse '{WarehouseCode}' not found; using default dimensions",
        normalizedWarehouseCode);
}

var width  = maxCoordinate.Count == 0 ? 50m  : maxCoordinate.Max(x => x.X) + 1m;
var length = maxCoordinate.Count == 0 ? 100m : maxCoordinate.Max(x => x.Y) + 1m;
var height = maxCoordinate.Count == 0 ? 10m  : maxCoordinate.Max(x => x.Z) + 1m;

return new WarehouseLayout
{
    WarehouseCode = normalizedWarehouseCode,
    WidthMeters  = decimal.Round(width,  2, MidpointRounding.AwayFromZero),
    LengthMeters = decimal.Round(length, 2, MidpointRounding.AwayFromZero),
    HeightMeters = decimal.Round(height, 2, MidpointRounding.AwayFromZero),
    UpdatedAt = DateTimeOffset.UtcNow,
    Zones = new List<ZoneDefinition>()
};
```

> **Codex note:** The `dynamic` typing is used above only for illustration. Use the same anonymous-type projection pattern that already exists in the original code — EF Core projects to `<{decimal X, decimal Y, decimal Z}>`. Replace `IReadOnlyList<dynamic>` with the correct anonymous type or extract a local record.

---

### TASK 7 — Fix `LocationsController.CreateAsync` to require and store `WarehouseId`

**File:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/LocationsController.cs`

**7a. Add `WarehouseId` (or `WarehouseCode`) to `UpsertLocationRequest`**

Find the `UpsertLocationRequest` DTO (likely in the same file at the bottom, or in a `Dtos/` file). Add:

```csharp
public Guid? WarehouseId { get; set; }
```

If the request uses `WarehouseCode` (string) instead of a Guid, that is also acceptable — the controller must then resolve the Guid via a lookup. Prefer `WarehouseId` for consistency with the FK.

**7b. Add validation in `CreateAsync`** — after the existing barcode duplicate check (around line 182), add:

```csharp
// Non-virtual locations must belong to a warehouse.
Guid? resolvedWarehouseId = null;
if (!request.IsVirtual)
{
    if (!request.WarehouseId.HasValue)
    {
        return ValidationFailure("Field 'warehouseId' is required for non-virtual locations.");
    }

    var warehouseExists = await _dbContext.Warehouses
        .AsNoTracking()
        .AnyAsync(x => x.WarehouseId == request.WarehouseId.Value, cancellationToken);
    if (!warehouseExists)
    {
        return ValidationFailure($"Warehouse '{request.WarehouseId.Value}' was not found.");
    }

    resolvedWarehouseId = request.WarehouseId.Value;
}
```

**7c. Set `WarehouseId` on the new entity** — in the `new Location { ... }` initializer (currently line 184), add:

```csharp
WarehouseId = resolvedWarehouseId,
```

---

### TASK 8 — Fix `LocationsController.GetAsync` to support warehouse-scoped queries

**File:** same controller, `GetAsync` method (line 33)

**8a. Add optional filter parameter:**

```csharp
[HttpGet]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
public async Task<IActionResult> GetAsync(
    [FromQuery] string? search,
    [FromQuery] string? status,
    [FromQuery] Guid? warehouseId,         // ← ADD
    [FromQuery] bool includeVirtual = true,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 50,
    CancellationToken cancellationToken = default)
```

**8b. Add filter clause** after the `!includeVirtual` block:

```csharp
if (warehouseId.HasValue)
{
    query = query.Where(x => x.WarehouseId == warehouseId.Value);
}
```

This is a non-breaking change — existing callers that omit `warehouseId` continue to receive all locations.

---

### TASK 9 — Fix `BinPlacementValidator` to write warehouse ownership

**File:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/Visualization/BinPlacementValidator.cs`

Find the method `ValidateAsync`. Currently it validates the placement against a warehouse layout but does **not** write `WarehouseId` back to the location.

**9a. Add `WarehouseId` to `RackPlacementValidationResult`**

Find the `RackPlacementValidationResult` record/class (likely in the same file or nearby). Add:

```csharp
public Guid WarehouseId { get; init; }
```

**9b. Populate `WarehouseId` in the return value** — after the warehouse layout is loaded (around line 70 where `layout` is retrieved), resolve the warehouse:

```csharp
var warehouse = await _dbContext.Warehouses
    .AsNoTracking()
    .FirstOrDefaultAsync(x => x.Code == normalizedWarehouseCode, cancellationToken);
if (warehouse is null)
{
    return (null, $"Warehouse '{normalizedWarehouseCode}' was not found in warehouses table.");
}
```

Then include `WarehouseId = warehouse.WarehouseId` in the returned `RackPlacementValidationResult`.

**9c. In the caller** (the controller action that calls `ValidateAsync` and then writes to the database), after a successful placement result, set `location.WarehouseId = result.WarehouseId` before saving.

Find the rack placement endpoint — it is in `WarehouseVisualizationController.cs`. Search for the call to `_binPlacementValidator.ValidateAsync(...)` and add:

```csharp
// After ValidateAsync returns a non-null result:
location.WarehouseId = placement.WarehouseId;
```

---

### TASK 10 — Fix `UpdateAsync` and `UpdateByCodeAsync` in LocationsController

**File:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/LocationsController.cs`

In both update endpoints (lines 253 and 412), add the same warehouse ownership preservation pattern:

- If the request includes a `WarehouseId`, validate it exists and update `entity.WarehouseId`
- If the request does not include `WarehouseId`, do not clear the existing value (never set `entity.WarehouseId = null` on an update)

---

## What Is NOT Changed in This Blueprint

| Component | Reason |
|---|---|
| `WarehouseLayout` entity | Already warehouse-scoped by `WarehouseCode`; no changes needed |
| `StockLedger` / `Reservation` aggregates | Use `Location` code as a string key; not affected |
| `AvailableStockView` / `LocationBalanceView` | Use location code strings; no schema change |
| `BulkCoordinatesAsync` endpoint | Only updates coordinate fields; WarehouseId is not affected |
| Virtual locations (`IsVirtual = true`) | Intentionally warehouse-agnostic; `WarehouseId` remains nullable for them |
| Unique index on `Code` and `Barcode` | Not changed in this migration phase — see note in Task 2c |
| Reports, imports, stock screens | Need warehouse-aware adjustments but are out of scope here |

---

## Migration Safety Rules

1. **Run `Up()` migration during a maintenance window** — the `UPDATE` backfill touches the `locations` table and acquires row locks.
2. **Verify backfill before deploying app code** — query `SELECT COUNT(*) FROM locations WHERE "WarehouseId" IS NULL AND NOT "IsVirtual"` after migration. If non-zero, review and assign manually before releasing.
3. **Deploy app code after migration** — the new nullable column has a default of `NULL`, so the old app version continues to work with the new schema during a rolling deploy.
4. **Do not enforce NOT NULL yet** — leave `WarehouseId` nullable until all existing data is backfilled and verified. A follow-up migration will make it required.

---

## Acceptance Criteria

### Schema

- [ ] `locations` table has a nullable `WarehouseId uuid` column
- [ ] `FK_locations_warehouses_WarehouseId` foreign key exists with `ON DELETE RESTRICT`
- [ ] `IX_Locations_WarehouseId` index exists on `locations.WarehouseId`
- [ ] `IX_Locations_WarehouseCode` composite index exists on `(WarehouseId, Code)`
- [ ] Migration `Down()` fully reverses the schema to the pre-migration state

### Backfill

- [ ] After migration, locations with `RackRowId` have `WarehouseId` populated via the backfill SQL
- [ ] Backfill does not touch virtual locations
- [ ] Backfill does not assign a location to a warehouse it cannot be matched to (no false positives)

### Visualization

- [ ] `GET /api/.../visualization/3d?warehouseCode=WH2` returns only bins that belong to warehouse WH2
- [ ] `GET /api/.../visualization/3d?warehouseCode=WH1` returns only bins that belong to warehouse WH1
- [ ] Creating warehouse WH3 and calling visualization for WH3 returns zero bins (not bins from WH1 or WH2)
- [ ] `LoadLayoutAsync` fallback infers dimensions from only the selected warehouse's locations
- [ ] When a warehouse has no locations and no layout record, the visualization API returns an empty bins list (not a 500 error)

### Location CRUD

- [ ] `POST /api/.../locations` without `warehouseId` for a non-virtual location returns 422
- [ ] `POST /api/.../locations` with an invalid `warehouseId` returns 422
- [ ] `POST /api/.../locations` with a valid `warehouseId` creates the location with that FK set
- [ ] `GET /api/.../locations?warehouseId={id}` returns only locations belonging to that warehouse
- [ ] `GET /api/.../locations` without `warehouseId` continues to return all locations (backwards compatible)
- [ ] Virtual locations can be created without `warehouseId`

### Rack Placement

- [ ] After a successful `POST /api/.../locations/{id}/rack-placement`, the location's `WarehouseId` is set to the warehouse that owns the specified rack row
- [ ] Placement into a rack row that belongs to warehouse WH1 sets `WarehouseId` to WH1's Guid — not null, not another warehouse

### No Regression

- [ ] `dotnet build src/LKvitai.MES.sln -c Release` passes with zero errors and zero warnings
- [ ] `dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/...` passes
- [ ] `dotnet test tests/ArchitectureTests/...` passes
- [ ] `dotnet run --project tools/DependencyValidator/...` passes

---

## Follow-Up Tasks (out of scope for this blueprint)

These are NOT part of this implementation but must be tracked:

| ID | Task |
|---|---|
| FU-1 | Make `Location.WarehouseId` NOT NULL for non-virtual locations once all data is backfilled |
| FU-2 | Drop global `UNIQUE(Code)` index, replace with `UNIQUE(WarehouseId, Code)` |
| FU-3 | Decide on `Barcode` uniqueness scope (global vs. per-warehouse) — needs business confirmation |
| FU-4 | Update admin Location list UI to filter by warehouse (currently shows global list) |
| FU-5 | Update import flows that create locations to require `WarehouseId` |
| FU-6 | Update reports/stock screens that query `Locations` table to add warehouse filter where needed |

---

## Codex Prompt

```
Read the full implementation blueprint at:
  docs/warehouse-location-ownership-fix.md

Then implement every task (TASK 1 through TASK 10) in order.

Key files to change:
  src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/MasterDataEntities.cs
  src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/Persistence/WarehouseDbContext.cs
  src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/WarehouseVisualizationController.cs
  src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/LocationsController.cs
  src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/Visualization/BinPlacementValidator.cs

After TASK 2, run:
  dotnet ef migrations add AddWarehouseOwnershipToLocations \
    --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure \
    --startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api \
    --output-dir Persistence/Migrations
Then edit the generated migration to add the backfill SQL from TASK 4.

Constraints:
  - Do not change WarehouseLayout, StockLedger, Reservation, or projection types.
  - Do not make WarehouseId NOT NULL — leave it nullable (this is Phase 1 of a two-phase migration).
  - Do not break existing callers of GET /locations that omit warehouseId.
  - Virtual locations (IsVirtual = true) do not require WarehouseId.
  - After all changes, run: dotnet build src/LKvitai.MES.sln -c Release
    It must compile with zero errors.
```
