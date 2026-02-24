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

```bash
dotnet restore src/LKvitai.MES.sln
dotnet build src/LKvitai.MES.sln -c Release
dotnet test src/LKvitai.MES.sln -c Release
```
