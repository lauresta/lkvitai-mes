# Vector Log Collection (Test)

This setup makes API file logs persistent and ships both docker stdout logs and API file logs to VictoriaLogs.

## 1) API logs persistence in compose

Already added in:

- `docker-compose.test.yml`:
  - `${API_LOGS_DIR:-/opt/lkvitai-mes/logs/api}:/app/logs`
- `docker-compose.prod.yml`:
  - `${API_LOGS_DIR:-/opt/lkvitai-mes/logs/api}:/app/logs`

The API writes files like `warehouse-YYYYMMDD.log` under `/app/logs`.

## 2) Vector config

Use config template:

- `deploy/vector/vector.test.victorialogs.yaml`

It includes:

- `docker_logs` source (API/WebUI)
- `file` source for `/var/log/lkvitai-mes/api/warehouse-*.log`
- parsed fields: `trace_id`, `error_id`
- retry + disk buffer for delivery stability

## 3) Vector container mounts (on test host)

Update vector service mounts to include:

```yaml
volumes:
  - /var/run/docker.sock:/var/run/docker.sock:ro
  - /opt/vector/vector.yaml:/etc/vector/vector.yaml:ro
  - /opt/lkvitai-mes/logs/api:/var/log/lkvitai-mes/api:ro
  - /opt/vector/data:/var/lib/vector
```

## 4) Apply on test host

```bash
sudo mkdir -p /opt/lkvitai-mes/logs/api /opt/vector/data
sudo cp deploy/vector/vector.test.victorialogs.yaml /opt/vector/vector.yaml
docker restart vector
```

## 5) Quick validation

1. Trigger a transfer failure from UI.
2. Query VictoriaLogs by `trace_id` from UI Error ID.
3. Ensure you see events from:
   - `log_kind=docker` (webui/api stdout)
   - `log_kind=file` (api file logs)
