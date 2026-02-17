# Grafana Dashboards (PRD-1647)

## Delivered Dashboards
The following dashboards are defined under `grafana/dashboards/`:

- `business-metrics.json`
- `sla-tracking.json`
- `system-health.json`
- `errors.json`
- `capacity-planning.json`

All dashboards:
- use Prometheus datasource (`uid: prometheus`)
- include threshold coloring (green/yellow/red)
- auto-refresh every 30 seconds (`"refresh": "30s"`)

## Grafana Provisioning
Provisioning files:
- `grafana/provisioning/datasources/prometheus.yml`
- `grafana/provisioning/dashboards/dashboards.yml`

Compose integration:
- `docker-compose.yml` includes a `grafana` service on `localhost:3000`
- Dashboard and provisioning directories are mounted read-only

## Validation Commands
```bash
docker compose up -d grafana
docker compose config
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln --no-build
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~GrafanaDashboardTests"
```

## Notes
- Dashboards are template-ready JSON artifacts for import/reuse.
- Prometheus server itself is not provisioned in this PRD; datasource points to `http://prometheus:9090` for environment-level wiring.
