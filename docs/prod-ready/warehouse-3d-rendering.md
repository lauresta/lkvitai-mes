# 3D Warehouse Rendering (Sprint 7)

## Route
- `/warehouse/visualization/3d`

## API
- `GET /api/warehouse/v1/visualization/3d`

## Implemented
- Three.js scene with Perspective camera, ambient/directional lights, OrbitControls
- Bin rendering from API coordinates and dimensions
- Color coding:
  - `EMPTY` gray `#808080`
  - `LOW` yellow `#FFFF00`
  - `FULL` orange `#FFA500`
  - `RESERVED` blue `#0000FF`
  - `OVER_CAPACITY` red `#FF0000`
- Bin click selection/highlight and right-side details panel
- Zone/status filters in toolbar
- Details panel shows utilization percentage and handling units
- Details action links to `/warehouse/locations/{id}`
- Refresh button to reload API data
