# APM Integration (PRD-1646)

## Overview
This implementation adds opt-in Application Insights telemetry on top of the existing OpenTelemetry pipeline.

Key behaviors:
- Automatic request/dependency/exception instrumentation through `Microsoft.ApplicationInsights.AspNetCore`
- Correlation propagation retained via `X-Correlation-ID` middleware and MassTransit publish tracing
- Business telemetry events:
  - `OrderCreated`
  - `ShipmentDispatched`
  - `StockAdjusted`
- Sampling policy:
  - Failed requests and exceptions are always kept
  - Successful requests are sampled using `Apm:SuccessfulRequestSampleRate` (default `0.1`)

## Configuration
`src/LKvitai.MES.Api/appsettings.json` and `appsettings.Development.json` now include:

```json
"ApplicationInsights": {
  "ConnectionString": ""
},
"Apm": {
  "Enabled": false,
  "SuccessfulRequestSampleRate": 0.1,
  "WarehouseCodeClaimType": "warehouse_code"
}
```

Behavior:
- If `ApplicationInsights:ConnectionString` is empty, telemetry service remains safe/no-op.
- Set a valid connection string and `Apm:Enabled=true` to enable full APM flow.

## Validation
Run:

```bash
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln --no-build
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~APMIntegrationTests"
```

Notes:
- Full solution tests currently include pre-existing failures in PII encryption tests; PRD-specific APM tests validate the added APM behavior.
