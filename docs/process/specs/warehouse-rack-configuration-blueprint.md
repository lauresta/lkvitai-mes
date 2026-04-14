# Warehouse Rack Configuration Blueprint

## Purpose
- Define how to evolve the current warehouse 3D model from "independent bins rendered as boxes" into a configurable physical warehouse structure.
- Introduce first-class configuration for real storage infrastructure:
  - pallet racks
  - cantilever racks
  - floor storage zones
  - rack bays and levels
- Keep current location-driven WMS behavior working while adding a more realistic rendering model.

## Why This Change Is Needed
- Current 3D visualization is operationally useful but physically naive.
- The system already stores coordinates and box dimensions for locations, but it does not store the warehouse infrastructure itself.
- Realistic rendering requires a separate model for:
  - room envelope
  - storage structures
  - storage cells inside those structures
- Without this split, the renderer can only draw floating boxes and outline edges, not actual shelving or rack frames.

## Current State

### Existing Configuration
- `WarehouseLayout` stores:
  - `WarehouseCode`
  - `WidthMeters`
  - `LengthMeters`
  - `HeightMeters`
  - rectangular `Zones`
- `Location` stores:
  - `CoordinateX/Y/Z`
  - `WidthMeters/LengthMeters/HeightMeters`
  - `Aisle/Rack/Level/Bin`
  - capacity metadata
- `GET /api/warehouse/v1/visualization/3d` builds output primarily from `Location` rows.
- Web UI renders each bin as `THREE.BoxGeometry`.

### Practical Limitation
- `WarehouseLayout` describes the container.
- `Location` describes addressable storage cells.
- There is no persisted model for the physical rack system between them.

## Target Model

### Conceptual Layers
1. `WarehouseLayout`
   Keeps room envelope and high-level zones.
2. `StorageStructure`
   Physical infrastructure inside the warehouse.
3. `Location`
   WMS-addressable storage cell mapped to a structure position.
4. `HandlingUnit` / stock
   Inventory content placed into a location.

### Design Rule
- `Location` must remain the operational WMS entity.
- `StorageStructure` must become the physical rendering/configuration entity.
- Do not overload `ZoneDefinition`.
- Do not make a single `Location` represent both a rack and a storage cell.

## Proposed Domain Model

### New Entity: `StorageStructure`
- Represents one physical storage installation.
- Suggested fields:
  - `Id`
  - `WarehouseLayoutId`
  - `Code`
  - `Type`
  - `Name`
  - `OriginX`
  - `OriginY`
  - `OriginZ`
  - `RotationDegrees`
  - `WidthMeters`
  - `LengthMeters`
  - `HeightMeters`
  - `Color`
  - `MaterialPreset`
  - `IsActive`
  - `MetadataJson`

### `StorageStructure.Type`
- `PalletRack`
- `CantileverRack`
- `FloorStorage`
- `BlockStack`
- `WallMountedShelf`
- `Custom`

### New Entity: `StorageStructureBay`
- Represents one horizontal section of a structure.
- Suggested fields:
  - `Id`
  - `StorageStructureId`
  - `BayIndex`
  - `OffsetX`
  - `OffsetY`
  - `OffsetZ`
  - `WidthMeters`
  - `LengthMeters`
  - `HeightMeters`
  - `MaxLoadKg`

### New Entity: `StorageStructureLevel`
- Represents a vertical storage level within a bay.
- Suggested fields:
  - `Id`
  - `StorageStructureBayId`
  - `LevelIndex`
  - `BottomZ`
  - `ClearHeightMeters`
  - `BeamThicknessMeters`
  - `MaxLoadKg`
  - `IsGroundLevel`

### Extend `Location`
- Keep existing fields.
- Add physical mapping fields:
  - `StorageStructureId` nullable
  - `BayIndex` nullable
  - `LevelIndex` nullable
  - `SlotIndex` nullable
  - `LocationRole` nullable
- `LocationRole` examples:
  - `Cell`
  - `Bulk`
  - `EndCap`
  - `Overflow`
  - `GroundSlot`

## Why This Shape Works
- It preserves existing WMS workflows based on `Location`.
- It allows a single rack to own many WMS locations.
- It supports both:
  - realistic rendering
  - future layout editing/import
- It can represent both pallet rack and cantilever logic without forcing one geometry model on all structures.

## Configuration Strategy

### What Should Be Stored as Configuration
- Warehouse room size
- Zones
- Rack and shelving infrastructure
- Rack bay count and level count
- Rack orientation and position
- Visual presets and structural dimensions
- Mapping from physical cells to WMS locations

### What Should Not Be Stored in Rack Config
- Live stock quantity
- utilization status
- reservation state
- handling unit contents

Those remain dynamic operational data derived from existing WMS entities.

## Initial Assumptions for Lauresta Warehouse
- First implementation may use reasonable assumptions derived from:
  - current photos
  - provided warehouse diagrams
  - existing location coordinates
- The model must support later correction of:
  - room dimensions
  - bay widths
  - level heights
  - cantilever arm spacing
- Realism should be additive, not blocking.

## API Blueprint

### Existing Endpoints to Keep
- `GET /api/warehouse/v1/layout`
- `PUT /api/warehouse/v1/layout`
- `GET /api/warehouse/v1/visualization/3d`

### New Endpoints
- `GET /api/warehouse/v1/layout/structures?warehouseCode={code}`
- `PUT /api/warehouse/v1/layout/structures`
- `POST /api/warehouse/v1/layout/structures/import`
- `GET /api/warehouse/v1/visualization/3d/detailed?warehouseCode={code}`

### Extended 3D Response
- Keep current response contract for compatibility.
- Add new sections:
  - `structures`
  - `structureBays`
  - `structureLevels`
  - optionally `locationMappings`

### Suggested Detailed Response Shape
- `warehouse`
- `zones`
- `structures[]`
  - `id`
  - `code`
  - `type`
  - `position`
  - `rotationDegrees`
  - `dimensions`
  - `visual`
  - `bays[]`
    - `bayIndex`
    - `dimensions`
    - `offset`
    - `levels[]`
- `bins[]`
  - keep current contract
  - add:
    - `structureId`
    - `bayIndex`
    - `levelIndex`
    - `slotIndex`

## UI Blueprint

### Admin Configuration
- Extend current layout editor into two layers:
  - room/zones editor
  - storage structures editor
- Short-term acceptable implementation:
  - keep JSON editor
  - add `structures` array to the payload
- Later improvement:
  - visual admin editor with placement handles and snapping

### 3D Rendering
- Render in this order:
  1. room/grid/floor
  2. zones
  3. storage structure geometry
  4. location cells
  5. handling unit overlays
  6. selection/highlight effects

### Rendering Rule
- Structure geometry is static configuration.
- Bin fill and status overlay are dynamic operational data.
- This separation avoids rebuilding rack meshes every time stock data changes.

## Implementation Phases

### Phase 1: Data Model Foundation
- Add new entities:
  - `StorageStructure`
  - `StorageStructureBay`
  - `StorageStructureLevel`
- Extend `Location` with physical mapping columns.
- Add EF configuration and migration.
- Keep current visualization endpoint unchanged.

### Phase 2: Configuration API
- Extend layout contract or add dedicated structure endpoints.
- Support CRUD for structures.
- Support creation of bays and levels.
- Add validation:
  - positive dimensions
  - valid bay and level ordering
  - no duplicate structure code per warehouse
  - no invalid location-to-structure mapping

### Phase 3: Detailed Visualization API
- Add new detailed response with structures plus bins.
- Preserve current `bins` output.
- Populate `structureId/bayIndex/levelIndex/slotIndex` on bins where available.

### Phase 4: Frontend Rendering Upgrade
- Build rack frame meshes from `structures`.
- Continue to render bins as internal occupancy markers.
- Keep current selection/search/filter behavior.
- Add structure-level toggles:
  - show/hide rack frames
  - show/hide occupancy boxes
  - realistic view vs operational view

### Phase 5: Data Migration and Seeding
- Seed first Lauresta warehouse structure config from known drawings and photos.
- Map current locations into rack bays and levels where possible.
- Leave unmapped locations valid but rendered in compatibility mode.

## Backward Compatibility
- Existing `Location` rows must remain valid even if `StorageStructureId` is null.
- Existing `GET /visualization/3d` should keep working.
- Existing stock workflows must not depend on rack config being complete.
- Renderer should support fallback mode:
  - if no structure config exists, render current box-only scene

## Validation Rules

### Structure Validation
- `WidthMeters`, `LengthMeters`, `HeightMeters` > 0
- `RotationDegrees` normalized to `0..359.999`
- structure codes unique within warehouse

### Bay/Level Validation
- bay indexes unique within structure
- level indexes unique within bay
- `BottomZ` strictly increasing by level
- no level extents outside structure height

### Location Mapping Validation
- a mapped location must reference an existing structure
- `BayIndex` must exist within that structure
- `LevelIndex` must exist within that bay
- optional slot overlap rules must prevent multiple locations from occupying the same physical cell unless explicitly allowed

## Migration Approach

### Database
- Add new tables for structures, bays, levels.
- Add nullable mapping fields to `locations`.
- Do not rewrite existing coordinate fields yet.

### Application
- Continue using existing coordinate fields as source of truth during transition.
- Gradually introduce derived coordinates from structure mappings where available.

### Renderer
- Version 1:
  - structures are rendered from config
  - bins still use stored coordinates
- Version 2:
  - bins can derive exact position from structure mapping plus slot offset

## Recommended Minimal First Implementation
- Add only one new top-level entity at first: `StorageStructure`.
- Store bay/level model in `MetadataJson` for speed.
- Extend visualization response with `structures`.
- Render simple rack frames in Three.js.
- Keep locations as current bins.

This is the fastest path to visible value with minimal backend risk.

## Recommended Clean Long-Term Implementation
- Normalize:
  - `StorageStructure`
  - `StorageStructureBay`
  - `StorageStructureLevel`
- Add explicit location mapping fields.
- Derive cell coordinates from physical structure geometry.
- Support multiple visual presets by structure type.

This is the correct path if the warehouse model will become a durable product feature rather than a demo-only enhancement.

## Risks
- Over-modeling too early can slow delivery.
- Under-modeling can trap the renderer in another temporary format.
- Photos and diagrams may still leave some dimensions ambiguous.
- Some current coordinates may not align cleanly with real rack geometry.

## Recommended Decision
- Implement a hybrid approach:
  - normalized `StorageStructure`
  - nullable mapping on `Location`
  - optionally JSON-backed bay/level metadata in first release
- This gives:
  - fast implementation
  - realistic rendering gains
  - a clean migration path to full physical warehouse modeling

## Suggested File Impact
- Domain:
  - `MasterDataEntities.cs`
- Infrastructure:
  - EF migration
  - DbContext configuration
- API:
  - `WarehouseVisualizationController.cs`
  - new or extended layout structure controller
- Web UI:
  - `LayoutEditorDtos.cs`
  - `LayoutEditorClient.cs`
  - `LayoutEditor.razor`
  - `VisualizationDtos.cs`
  - `warehouseVisualization.js`

## Definition of Done for First Milestone
- Rack configuration can be stored per warehouse.
- Existing locations can optionally map to racks.
- Detailed visualization API returns racks plus bins.
- Frontend draws visible rack structures.
- If rack config is missing, old box rendering still works.
- Lauresta warehouse can be seeded with an initial realistic structure layout.
