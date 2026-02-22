# Feature Flags

## Implementation
- LaunchDarkly package installed: `LaunchDarkly.ServerSdk`
- Service: `IFeatureFlagService` / `FeatureFlagService`
- Endpoint: `GET /api/warehouse/v1/features/{flagKey}`
- Config section: `FeatureFlags` in appsettings

## Supported Flags
- `enable_3d_visualization` (bool, default `false`)
- `enable_wave_picking` (bool, default `false`)
- `enable_agnum_export` (bool + rollout percent, default `true`)
- `max_order_lines` (number, default `100`)

## Behavior
- Percentage rollout uses deterministic user bucketing.
- User/role targeting supported for wave picking.
- Cached evaluation with TTL (`CacheTtlSeconds`, default `30`).

## Validation
```bash
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --filter "FullyQualifiedName~FeatureFlagTests"
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln
```
