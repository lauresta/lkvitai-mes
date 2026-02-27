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
- `SLOWMO_MS` (optional): Playwright slow motion delay in milliseconds. Default: `0`
- `PWDEBUG` (optional): set to `1` to force headed mode; if `SLOWMO_MS` is not set, defaults to `250`

### Headed debug example (Windows cmd)

```cmd
set HEADLESS=false
set SLOWMO_MS=250
set PWDEBUG=1
dotnet test tests\Modules\Warehouse\LKvitai.MES.Tests.Warehouse.E2E\LKvitai.MES.Tests.Warehouse.E2E.csproj
```

### Headed debug example (PowerShell)

```powershell
$env:BASE_URL = "http://localhost:5124"
$env:HEADLESS = "false"
$env:SLOWMO_MS = "250"
$env:PWDEBUG = "1"
dotnet test tests\Modules\Warehouse\LKvitai.MES.Tests.Warehouse.E2E\LKvitai.MES.Tests.Warehouse.E2E.csproj
```

## Run tests

```bash
dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/LKvitai.MES.Tests.Warehouse.E2E.csproj
```

Failure artifacts are saved under:

`tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/bin/<Configuration>/net8.0/playwright-artifacts/`
