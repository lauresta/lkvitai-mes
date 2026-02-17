# SLA Monitoring (PRD-1649)

## Overview
SLA monitoring is implemented with runtime metric collection plus an admin report endpoint.

Implemented components:
- SLA metric tracking service: `src/LKvitai.MES.Api/Services/SlaMonitoringService.cs`
- Request duration capture middleware: `src/LKvitai.MES.Api/Middleware/SlaMetricsMiddleware.cs`
- SLA report endpoint: `src/LKvitai.MES.Api/Api/Controllers/AdminSlaController.cs`
- Metrics exposure updates: `src/LKvitai.MES.Api/Api/Controllers/MetricsController.cs`

## Exposed Metrics (`/metrics`)
- `sla_uptime_percentage`
- `sla_api_response_time_p95`
- `sla_projection_lag_seconds`
- `sla_order_fulfillment_rate`

## Report Endpoint
`POST /api/admin/sla/report?month=yyyy-MM`

Returns a generated report payload as `application/pdf` attachment (`sla-report-yyyy-MM.pdf`) containing:
- uptime
- API p95
- projection lag
- order fulfillment
- breach incident count (derived from target comparisons)

## Configuration
`appsettings*.json` includes `SlaMonitoring`:
- request window size
- tracking window days
- SLA targets
- planned downtime minutes

## Validation
```bash
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln --no-build
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~SLAMonitoringTests"
```
