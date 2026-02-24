# Warehouse Dev/Test EF Migrations

This document defines deterministic commands to apply Warehouse EF Core migrations.

## Prerequisites

- Run commands from repository root.
- .NET SDK and `dotnet-ef` tool are installed.
- Connection string is configured for the selected environment.

## Development

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet ef database update `
  --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/LKvitai.MES.Modules.Warehouse.Infrastructure.csproj `
  --startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/LKvitai.MES.Modules.Warehouse.Api.csproj
```

## Test

```powershell
$env:ASPNETCORE_ENVIRONMENT="Test"
dotnet ef database update `
  --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/LKvitai.MES.Modules.Warehouse.Infrastructure.csproj `
  --startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/LKvitai.MES.Modules.Warehouse.Api.csproj
```

## Verify

```powershell
dotnet ef migrations list `
  --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/LKvitai.MES.Modules.Warehouse.Infrastructure.csproj `
  --startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/LKvitai.MES.Modules.Warehouse.Api.csproj
```

