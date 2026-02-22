# Load Balancing (PRD-1645)

## Topology
- `nginx` container listens on `:80`
- Upstream API instances:
  - `api-1:8080`
  - `api-2:8080`
  - `api-3:8080`
- Shared backing services:
  - PostgreSQL `pg:5432`
  - Redis `redis:6379`

## Nginx Behavior
- Round-robin upstream: `warehouse_api`
- Health-failure controls:
  - `max_fails=3`
  - `fail_timeout=30s`
- Keepalive: `keepalive 32`
- Sticky hub traffic:
  - `warehouse_hubs` uses `ip_hash` for `/hubs/*` path

## Health Endpoint
- API health endpoint is available at `GET /health`
- Nginx proxies `/health` to one of the healthy upstream API instances.

## Compose Commands
```bash
docker compose up -d
docker compose ps
curl http://localhost/health
docker compose logs -f nginx
```

## Validation Notes
- Distribution/failover under load requires running environment (`k6` + containerized stack).
- In this implementation pass, config correctness was validated with `docker compose config`.
