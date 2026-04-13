# Warehouse 3D Rack — Codex Implementation Task

**Reference:** `docs/process/specs/warehouse-3d-rack-blueprint.md` (read it first)
**Branch:** `feature/warehouse-3d-rack-blueprint`

---

## 1. Goal

Add physical rack structure (posts, shelf planks, slot grid) to the warehouse 3D visualization. Slots must always be visible — including empty ones. Bins must render inside their physical slots with visible inset. Existing bin click/details/search behavior must be preserved exactly.

---

## 2. Scope

- DB: 4 nullable columns on `locations` + `RacksJson` jsonb on `warehouse_layouts`
- Backend: geometry calculator service + placement validator + API extensions
- Frontend: rack frame rendering + slot grid rendering + empty slot click + address search
- Layout editor: rack JSON textarea on admin page

---

## 3. Must Keep Working

- `GET /api/warehouse/v1/visualization/3d` — existing response fields unchanged
- Click on bin → details panel with HUs, status, utilization — identical behavior
- Search by SKU or location code → bin highlight — identical behavior
- Bins without rack placement render at `CoordinateX/Y/Z` (legacy fallback)
- Warehouse without `RacksJson` renders exactly as today — no JS errors, no visual regression

---

## 4. Files / Modules Likely Affected

| File / Module | Change type |
|--------------|-------------|
| `MasterDataEntities.cs` | Add 4 nullable properties to `Location`; add `RacksJson` to `WarehouseLayout` |
| EF Core `DbContext` | Register new columns |
| `Migrations/` | New migration `AddRackPlacementToLocations` + `AddRacksJsonToWarehouseLayouts` |
| `VisualizationDtos.cs` | Add `VisualizationRackDto`, `VisualizationSlotDto`; extend `VisualizationBinDto` |
| `WarehouseVisualizationController.cs` | Extend `GET /visualization/3d`; add 4 new endpoints |
| `WarehouseGeometryCalculator.cs` | New service — slot computation, bin coordinate derivation |
| `BinPlacementValidator.cs` | New service — range overlap validation, rack/level/slot range checks |
| `RackLayoutDto.cs` | New records: `RackRow`, `ShelfLevel` |
| `warehouseVisualization.js` | Add `renderRackFrames`, `renderShelfPlanks`, `renderEmptySlots`, slot click handler |
| `Warehouse3D.razor` | Extend details panel; add empty-slot state |
| Admin layout editor page | Add `RacksJson` JSON textarea section |

---

## 5. Required Changes

### 5.1 DB Migration

```sql
ALTER TABLE locations
  ADD COLUMN "RackRowId"       varchar(30) NULL,
  ADD COLUMN "ShelfLevelIndex" int         NULL,
  ADD COLUMN "SlotStart"       int         NULL,
  ADD COLUMN "SlotSpan"        int         NULL DEFAULT 1;

ALTER TABLE locations ADD CONSTRAINT ck_locations_rack_placement CHECK (
  ("RackRowId" IS NULL AND "ShelfLevelIndex" IS NULL AND "SlotStart" IS NULL)
  OR
  ("RackRowId" IS NOT NULL AND "ShelfLevelIndex" IS NOT NULL AND "SlotStart" IS NOT NULL)
);

CREATE INDEX "IX_Locations_RackPlacement"
  ON locations ("RackRowId", "ShelfLevelIndex", "SlotStart")
  WHERE "RackRowId" IS NOT NULL;

ALTER TABLE warehouse_layouts ADD COLUMN "RacksJson" jsonb NULL;
```

### 5.2 Geometry Calculator Contract

```csharp
// Input
record RackGeometryInput(WarehouseLayout Layout, IReadOnlyList<Location> Locations);

// Output
record WarehouseGeometryResult(
    VisualizationRackDto[] Racks,
    VisualizationSlotDto[] Slots,        // ALL slots (occupied + empty)
    VisualizationBinDto[] Bins           // enriched with rack fields where applicable
);
```

Rules:
- `INSET = 0.04m` applied to all 6 bin box faces
- Span bin: one merged box, `width = span * slotWidth - 2 * INSET`, INSET on outer boundary only
- Slot `occupied = true` if ANY bin's `[startSlot, startSlot+span-1]` covers it
- `slot.origin` = left-front-bottom corner of slot volume
- `bin.origin` = left-front-bottom corner of bin box (= slot.origin + INSET vector)
- Legacy bins (RackRowId IS NULL): pass through with `CoordinateX/Y/Z` as origin, no changes

### 5.3 Overlap Validation (MUST implement — unique index is NOT sufficient)

```sql
SELECT 1 FROM locations
WHERE "RackRowId" = :rackId
  AND "ShelfLevelIndex" = :levelIndex
  AND "SlotStart" <= (:newStartSlot + :newSpan - 1)
  AND ("SlotStart" + "SlotSpan" - 1) >= :newStartSlot
  AND "Id" != :currentLocationId
```

Return 422 with error message if any row found.

### 5.4 New API Endpoints

| Method | Path | Action |
|--------|------|--------|
| PUT | `/api/warehouse/v1/locations/{id}/rack-placement` | Set `RackRowId/ShelfLevelIndex/SlotStart/SlotSpan` — validate, save |
| DELETE | `/api/warehouse/v1/locations/{id}/rack-placement` | Clear all 4 fields to null |
| GET | `/api/warehouse/v1/warehouse-layouts/{code}/rack-config` | Return `RacksJson` |
| PUT | `/api/warehouse/v1/warehouse-layouts/{code}/rack-config` | Validate + save `RacksJson` |

### 5.5 Frontend Rendering (additive only)

```javascript
// In render() — add AFTER existing floor/zone rendering:
if (data.racks && data.racks.length > 0) {
    renderRackFrames(scene, data.racks);     // posts + top/bottom beams
    renderShelfPlanks(scene, data.racks);    // plank per level
    renderEmptySlots(scene, data.slots.filter(s => !s.occupied));
}
// Existing bin rendering unchanged — bins now have server-computed coords
```

Empty slot mesh: `LineSegments(EdgesGeometry(BoxGeometry(...)))`, color `#CBD5E1`, opacity 0.3.

On click: if raycaster hits an empty slot mesh, show `"{slot.address} — Empty"` in details panel.

### 5.6 `VisualizationBinDto` Extension

Add these optional fields (null for legacy bins):
- `string? Address` — canonical `"2-C3-5"` or `"2-C3-1+3"` for span
- `string? RackId`
- `int? Level`
- `int? StartSlot`
- `int? Span`

Existing fields (`Code`, `Status`, `Color`, `IsReserved`, `UtilizationPercent`, `HandlingUnits`, `Origin`, `Size`) unchanged.

---

## 6. Validation Expectations

### Layout JSON validation (reject with 422):
- Duplicate `rack.id` within warehouse
- Any `rack.dimensions.width/depth/height <= 0`
- `slotsPerLevel < 1`
- Level `index` not unique or not contiguous per rack
- Level `heightFromBase < 0` or `>= rack.height`
- Levels not in ascending `heightFromBase` order
- Rack bounding box outside room bounds
- Two racks with overlapping AABB (XZ footprint)
- `pairedWithRackId` references non-existent rack

### Bin placement validation (reject with 422):
- `RackRowId` not in layout racks
- `ShelfLevelIndex` not in rack levels
- `SlotStart < 1` or `SlotStart > slotsPerLevel`
- `SlotSpan < 1`
- `SlotStart + SlotSpan - 1 > slotsPerLevel`
- Range overlap with existing bin in same rack+level (range check — see §5.3)
- Only some of the required placement fields are set (partial = invalid)

---

## 7. Backward Compatibility Rules

- `GET /visualization/3d` response: existing JSON fields MUST be present and unchanged. New fields (`racks`, `slots`, additional bin fields) are additive.
- If `RacksJson` is null: `racks = []`, `slots = []` in response. No JS errors.
- If `Location.RackRowId` is null: bin rendered from `CoordinateX/Y/Z/Width/Length/Height` as before.
- No changes to any read model (HandlingUnit, AvailableStock, LocationBalance, ActiveHardLocks).
- No changes to any command or domain aggregate.

---

## 8. Done Criteria

- [ ] EF Core migration runs cleanly (`dotnet ef database update`)
- [ ] `dotnet build src/LKvitai.MES.sln -c Release` — zero errors, zero warnings
- [ ] `dotnet test` — all existing tests pass
- [ ] `GET /visualization/3d` with no `RacksJson`: response identical to before (backward compat)
- [ ] `GET /visualization/3d` with `RacksJson`: response includes `racks[]` and `slots[]` with correct geometry
- [ ] 3D view renders rack frames and slot grid when rack config is present
- [ ] 3D view renders normally (no JS errors) when rack config is absent
- [ ] Click on bin: existing details panel, unchanged
- [ ] Click on empty slot: shows `"{address} — Empty"`
- [ ] `PUT /locations/{id}/rack-placement` with overlapping span: returns 422
- [ ] `PUT /warehouse-layouts/{code}/rack-config` with duplicate rack IDs: returns 422
- [ ] Architecture tests pass (`dotnet test tests/ArchitectureTests/...`)
- [ ] Dependency validator passes (`dotnet run --project tools/DependencyValidator/...`)

---

## 9. Non-Goals

- No vertical span (bin on multiple shelf levels)
- No variable slot widths
- No real-time WebSocket occupancy updates
- No drag-and-drop in 3D view
- No bin placement history
- No back-to-back shared post optimization (render 8 posts per pair for v1, optimize later)
- No mobile-specific rendering path
- No automatic bin-to-slot assignment
