# 2D/3D Toggle and Interaction (Sprint 7)

## Route
- `/warehouse/visualization/3d`
- `/warehouse/visualization/2d`

## Implemented
- 2D/3D toggle on the same visualization page
- Selection preserved across toggle using `selected` query parameter
- Search autocomplete suggestions for location codes
- Search selection triggers 3D fly-to camera animation (1 second) and bin highlight
- Color legend shown on visualization canvas:
  - Empty: gray `#808080`
  - Low: yellow `#FFFF00`
  - Full: orange `#FFA500`
  - Reserved: blue `#0000FF`
  - Over capacity: red `#FF0000`
- 2D zoom controls (`+` and `-`) with centered viewBox zoom

## Tests
- `WarehouseVisualizationSearchTests` cover autocomplete and best-match logic.
