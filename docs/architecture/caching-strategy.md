# Caching Strategy (PRD-1642)

## Provider
- Redis 7.2 via `docker-compose.yml` service `redis`
- Connection: `Caching:RedisConnectionString` in API appsettings
- Runtime implementation: `src/LKvitai.MES.Infrastructure/Caching/RedisCacheService.cs`

## Cache-Aside Flows
- `item:{id}` TTL 1 hour
  - Read path: `GET /api/warehouse/v1/items/{id}`
  - Invalidation: item create/update/deactivate/barcode update
- `customer:{id}` TTL 30 minutes
  - Read path: `GET /api/warehouse/v1/customers/{id}`
  - Invalidation: customer create
- `location:{code}` TTL 2 hours
  - Read path: stock controller location materialization
  - Invalidation: location create/update
- `stock:{itemId}:{locationId}` TTL 30 seconds
  - Read path: `GET /api/warehouse/v1/stock/available` (when item/location filters are both present)
  - Invalidation: stock-move projection updates (`RemoveByPrefix("stock:")`)
- `value:{itemId}` TTL 5 minutes
  - Population: valuation on-hand-value query path
  - Invalidation: on-hand-value projection updates

## Graceful Degradation
- If Redis cannot connect or operations fail, cache methods return fallback behavior and do not throw.
- API continues serving DB-backed responses.
- Warning logs are emitted for visibility.

## Metrics
- Endpoint: `GET /metrics`
- Exposed Prometheus-style values:
  - `cache_hit_rate`
  - `cache_hit_total`
  - `cache_miss_total`
  - `cache_latency_ms`
  - `cache_size`
