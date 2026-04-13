# Warehouse 3D Rack — Final Implementation Blueprint (v1)

## 1. Title and Goal

**What:** Add physical rack structure to the warehouse 3D visualization in LKvitai.MES WebUI.

**Why:** The current visualization renders bins as floating boxes positioned by raw `CoordinateX/Y/Z` fields. There is no visible shelf grid, no rack frame, no sense of physical structure. The client wants to see the actual rack infrastructure — posts, shelf planks, and slot grid — always visible, with bins sitting inside their physical slot positions.

**Effect:**
- Warehouse map shows rack frames and shelf-level planks at all times.
- Every slot (occupied or empty) is always visible as a wireframe cell.
- Bins are rendered inside their physical slot, with inset/padding, as a solid box.
- Clicking a bin still opens the existing details panel (contents, HUs, utilization).
- Search still highlights bins by SKU or location code.
- Legacy bins without rack placement continue to render via fallback (no regression).

---

## 2. Current State — What Exists and Must Not Break

### Entities (in `MasterDataEntities.cs`)

| Entity | Key Fields | Status |
|--------|-----------|--------|
| `Location` | `Code`, `Barcode`, `Type`, `CoordinateX/Y/Z`, `WidthMeters/LengthMeters/HeightMeters`, `Aisle`, `Rack`, `Level`, `Bin`, `CapacityWeight/Volume` | **Keep all fields, add 4 nullable** |
| `WarehouseLayout` | `WarehouseCode`, `Width/Length/HeightMeters`, `Zones[]` (jsonb) | **Keep, add `RacksJson` jsonb column** |
| `ZoneDefinition` | `ZoneType`, `X1/Y1/X2/Y2`, `Color` | **Keep unchanged** |

### Projections / Read Models (unchanged)

- `HandlingUnit` — LPN, Type, Status, Location, Lines
- `AvailableStockView` — OnHandQty, HardLockedQty, AvailableQty per location
- `LocationBalanceView` — balance per (warehouseId, location, SKU)
- `ActiveHardLockView` — current HARD locks

### API Endpoints (must stay backward compatible)

- `GET /api/warehouse/v1/visualization/3d` — must return existing response shape + optional new fields
- All location management endpoints — unchanged

### UI Behavior (must stay working)

- Click bin → existing details panel (status, utilization, coordinates, HUs list)
- Search by SKU or location code → highlight bin
- Toolbar, zone overlays, floor plane, camera controls

### Frontend Files

| File | Status |
|------|--------|
| `warehouseVisualization.js` (Three.js renderer) | **Extend additively** |
| `Warehouse3D.razor` (page + details panel) | **Extend additively** |
| `VisualizationDtos.cs` (DTOs) | **Extend additively** |

---

## 3. Final v1 Decisions

These are fixed. No alternatives.

| # | Decision |
|---|----------|
| **D-01** | Bins stay. Existing `Location` entity is the bin. Do not replace it. |
| **D-02** | Slot grid is always visible, including empty slots. This is the primary visual effect the client wants. |
| **D-03** | Bins are an occupancy layer rendered on top of the slot grid. |
| **D-04** | For bins with rack placement, shelf geometry (`RacksJson`) is the source of truth for 3D coordinates. `CoordinateX/Y/Z` becomes fallback only. |
| **D-05** | Old `CoordinateX/Y/Z` is fallback for legacy bins without rack placement. Two geometry modes must not be equal — shelf mode is primary when placement fields are set. |
| **D-06** | Slots are computed in v1. No `Slot` entity or table. Slot geometry derives from rack dimensions + level config + `slotsPerLevel`. |
| **D-07** | Span is horizontal only in v1. No vertical span. |
| **D-08** | Span is always contiguous (consecutive slot indices). No gaps within a span. |
| **D-09** | Box inside slot has inset/padding (0.04 m each side). Merged box for span bins has one outer padding across the full span range — no internal gaps inside the span. |
| **D-10** | Overlap is validated by range rules (`startSlot <= endSlot_of_existing AND endSlot >= startSlot_of_existing`). A unique index on `SlotStart` alone is NOT sufficient and MUST NOT be used as the sole guard. |
| **D-11** | Click on bin → existing behavior unchanged. |
| **D-12** | Click on empty slot → details panel shows canonical address + "Empty". |
| **D-13** | Search highlights bins only (not empty slots) in v1. |

---

## 4. Recommended Data Model

### What Stays (no change)

- All existing `Location` columns
- All existing `WarehouseLayout` columns and `Zones` jsonb
- All projections and read models

### What Is Added

**On `warehouse_layouts` table:**
```sql
RacksJson jsonb NULL
```

Contains array of `RackRow` objects (see §7 for JSON shape).

**On `locations` table — 5 nullable columns:**
```sql
RackRowId        varchar(30)  NULL
ShelfLevelIndex  int          NULL
SlotStart        int          NULL
SlotSpan         int          NULL DEFAULT 1
LocationRole     varchar(20)  NULL   -- 'Cell' | 'Bulk' | 'EndCap' | 'Overflow' | 'GroundSlot'
```

**DB constraints:**
```sql
-- All-or-nothing: if any placement field is set, all required ones must be set
ALTER TABLE locations ADD CONSTRAINT ck_locations_rack_placement CHECK (
  ("RackRowId" IS NULL AND "ShelfLevelIndex" IS NULL AND "SlotStart" IS NULL)
  OR
  ("RackRowId" IS NOT NULL AND "ShelfLevelIndex" IS NOT NULL AND "SlotStart" IS NOT NULL)
);

-- LocationRole allowed values
ALTER TABLE locations ADD CONSTRAINT ck_locations_role
  CHECK ("LocationRole" IN ('Cell', 'Bulk', 'EndCap', 'Overflow', 'GroundSlot') OR "LocationRole" IS NULL);

-- No unique index on SlotStart alone — overlap is enforced at application layer (range check)
-- Partial index for lookup performance only:
CREATE INDEX "IX_Locations_RackPlacement"
  ON locations ("RackRowId", "ShelfLevelIndex", "SlotStart")
  WHERE "RackRowId" IS NOT NULL;
```

### What Is Computed (not stored)

| Value | Computed From |
|-------|--------------|
| `slotWidth` | `rack.width / rack.slotsPerLevel` |
| `slotHeight` | `levels[n+1].heightFromBase - levels[n].heightFromBase` (last level: `rack.height - level.heightFromBase`) |
| Bin 3D `origin` | `rack.origin + (SlotStart - 1) * slotWidth` + `level.heightFromBase` |
| Bin 3D `size` | `SlotSpan * slotWidth - 2*INSET` × `rack.depth - 2*INSET` × `slotHeight - 2*INSET` |
| Empty slot list | all possible `(rack, level, slot)` combinations minus occupied ones |
| Canonical address | `"{warehouseCode}-{rackId}{levelIndex}-{slotStart}"` |
| Rack frame mesh vertices | rack `origin` + `dimensions` + `levels[].heightFromBase` |

### What Is Fallback (legacy bins)

Location rows where `RackRowId IS NULL`:
- 3D coordinates come from existing `CoordinateX/Y/Z`
- Size comes from existing `WidthMeters/LengthMeters/HeightMeters`
- Rendered as floating boxes as today — no regression

---

## 5. Placement Model

**Recommendation for v1: placement fields directly on `Location`.**

| Criterion | Fields on Location | Separate placement table |
|-----------|-------------------|--------------------------|
| Complexity | 4 nullable ALTER TABLE | New table + FK + JOIN |
| Atomicity | Bin and placement are one record | Two records to keep in sync |
| Query | `WHERE RackRowId IS NOT NULL` | JOIN required |
| Overlap check | Simple WHERE on same table | Cross-table check |
| Backward compat | Nulls = old behavior | Extra nullable FK on Location anyway |
| Future: bin moves | UPDATE 4 fields | UPDATE or DELETE+INSERT placement record |

**Why not separate table in v1:** No multi-tenancy of placement (one bin is in one slot at a time). No history needed (audit log, if any, comes from domain events). No polymorphic placement targets. The added join complexity gives zero v1 benefit.

**Trade-off acknowledged:** If v2 needs placement history or time-boxed placements, a `BinPlacementHistory` table can be added later without breaking the current model.

---

## 6. Addressing Model

### Canonical Address

Format: `{warehouseCode}-{rackId}{levelIndex}-{slotStart}`

Examples:
- `2-C3-15` — warehouse 2, rack C, level 3, slot 15
- `2-C3-15+3` — same, span of 3 slots (display variant for span > 1)

### What Is Stored as Fields

| Field | Type | Example |
|-------|------|---------|
| `WarehouseCode` (existing) | string | `"2"` |
| `RackRowId` | string | `"C"` |
| `ShelfLevelIndex` | int | `3` |
| `SlotStart` | int | `15` |
| `SlotSpan` | int | `1` or `3` |

### What Is Derived (not stored)

- Address string `"2-C3-15"` — for display and search only
- `SlotEnd = SlotStart + SlotSpan - 1`

### Human-friendly

`"2-C3-15"` — short, readable, copy-paste friendly, printable on label.

### Backend/runtime

4 separate int/string fields — indexable, overlap-checkable with simple SQL.

### Span in Address

Single slot: `"2-C3-15"` — `SlotSpan = 1`
Multi-slot (display): `"2-C3-15+3"` — spans slots 15, 16, 17 — `SlotSpan = 3`

---

## 7. Editor-Friendly JSON

Stored in `WarehouseLayout.RacksJson`. Two top-level arrays: `racks` (physical structure, changes rarely) and `bins` (placement, changes when bins are reconfigured).

```json
{
  "warehouseCode": "2",
  "racks": [
    {
      "id": "C",
      "type": "PalletRack",
      "origin": { "x": 5.0, "y": 8.0, "z": 0.0 },
      "dimensions": { "width": 12.0, "depth": 1.1, "height": 5.4 },
      "orientationDeg": 0,
      "slotsPerLevel": 6,
      "bayCount": 6,
      "backToBack": false,
      "pairedWithRackId": null,
      "levels": [
        { "index": 1, "heightFromBase": 0.10 },
        { "index": 2, "heightFromBase": 1.80 },
        { "index": 3, "heightFromBase": 3.50 }
      ]
    },
    {
      "id": "D",
      "type": "PalletRack",
      "origin": { "x": 5.0, "y": 9.2, "z": 0.0 },
      "dimensions": { "width": 12.0, "depth": 1.1, "height": 5.4 },
      "orientationDeg": 0,
      "slotsPerLevel": 6,
      "bayCount": 6,
      "backToBack": true,
      "pairedWithRackId": "C",
      "levels": [
        { "index": 1, "heightFromBase": 0.10 },
        { "index": 2, "heightFromBase": 1.80 },
        { "index": 3, "heightFromBase": 3.50 }
      ]
    },
    {
      "id": "FLOOR-A",
      "type": "FloorStorage",
      "origin": { "x": 40.0, "y": 2.0, "z": 0.0 },
      "dimensions": { "width": 8.0, "depth": 6.0, "height": 2.0 },
      "orientationDeg": 0,
      "slotsPerLevel": 0,
      "bayCount": 0,
      "backToBack": false,
      "pairedWithRackId": null,
      "levels": []
    }
  ],
  "bins": [
    {
      "locationCode": "BIN-C3-05",
      "rackId": "C",
      "level": 3,
      "slot": 5,
      "span": 1
    },
    {
      "locationCode": "PAL-WIDE-01",
      "rackId": "C",
      "level": 3,
      "slot": 1,
      "span": 3
    }
  ]
}
```

Notes:
- `type` values: `PalletRack` | `FloorStorage` | `WallShelf` | `Custom`. Renderer uses this to decide geometry (slot grid only for `PalletRack` and `WallShelf`).
- `bayCount` = number of bays (sections between upright pairs). Renderer draws `bayCount + 1` upright frames evenly spaced along rack width. If `bayCount = 0` or omitted: only corner posts.
- `FloorStorage` type: no levels, no slot grid. Renderer draws only a floor footprint rectangle + optional low fence outline.
- `bins` section is used only for bulk import / admin configuration. At runtime, placement comes from `Location` DB columns.
- `origin` is the left-front-bottom corner of the rack.
- `orientationDeg` is `0` (along X axis) or `90` (along Y axis).
- `pairedWithRackId` — informational for back-to-back pairs, not used in address scheme.

---

## 8. Runtime/Render JSON

`GET /api/warehouse/v1/visualization/3d` response — extended, backward compatible.

New optional top-level arrays: `racks`, `slots`. Existing `bins` array extended with optional fields.

```json
{
  "warehouseCode": "2",
  "room": { "width": 60.0, "length": 30.0, "height": 8.0 },
  "zones": [
    { "type": "STORAGE", "bounds": { "x1": 2, "y1": 2, "x2": 55, "y2": 28 }, "color": "#E5E7EB" }
  ],
  "racks": [
    {
      "id": "C",
      "origin": { "x": 5.0, "y": 8.0, "z": 0.0 },
      "dimensions": { "width": 12.0, "depth": 1.1, "height": 5.4 },
      "orientationDeg": 0,
      "slotWidth": 2.0,
      "levels": [
        { "index": 1, "z": 0.10, "slotHeight": 1.70 },
        { "index": 2, "z": 1.80, "slotHeight": 1.70 },
        { "index": 3, "z": 3.50, "slotHeight": 1.90 }
      ]
    }
  ],
  "slots": [
    {
      "address": "2-C1-1",
      "rackId": "C",
      "level": 1,
      "slotNumber": 1,
      "origin": { "x": 5.0, "y": 8.0, "z": 0.10 },
      "size": { "width": 2.0, "depth": 1.1, "height": 1.70 },
      "occupied": false
    },
    {
      "address": "2-C3-1",
      "rackId": "C",
      "level": 3,
      "slotNumber": 1,
      "origin": { "x": 5.0, "y": 8.0, "z": 3.50 },
      "size": { "width": 2.0, "depth": 1.1, "height": 1.90 },
      "occupied": true
    }
  ],
  "bins": [
    {
      "locationId": 42,
      "code": "BIN-C3-05",
      "address": "2-C3-5",
      "rackId": "C",
      "level": 3,
      "startSlot": 5,
      "span": 1,
      "origin": { "x": 13.04, "y": 8.04, "z": 3.54 },
      "size": { "width": 1.92, "depth": 1.02, "height": 1.82 },
      "status": "FULL",
      "color": "#F97316",
      "isReserved": false,
      "utilizationPercent": 85.0,
      "handlingUnits": [
        { "id": "uuid", "lpn": "PAL-0042", "sku": "WIDGET-A100", "qty": 120.0 }
      ]
    },
    {
      "locationId": 99,
      "code": "PAL-WIDE-01",
      "address": "2-C3-1+3",
      "rackId": "C",
      "level": 3,
      "startSlot": 1,
      "span": 3,
      "origin": { "x": 5.04, "y": 8.04, "z": 3.54 },
      "size": { "width": 5.92, "depth": 1.02, "height": 1.82 },
      "status": "MEDIUM",
      "color": "#FBBF24",
      "isReserved": false,
      "utilizationPercent": 60.0,
      "handlingUnits": [
        { "id": "uuid", "lpn": "PAL-0099", "sku": "HEAVY-PART-X", "qty": 45.0 }
      ]
    },
    {
      "locationId": 7,
      "code": "BIN-LEGACY-001",
      "address": null,
      "rackId": null,
      "level": null,
      "startSlot": null,
      "span": null,
      "origin": { "x": 10.5, "y": 3.2, "z": 0.0 },
      "size": { "width": 1.0, "depth": 0.8, "height": 1.5 },
      "status": "EMPTY",
      "color": "#94A3B8",
      "isReserved": false,
      "utilizationPercent": 0.0,
      "handlingUnits": []
    }
  ]
}
```

Rules:
- `racks` and `slots` arrays are absent (or empty) when `RacksJson` is null — existing renderer works unchanged.
- Legacy bins (`rackId: null`) get `origin`/`size` from `CoordinateX/Y/Z` as today.
- Rack-placed bins get `origin`/`size` computed from shelf geometry.
- `slots[].occupied = true` for slots covered by any bin's `[startSlot, startSlot+span-1]` range.

---

## 9. Geometry Rules

### Axes / Coordinate System

- Three.js uses Y-up. Server sends coordinates in Three.js space.
- Warehouse floor is at Y=0.
- `origin` in all DTOs is **center-bottom** of the box (Three.js mesh position = center, so frontend adds `height/2` to Y for placement).
- OR: server sends center of box directly (recommended to keep JS simple). Document which convention is chosen and apply consistently.

**Recommendation:** Server sends `origin` as the **left-front-bottom corner** of each object. Frontend computes mesh center as `origin + size/2` for Three.js positioning. This matches the editor JSON convention and is easier to verify by eye.

### Rack Geometry (type = PalletRack | WallShelf)

```
Upright frames (vertical post pairs):
  - Total upright pairs = bayCount + 1
  - Evenly spaced: uprightSpacing = rack.width / bayCount
  - Each upright pair = 2 posts (front + back), same X position, offset Z by rack.depth
  - Post geometry: BoxGeometry(0.06, rack.height, 0.06)
  - Color: #475569

  If bayCount = 0: render only 2 corner upright pairs (4 posts total).
  If bayCount > 0: render bayCount + 1 upright pairs at x = 0, x = spacing, ..., x = width.

Horizontal beams per level:
  - BoxGeometry(rack.width, 0.04, rack.depth)  ← spans full width (schematic, ±15cm ok)
  - Y position: rack.origin.y + level.heightFromBase
  - Color: #94A3B8

For back-to-back pairs (rack.backToBack = true):
  - Posts on the shared (inner) side are shared with paired rack → v1: render 8 posts per pair
    (visual overlap acceptable — optimize in v2)
  - No back wall between paired racks
```

### Rack Geometry (type = FloorStorage)

```
No upright posts. No levels. No slot grid.
Render:
  - Floor footprint rectangle (PlaneGeometry at Y = 0.01 to float above floor)
    size: rack.dimensions.width × rack.dimensions.depth
    color: #D1FAE5 (soft green), opacity 0.35
  - Low perimeter fence:
    4 thin box outlines, height = 0.12m, color: #6EE7B7, opacity 0.7
```

### Level Geometry

- `level.z = rack.origin.z (Y in Three.js) + level.heightFromBase`
- `level.slotHeight = nextLevel.heightFromBase - thisLevel.heightFromBase`
- Last level: `slotHeight = rack.height - thisLevel.heightFromBase`

### Slot Geometry

```
slotWidth = rack.dimensions.width / rack.slotsPerLevel

slot_n:
  origin.x = rack.origin.x + (n - 1) * slotWidth   (n is 1-based)
  origin.y = rack.origin.y (Three.js Z converted to Y)
  origin.z = level.z

size:
  width  = slotWidth
  depth  = rack.dimensions.depth
  height = level.slotHeight
```

### Bin Box Geometry (single slot)

```
INSET = 0.04  (meters, each side)

bin.origin.x = slot.origin.x + INSET
bin.origin.y = slot.origin.y + INSET
bin.origin.z = slot.origin.z + INSET

bin.size.width  = slotWidth - 2 * INSET
bin.size.depth  = rack.depth - 2 * INSET
bin.size.height = slotHeight - 2 * INSET
```

### Bin Box Geometry (span > 1)

```
spanWidth = span * slotWidth

bin.origin.x = slot_startSlot.origin.x + INSET   ← left edge of startSlot + INSET
bin.size.width = spanWidth - 2 * INSET            ← full span width minus ONE outer INSET on each side
bin.size.depth  = rack.depth - 2 * INSET
bin.size.height = slotHeight - 2 * INSET

→ No internal gaps between covered slots. One merged box, one outer padding.
```

### Padding / Inset Rules

- `INSET = 0.04 m` (configurable server-side constant, not per-bin)
- Applied symmetrically to all 6 faces
- For span bins: applied ONLY to the outer boundary of the full span, never between slots
- Rack posts and shelf planks are NOT affected by bin inset

---

## 10. Rendering Rules

### Always Rendered (static layer, regardless of data)

1. **Floor plane** — `PlaneGeometry(room.width, room.length)`, light gray `#F1F5F9`, Y=0
2. **Zone overlays** — semi-transparent rectangles on floor (existing, unchanged)
3. **Rack frames** — posts + beams per rack in `racks[]` array (new, only when `racks` non-empty)
4. **Shelf planks** — horizontal beams at each `level.z` (new, only when `racks` non-empty)
5. **Empty slot wireframes** — one wireframe box per `slots[]` entry where `occupied=false` (new)

### Empty Slot Rendering

```javascript
// InstancedMesh for performance
geometry: BoxGeometry(slot.size.width * 0.92, slot.size.height * 0.3, slot.size.depth * 0.92)
material: MeshBasicMaterial({ color: 0xCBD5E1, wireframe: true, opacity: 0.25, transparent: true })
// Or: thin LineSegments (EdgesGeometry) — same visual result, cheaper
```

### Occupancy Layer (rendered on top of rack grid)

- Bins from `bins[]` array — solid colored boxes with edge outlines (existing material setup, unchanged)
- Span bins: one mesh, wider box (see §9)
- Legacy bins (no rack placement): rendered as before, floating boxes

### Selection / Highlight

- **Clicked bin:** existing emissive + scale effect (unchanged)
- **Hovered bin:** existing hover effect (unchanged)
- **Clicked empty slot:** show address in details panel — `"2-C3-4 — Empty"`. No mesh effect needed for v1.
- **Search result highlight:** existing flyTo + selection ring on bin (unchanged). In v1 search does not highlight empty slots.

### Click Behavior

| Click target | Action |
|-------------|--------|
| Bin mesh | Existing: `SelectBinAsync(code)` → details panel with HUs, utilization, etc. |
| Empty slot mesh | New: show canonical address + "Empty" in details panel |
| Rack post / beam | No action |
| Floor | Deselect current |

### Search Highlight

No change. Bins only. Address search: match `bin.code` OR `bin.address` field (new — add `address` to search index in `WarehouseVisualizationSearch`).

---

## 11. Validation Rules

### Layout Validation (on save of `RacksJson`)

| # | Rule | Check |
|---|------|-------|
| V-L-01 | Rack ID unique | `rack.id` unique within the warehouse |
| V-L-02 | Rack geometry positive | `width > 0`, `depth > 0`, `height > 0` |
| V-L-02b | Rack type valid | `type` in `['PalletRack', 'FloorStorage', 'WallShelf', 'Custom']` |
| V-L-02c | FloorStorage has no levels | `type = 'FloorStorage'` → `levels` must be empty, `slotsPerLevel = 0` |
| V-L-02d | bayCount non-negative | `bayCount >= 0` |
| V-L-03 | Slots per level valid | `slotsPerLevel >= 1` |
| V-L-04 | Level index valid | `>= 1`, unique per rack, contiguous (no gaps) |
| V-L-05 | Level heightFromBase valid | `>= 0` AND `< rack.height` |
| V-L-06 | Levels ordered ascending | `levels[n].heightFromBase < levels[n+1].heightFromBase` |
| V-L-07 | Rack within room bounds | `rack.origin.x + rack.width <= room.width`, etc. |
| V-L-08 | Rack AABB overlap | No two racks share any volume (XZ footprint check) |
| V-L-09 | Paired rack exists | `pairedWithRackId` references a valid rack ID in same layout |
| V-L-10 | Paired rack is adjacent | Paired racks must be touching (shared edge) — warn if not |

### Bin Placement Validation (on add/update of rack placement fields)

| # | Rule | Check |
|---|------|-------|
| V-P-01 | Rack exists | `RackRowId` in `layout.racks` |
| V-P-02 | Level exists | `ShelfLevelIndex` in `rack.levels` |
| V-P-03 | SlotStart in range | `SlotStart >= 1` AND `SlotStart <= rack.slotsPerLevel` |
| V-P-04 | Span valid | `SlotSpan >= 1` |
| V-P-05 | End slot in range | `SlotStart + SlotSpan - 1 <= rack.slotsPerLevel` |
| V-P-06 | No range overlap | See SQL below — MUST be enforced at application layer |
| V-P-07 | No cross-level span | `SlotSpan` never spans across levels (not needed in v1 by design) |
| V-P-08 | All-or-nothing | All four placement fields set or all null |

**Range overlap check (application layer, required):**
```sql
SELECT 1 FROM locations
WHERE "RackRowId" = :rackId
  AND "ShelfLevelIndex" = :levelIndex
  AND "SlotStart" <= :newEndSlot               -- existing bin starts before or at new end
  AND "SlotStart" + "SlotSpan" - 1 >= :newStartSlot  -- existing bin ends after or at new start
  AND "Id" != :currentLocationId               -- exclude self (for updates)
```

Where `newEndSlot = :newStartSlot + :newSpan - 1`.

**A unique index on `SlotStart` alone is explicitly PROHIBITED as the only overlap guard** — it would allow `[5, span=3]` and `[7, span=1]` to coexist when they should not.

---

## 12. Migration Strategy

### What Stays Unchanged

- All `Location` rows — all existing columns, all existing FK references
- All `WarehouseLayout` rows — `Zones` jsonb untouched
- All API response consumers — existing fields present as before
- All Blazor pages except `Warehouse3D.razor` (which is extended, not replaced)
- All projections, sagas, domain aggregates

### What Is Added Minimally

1. **DB migration:** 4 nullable columns on `locations` + `RacksJson` jsonb on `warehouse_layouts` + constraint + partial index
2. **Server-side geometry calculator:** C# service that reads `RacksJson` + `Location` placement fields → computes rack/slot/bin 3D data
3. **Extended API DTO:** `racks[]` + `slots[]` added to response; `bins[]` entries get optional rack fields
4. **Additive JS rendering:** `if (data.racks?.length) { renderRackFrames(); renderSlots(); }` — existing bin rendering untouched
5. **Layout editor extension:** admin page gets rack JSON textarea (same approach as current zones JSON editor)

### Fallback Guarantee

```
Location where RackRowId IS NULL:
  → rendered from CoordinateX/Y/Z as today
  → zero visual regression

WarehouseLayout where RacksJson IS NULL:
  → response has no `racks[]` / `slots[]` arrays
  → JS renderer skips rack rendering path
  → existing bins render as floating boxes as today
```

### Gradual Rollout

1. Deploy DB migration (non-breaking, all new columns are nullable)
2. Deploy API extension (backward compatible — new fields only)
3. Deploy JS extension (guard: `if (data.racks?.length > 0)`)
4. Admin configures first rack layout via JSON editor
5. Admin assigns placement fields to bins via UI/import
6. Remaining unassigned bins continue to render via fallback

---

## 13. DB / API / UI Change Plan

### DB Changes

```sql
-- Migration: AddRackPlacementToLocations
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

-- Migration: AddRacksJsonToWarehouseLayouts
ALTER TABLE warehouse_layouts
  ADD COLUMN "RacksJson" jsonb NULL;
```

### Backend / Domain Changes

| File/Class | Change |
|-----------|--------|
| `Location` entity | Add 4 nullable properties |
| `WarehouseLayout` entity | Add `RacksJson` property (string, deserialize on use) |
| `RackLayoutDto` | New C# record: `RackRow`, `ShelfLevel` |
| `WarehouseGeometryCalculator` | New service: takes layout JSON + locations → produces runtime DTOs |
| `VisualizationBinDto` | Add optional: `Address`, `RackId`, `Level`, `StartSlot`, `Span` |
| `VisualizationRackDto` | New DTO: rack render data with pre-computed `slotWidth`, levels |
| `VisualizationSlotDto` | New DTO: per-slot address + origin + size + occupied flag |
| `BinPlacementValidator` | New: validates placement fields against layout, checks range overlap |
| EF Core migration | `AddRackPlacementToLocations` |

### API Changes

| Endpoint | Change |
|---------|--------|
| `GET /visualization/3d` | Response: add `racks[]`, `slots[]`; enrich `bins[]` with rack fields |
| `PUT /locations/{id}/rack-placement` | New endpoint: set `RackRowId/ShelfLevelIndex/SlotStart/SlotSpan` with validation |
| `DELETE /locations/{id}/rack-placement` | New endpoint: clear placement fields |
| `GET /warehouse-layouts/{code}/rack-config` | New endpoint: return/validate `RacksJson` |
| `PUT /warehouse-layouts/{code}/rack-config` | New endpoint: save `RacksJson` with layout validation |

### Frontend Changes

| File | Change |
|------|--------|
| `warehouseVisualization.js` | Add `renderRackFrames()`, `renderShelfPlanks()`, `renderEmptySlots()`, `handleSlotClick()` — all additive behind `if (data.racks?.length)` guard |
| `Warehouse3D.razor` | Add rack/level/slot fields to details panel `<dl>`; add empty-slot details state |
| `VisualizationDtos.cs` | Add `VisualizationRackDto`, `VisualizationSlotDto`; extend `VisualizationBinDto` |

### Layout Editor Changes

| File | Change |
|------|--------|
| `/admin/warehouse-layout-editor` (existing page) | Add `RacksJson` textarea section with validation feedback |

---

## 14. Implementation Order for Codex

Execute in this order. Each step is independently deployable.

**Step 1 — DB Migration**
- Add 5 nullable columns to `locations` (4 placement + `LocationRole`)
- Add `RacksJson` jsonb to `warehouse_layouts`
- Add check constraints and partial index
- EF Core migration file + `DbContext` update

**Step 2 — Domain / DTO Layer**
- Add 5 nullable properties to `Location` entity (`RackRowId`, `ShelfLevelIndex`, `SlotStart`, `SlotSpan`, `LocationRole`)
- Add `RacksJson` property to `WarehouseLayout` entity
- Create `RackRow`, `ShelfLevel` C# records (for JSON deserialization)
- Create `VisualizationRackDto`, `VisualizationSlotDto`
- Extend `VisualizationBinDto` with optional rack fields

**Step 3 — Geometry Calculator**
- Create `WarehouseGeometryCalculator` service
- Input: `WarehouseLayout` (with `RacksJson`) + list of `Location` rows
- Output: `VisualizationRackDto[]`, `VisualizationSlotDto[]`, enriched `VisualizationBinDto[]`
- Include `INSET = 0.04` constant
- Compute slot grid for every rack × level × slot number
- Mark slots as occupied where a bin's `[startSlot, startSlot+span-1]` covers them
- Legacy bins (no placement): pass through with existing coordinate fields

**Step 4 — Placement Validator**
- Create `BinPlacementValidator`
- Validate rack exists in layout JSON
- Validate level exists in rack
- Validate slot range (start, end within slotsPerLevel)
- Run range-overlap SQL check against other `Location` rows in same rack+level
- Return structured validation result (not exception)

**Step 5 — API Extension**
- Extend `GET /visualization/3d` controller action
  - Call `WarehouseGeometryCalculator` if `RacksJson` is non-null
  - Add `racks`, `slots` to response (empty arrays if no rack config)
  - Backward compatible: existing `bins` fields still present
- Add `PUT /locations/{id}/rack-placement` — set placement with `BinPlacementValidator`
- Add `DELETE /locations/{id}/rack-placement` — clear placement fields
- Add `GET/PUT /warehouse-layouts/{code}/rack-config` — manage `RacksJson` with layout validation

**Step 6 — Frontend: Rack Rendering**
- In `warehouseVisualization.js`:
  - Add `renderRackFrames(scene, racks)` — posts + top/bottom horizontal beams
  - Add `renderShelfPlanks(scene, racks)` — level plank for each level
  - All inside `if (data.racks?.length > 0)` guard
- Bin rendering stays unchanged (bins now have pre-computed coords from server)

**Step 7 — Frontend: Slot Grid**
- Add `renderEmptySlots(scene, slots)` — wireframe boxes for `occupied=false` slots
- Use `InstancedMesh` for performance (can be many slots)
- Add `handleSlotClick(slotAddress)` — show address + "Empty" in details panel

**Step 8 — Frontend: Details Panel Extension**
- In `Warehouse3D.razor`: add optional `<dl>` rows for Address, Rack, Level, Slot
- Add empty-slot state: `selectedSlot` with address display
- Address search: extend `WarehouseVisualizationSearch` to match `bin.address`

**Step 9 — Layout Editor**
- In admin layout editor page: add `RacksJson` JSON textarea
- On save: call `PUT /warehouse-layouts/{code}/rack-config`
- Show validation errors from server inline

**Step 10 — Testing**
- Unit: `WarehouseGeometryCalculator` — slot coordinate computation, inset, span merge, overlap detection
- Unit: `BinPlacementValidator` — overlap cases, boundary cases, all-or-nothing constraint
- Integration: `GET /visualization/3d` with and without `RacksJson` (backward compat)
- E2E: manual — configure rack JSON → assign bin placements → verify 3D map renders correctly

---

## 15. Non-Goals for v1

- No vertical span (bin spanning multiple levels)
- No variable slot widths within a level
- No non-rectangular rack shapes
- No real-time WebSocket updates for occupancy
- No slot reservation or locking through the visualization
- No bin history or placement history
- No drag-and-drop bin placement in the 3D view
- No 3D print or export of rack configuration
- No automatic bin-to-slot assignment (always manual/import)
- No mobile-optimized rendering path

---

## 16. Acceptance Criteria

All criteria are testable and binary (pass/fail).

| # | Criterion | How to Verify |
|---|-----------|--------------|
| AC-01 | Rack frame renders when `RacksJson` is configured | Navigate to 3D view, rack posts and beams are visible |
| AC-02 | Shelf planks render at correct heights | Count planks, match `levels[].heightFromBase` in config |
| AC-03 | Slot grid is always visible including empty slots | Empty slots show as wireframe boxes; removing bins does not hide grid |
| AC-04 | Bin renders inside its slot with visible gap to rack | Box is inset; rack posts visible behind/beside bins |
| AC-05 | Span-3 bin renders as single merged box (no internal gap) | Place bin with `span=3`; one continuous box without seams |
| AC-06 | Click on bin still shows HU content panel | Click any bin; details panel shows HUs, utilization — unchanged |
| AC-07 | Click on empty slot shows canonical address | Click empty slot; details show `"2-C3-4 — Empty"` |
| AC-08 | Search by SKU highlights the correct bin | Search for a known SKU; camera flies to bin, highlight appears |
| AC-09 | Legacy bins (no placement) render without regression | Warehouse with no `RacksJson`: bins float at CoordinateX/Y/Z as before |
| AC-10 | Overlap is rejected at API level | `PUT /locations/{id}/rack-placement` with overlapping range returns 422 with error |
| AC-11 | Span overflow is rejected | `slot=5, span=3` where `slotsPerLevel=6` → 422 error |
| AC-12 | Missing rack reference is rejected | Placement referencing non-existent `rackId` → 422 error |
| AC-13 | `RacksJson` with duplicate rack IDs is rejected | `PUT rack-config` → 422 with `rack.id` uniqueness error |
| AC-14 | Warehouse without `RacksJson`: no JS errors | No rack/slot arrays in response; renderer does not throw |
| AC-15 | EF Core migration is reversible | `dotnet ef migrations remove` or Down() works cleanly |

---

## 17. Risks / Caveats

| # | Risk | Likelihood | Mitigation |
|---|------|-----------|------------|
| R-01 | `RacksJson` deserialization performance for large warehouses (many racks × levels × slots) | Low | Pre-compute slot list server-side once per request; cache per warehouse code |
| R-02 | `InstancedMesh` for slot grid hits Three.js limits (>10k instances) | Low for v1 | 6 racks × 3 levels × 10 slots = 180 instances; acceptable |
| R-03 | Back-to-back shared post logic is complex to implement without visual artifact | Medium | For v1, render 8 posts per rack pair (visually overlapping). Optimize in v2. |
| R-04 | Overlap check is application-layer only — concurrent requests could create overlap | Low | Single-threaded mutation (admin UI), no concurrent slot assignment expected in v1 |
| R-05 | Admin-edited JSON can be malformed — silent failure if not validated | Medium | Always validate `RacksJson` on save with full `LayoutValidator` before storing |
| R-06 | CoordinateX/Y/Z on legacy bins uses different axis convention than rack model | High | Document and enforce one convention; test legacy bins render in same scene as rack bins |

---

## 18. Example Warehouse Config (Reference / Seed)

This is a **full example** demonstrating every feature the renderer supports in v1.
Use it as:
- a reference when creating real warehouse configs
- a seed for development/demo environment
- a test fixture for geometry calculator unit tests

The example uses a fictional warehouse `"EXAMPLE"` with all rack types, back-to-back pairs, floor storage, span bins, and legacy (unplaced) bins.

```json
{
  "warehouseCode": "EXAMPLE",
  "room": { "width": 50.0, "length": 25.0, "height": 8.0 },
  "zones": [
    { "type": "RECEIVING",  "bounds": { "x1": 0,  "y1": 0,  "x2": 10, "y2": 25 }, "color": "#BFDBFE" },
    { "type": "STORAGE",    "bounds": { "x1": 10, "y1": 0,  "x2": 40, "y2": 25 }, "color": "#D1FAE5" },
    { "type": "SHIPPING",   "bounds": { "x1": 40, "y1": 0,  "x2": 50, "y2": 25 }, "color": "#FEF3C7" }
  ],
  "racks": [
    {
      "id": "A",
      "type": "PalletRack",
      "comment": "Single-sided rack, 4 bays, 3 levels, 8 slots per level",
      "origin": { "x": 12.0, "y": 2.0, "z": 0.0 },
      "dimensions": { "width": 8.0, "depth": 1.1, "height": 5.5 },
      "orientationDeg": 0,
      "slotsPerLevel": 8,
      "bayCount": 4,
      "backToBack": false,
      "pairedWithRackId": null,
      "levels": [
        { "index": 1, "heightFromBase": 0.10 },
        { "index": 2, "heightFromBase": 1.90 },
        { "index": 3, "heightFromBase": 3.70 }
      ]
    },
    {
      "id": "B",
      "type": "PalletRack",
      "comment": "Back-to-back pair FRONT with rack C. 6 bays, 3 levels.",
      "origin": { "x": 12.0, "y": 6.0, "z": 0.0 },
      "dimensions": { "width": 12.0, "depth": 1.1, "height": 5.5 },
      "orientationDeg": 0,
      "slotsPerLevel": 6,
      "bayCount": 6,
      "backToBack": true,
      "pairedWithRackId": "C",
      "levels": [
        { "index": 1, "heightFromBase": 0.10 },
        { "index": 2, "heightFromBase": 1.90 },
        { "index": 3, "heightFromBase": 3.70 }
      ]
    },
    {
      "id": "C",
      "type": "PalletRack",
      "comment": "Back-to-back pair BACK with rack B.",
      "origin": { "x": 12.0, "y": 7.2, "z": 0.0 },
      "dimensions": { "width": 12.0, "depth": 1.1, "height": 5.5 },
      "orientationDeg": 0,
      "slotsPerLevel": 6,
      "bayCount": 6,
      "backToBack": true,
      "pairedWithRackId": "B",
      "levels": [
        { "index": 1, "heightFromBase": 0.10 },
        { "index": 2, "heightFromBase": 1.90 },
        { "index": 3, "heightFromBase": 3.70 }
      ]
    },
    {
      "id": "D",
      "type": "WallShelf",
      "comment": "Wall-mounted shelf along east wall, 2 levels, no bays (corner posts only)",
      "origin": { "x": 12.0, "y": 22.0, "z": 0.0 },
      "dimensions": { "width": 20.0, "depth": 0.6, "height": 2.4 },
      "orientationDeg": 0,
      "slotsPerLevel": 10,
      "bayCount": 0,
      "backToBack": false,
      "pairedWithRackId": null,
      "levels": [
        { "index": 1, "heightFromBase": 0.05 },
        { "index": 2, "heightFromBase": 1.10 }
      ]
    },
    {
      "id": "FLOOR-RCV",
      "type": "FloorStorage",
      "comment": "Receiving floor zone — no shelves, no slots. Shows footprint + fence only.",
      "origin": { "x": 1.0, "y": 1.0, "z": 0.0 },
      "dimensions": { "width": 8.0, "depth": 10.0, "height": 2.0 },
      "orientationDeg": 0,
      "slotsPerLevel": 0,
      "bayCount": 0,
      "backToBack": false,
      "pairedWithRackId": null,
      "levels": []
    },
    {
      "id": "FLOOR-BULK",
      "type": "FloorStorage",
      "comment": "Bulk storage floor zone, rotated 90 degrees.",
      "origin": { "x": 35.0, "y": 5.0, "z": 0.0 },
      "dimensions": { "width": 10.0, "depth": 6.0, "height": 3.0 },
      "orientationDeg": 90,
      "slotsPerLevel": 0,
      "bayCount": 0,
      "backToBack": false,
      "pairedWithRackId": null,
      "levels": []
    }
  ],
  "bins": [
    {
      "locationCode": "A-L1-S1",
      "comment": "Normal single-slot bin, level 1, slot 1",
      "rackId": "A",
      "level": 1,
      "slot": 1,
      "span": 1,
      "role": "Cell"
    },
    {
      "locationCode": "A-L1-S2",
      "comment": "Normal single-slot bin, level 1, slot 2",
      "rackId": "A",
      "level": 1,
      "slot": 2,
      "span": 1,
      "role": "Cell"
    },
    {
      "locationCode": "A-L2-S3-WIDE",
      "comment": "SPAN BIN — occupies slots 3-4-5 on level 2 (span=3). Renders as one merged box.",
      "rackId": "A",
      "level": 2,
      "slot": 3,
      "span": 3,
      "role": "Bulk"
    },
    {
      "locationCode": "A-ENDCAP-L3",
      "comment": "EndCap location — last slot on level 3, role=EndCap.",
      "rackId": "A",
      "level": 3,
      "slot": 8,
      "span": 1,
      "role": "EndCap"
    },
    {
      "locationCode": "B-L1-S1",
      "comment": "Front rack of back-to-back pair",
      "rackId": "B",
      "level": 1,
      "slot": 1,
      "span": 1,
      "role": "Cell"
    },
    {
      "locationCode": "C-L1-S1",
      "comment": "Back rack of back-to-back pair",
      "rackId": "C",
      "level": 1,
      "slot": 1,
      "span": 1,
      "role": "Cell"
    },
    {
      "locationCode": "D-SHELF-S5",
      "comment": "Wall shelf bin, level 1",
      "rackId": "D",
      "level": 1,
      "slot": 5,
      "span": 1,
      "role": "Cell"
    },
    {
      "locationCode": "LEGACY-FLOAT-001",
      "comment": "LEGACY BIN — no rack placement. Rendered from CoordinateX/Y/Z as before. Tests backward compat.",
      "rackId": null,
      "level": null,
      "slot": null,
      "span": null,
      "role": "GroundSlot"
    }
  ]
}
```

### How to use this example

1. Create a `WarehouseLayout` row with `WarehouseCode = "EXAMPLE"` and paste this JSON into `RacksJson`.
2. Create `Location` rows matching the `locationCode` values in `bins[]`, set their `RackRowId/ShelfLevelIndex/SlotStart/SlotSpan/LocationRole` from the example.
3. For `LEGACY-FLOAT-001`: create a Location with no rack fields, set `CoordinateX/Y/Z` to e.g. `{45, 12, 0}`.
4. Navigate to `/warehouse/3d?code=EXAMPLE` and verify:
   - 6 distinct rack structures visible
   - Back-to-back pair (B+C) rendered side-by-side
   - Floor storage zones (FLOOR-RCV, FLOOR-BULK) show footprint outline only
   - Slot grid visible on all `PalletRack` and `WallShelf` racks
   - Span bin on rack A level 2 slots 3-5 renders as one continuous box
   - Legacy bin LEGACY-FLOAT-001 floats at its coordinates without rack context
   - All empty slots visible as wireframes
