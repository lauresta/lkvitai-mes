# LKvitai.MES

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
