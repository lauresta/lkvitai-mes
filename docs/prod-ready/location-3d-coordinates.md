# Location 3D Coordinates (Sprint 7)

## API
- `PUT /api/warehouse/v1/locations/{code}`
- `POST /api/warehouse/v1/locations/bulk-coordinates` (CSV upload)

## CSV Format
- Header: `LocationCode,X,Y,Z,CapacityWeight,CapacityVolume`
- `X`, `Y`, `Z` are required and must be `>= 0`
- `CapacityWeight` and `CapacityVolume` are optional numeric values

## Validation
- Coordinates must be non-negative.
- Exact coordinate overlap is rejected.
- Bulk upload response includes:
  - `successCount`
  - `errorCount`
  - `errors[]`
