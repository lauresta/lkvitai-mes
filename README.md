# LKvitai.MES
[![Build and Push Docker Images](https://img.shields.io/github/actions/workflow/status/lauresta/lkvitai-mes/build-and-push.yml?branch=main&label=Build%20%26%20Push)](https://github.com/lauresta/lkvitai-mes/actions/workflows/build-and-push.yml)
[![Deploy to Test](https://img.shields.io/github/actions/workflow/status/lauresta/lkvitai-mes/deploy-test.yml?branch=main&label=Deploy%20Test)](https://github.com/lauresta/lkvitai-mes/actions/workflows/deploy-test.yml)
[![Warehouse CI](https://img.shields.io/github/actions/workflow/status/lauresta/lkvitai-mes/warehouse-ci.yml?branch=main&label=Warehouse%20CI)](https://github.com/lauresta/lkvitai-mes/actions/workflows/warehouse-ci.yml)
[![E2E Tests](https://img.shields.io/github/actions/workflow/status/lauresta/lkvitai-mes/e2e-tests.yml?branch=main&label=E2E%20Tests)](https://github.com/lauresta/lkvitai-mes/actions/workflows/e2e-tests.yml)
[![Architecture Checks](https://img.shields.io/github/actions/workflow/status/lauresta/lkvitai-mes/architecture-checks.yml?branch=main&label=Architecture%20Checks)](https://github.com/lauresta/lkvitai-mes/actions/workflows/architecture-checks.yml)
[![GHCR Warehouse API](https://img.shields.io/badge/GHCR-lkvitai--mes--warehouse--api-2ea44f?logo=github)](https://github.com/lauresta/lkvitai-mes/pkgs/container/lkvitai-mes-warehouse-api)
[![GHCR Warehouse WebUI](https://img.shields.io/badge/GHCR-lkvitai--mes--warehouse--webui-2ea44f?logo=github)](https://github.com/lauresta/lkvitai-mes/pkgs/container/lkvitai-mes-warehouse-webui)
[![GHCR Manuals](https://img.shields.io/badge/GHCR-lkvitai--mes--manuals-2ea44f?logo=github)](https://github.com/lauresta/lkvitai-mes/pkgs/container/lkvitai-mes-manuals)

Warehouse modular monolith (Phase refactor branch).

## Repository Layout

- `src/` - application code and solution (`src/LKvitai.MES.sln`)
- `tests/` - automated tests
- `docs/` - architecture, blueprint, audit, and status documents
- `scripts/` - operational and deployment scripts

## Refactor Blueprint

- Source of truth: `docs/blueprints/repo-refactor-blueprint.md`
- Validation report: `docs/blueprints/blueprint-validation-report.md`

## Quick Start

### Prerequisites

| Requirement | Where to get |
|-------------|-------------|
| **.NET SDK 8.0.418** | https://dotnet.microsoft.com/download/dotnet/8.0 — exact version pinned via `global.json` |
| **Docker Desktop** | https://www.docker.com/products/docker-desktop |
| **CLIP ONNX model** (~350 MB) | See step 3 below — required for Search by Image |

### Step 1 — Start infrastructure

```bash
# PostgreSQL 15 + Redis 7.2 + Jaeger (from repo root)
docker compose -f src/docker-compose.yml up -d
```

| Service | Port | URL |
|---------|------|-----|
| PostgreSQL | 5432 | `localhost:5432` · DB: `lkvitai_warehouse_dev` · user/pass: `postgres/postgres` |
| Redis | 6379 | `localhost:6379` |
| Jaeger UI | 16686 | http://localhost:16686 |

### Step 2 — Apply database migrations

```bash
dotnet tool install --global dotnet-ef   # once
dotnet ef database update \
  --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure \
  --startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api
```

> Requires `pgvector` PostgreSQL extension. The `warehouse-postgres` container uses `postgres:15` image
> which does **not** include pgvector — run once after first start:
> ```sql
> -- connect to warehouse-postgres and run:
> CREATE EXTENSION IF NOT EXISTS vector;
> ```

### Step 3 — Download CLIP model (Search by Image)

Without the model the app starts fine but `/search-by-image` returns 503.

**macOS / Linux:**
```bash
mkdir -p /tmp/models
curl -L "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx" \
     -o /tmp/models/item-image-model.onnx
```

**Windows (PowerShell):**
```powershell
New-Item -ItemType Directory -Force -Path "C:\models"
Invoke-WebRequest `
  -Uri "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx" `
  -OutFile "C:\models\item-image-model.onnx"
```

### Step 4 — Create launchSettings.json (not committed)

Create `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "LKvitai.MES.Modules.Warehouse.Api": {
      "commandName": "Project",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "Caching__RedisConnectionString": "localhost:6379,abortConnect=false,connectRetry=3,connectTimeout=1000,syncTimeout=1000",
        "UseJaegerExporter": "true",
        "Jaeger__AgentHost": "localhost",
        "ITEMIMAGES__ENDPOINT": "lauresta-bin.vpn.lauresta.com:9000",
        "ITEMIMAGES__BUCKET": "lkvitai-dev",
        "ITEMIMAGES__USESSL": "false",
        "ITEMIMAGES__ACCESSKEY": "<minio-access-key>",
        "ITEMIMAGES__SECRETKEY": "<minio-secret-key>",
        "ITEMIMAGES__MAXUPLOADMB": "5",
        "ITEMIMAGES__CACHEMAXAGESECONDS": "86400",
        "ITEMIMAGES__MODEL_PATH": "/tmp/models/item-image-model.onnx"
      },
      "applicationUrl": "https://localhost:5001;http://localhost:5000"
    }
  }
}
```

> **Debugging from a Windows VM** while Docker runs on Mac (VPN IP e.g. `10.11.12.4`):
> - `Caching__RedisConnectionString` → `10.11.12.4:6379,...`
> - `Jaeger__AgentHost` → `10.11.12.4`
> - `ITEMIMAGES__MODEL_PATH` → `C:\\models\\item-image-model.onnx`

### Step 5 — Run

```bash
dotnet restore src/LKvitai.MES.sln
dotnet build src/LKvitai.MES.sln -c Release
dotnet run --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api
```

Or open `src/LKvitai.MES.sln` in Visual Studio / Rider and run from IDE.

**Default URLs:**
- API: https://localhost:5001
- WebUI: https://localhost:5101

### Environment Variables Reference

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `Caching__RedisConnectionString` | Yes | in `appsettings.Development.json` | Redis connection string |
| `ITEMIMAGES__ENDPOINT` | Yes | — | MinIO `host:port` (no scheme, e.g. `minio.example.com:9000`) |
| `ITEMIMAGES__BUCKET` | Yes | — | MinIO bucket name |
| `ITEMIMAGES__ACCESSKEY` | Yes | — | MinIO access key |
| `ITEMIMAGES__SECRETKEY` | Yes | — | MinIO secret key |
| `ITEMIMAGES__USESSL` | No | `false` | Use HTTPS for MinIO |
| `ITEMIMAGES__MAXUPLOADMB` | No | `5` | Max photo upload size in MB |
| `ITEMIMAGES__CACHEMAXAGESECONDS` | No | `86400` | Photo proxy cache TTL in seconds |
| `ITEMIMAGES__MODEL_PATH` | No* | — | Absolute path to CLIP ViT-B/32 ONNX file. *Required for Search by Image |
| `UseJaegerExporter` | No | `false` | `true` → traces to Jaeger UI; `false` → traces to console |
| `Jaeger__AgentHost` | No | `localhost` | Jaeger UDP agent host |

### Build & Test

```bash
dotnet restore src/LKvitai.MES.sln
dotnet build src/LKvitai.MES.sln -c Release
dotnet test src/LKvitai.MES.sln -c Release
```
