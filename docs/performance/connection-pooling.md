# Connection Pooling (PRD-1643)

## Npgsql Pool Configuration
- `Minimum Pool Size=10`
- `Maximum Pool Size=100`
- `Connection Lifetime=300`
- `Connection Idle Lifetime=60`
- `Timeout=30`

These values are applied in `MartenConfiguration.AddWarehouseDbContext(...)` via `NpgsqlConnectionStringBuilder` defaults.

## Monitoring
- Interceptor: `ConnectionPoolMonitoringInterceptor`
- Metrics exposed via `/metrics`:
  - `npgsql_connection_active`
  - `npgsql_connection_idle`
  - `npgsql_connection_wait_ms`
  - `npgsql_connection_errors_total`
  - `npgsql_connection_pool_min`
  - `npgsql_connection_pool_max`

## Leak Detection
- Connection-held duration is measured between open/close events.
- Warning emitted when held duration exceeds 30 seconds:
  - `"Potential connection leak detected. Connection held for ... ms."`

## Lifetime Notes
- EF `WarehouseDbContext` is registered with scoped lifetime (`AddDbContext`).
- Marten query sessions are created as lightweight sessions and disposed by `await using`.
