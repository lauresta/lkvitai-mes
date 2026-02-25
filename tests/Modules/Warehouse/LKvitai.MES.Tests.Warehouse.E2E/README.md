# Warehouse UI E2E Tests (Playwright)

This project uses Playwright with xUnit for browser-based UI E2E tests.

## Prerequisites

1. Start the Web UI application.
2. Install Playwright browsers:

```bash
dotnet build tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/LKvitai.MES.Tests.Warehouse.E2E.csproj
pwsh tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/bin/Debug/net8.0/playwright.ps1 install
```

## Environment variables

- `BASE_URL` (optional): Web UI base URL. Default: `http://localhost:5124`
- `HEADLESS` (optional): `true`/`false`. Default: `true`

## Run tests

```bash
dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/LKvitai.MES.Tests.Warehouse.E2E.csproj
```

Failure artifacts are saved under:

`tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/bin/<Configuration>/net8.0/playwright-artifacts/`
