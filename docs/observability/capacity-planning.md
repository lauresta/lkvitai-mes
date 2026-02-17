# Capacity Planning (PRD-1650)

## Overview
Capacity planning adds runtime capacity metrics, 6-month forecasts, and scaling recommendations.

Implemented components:
- Service: `src/LKvitai.MES.Api/Services/CapacityPlanningService.cs`
- Admin report endpoint: `GET /api/admin/capacity/report`
- Alert simulation endpoint: `POST /api/test/simulate-capacity-alert`
- Metrics exposure in `/metrics`:
  - `capacity_database_size_gb`
  - `capacity_event_count`
  - `capacity_api_request_volume_per_hour`
  - `capacity_location_utilization_percent`

## Forecasting
- Linear projection uses configured growth assumptions over 6 months:
  - database growth/month
  - event growth/day
- Recommendations are generated from threshold checks:
  - database storage threshold
  - location utilization threshold
  - API request pressure proxy

## Configuration
`CapacityPlanning` section in `appsettings*.json` controls:
- storage allocation and warning thresholds
- growth rates
- forecast horizon

## Validation
```bash
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln --no-build
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~CapacityPlanningTests"
```
