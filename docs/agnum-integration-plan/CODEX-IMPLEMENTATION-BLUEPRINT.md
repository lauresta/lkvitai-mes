# Codex Implementation Blueprint — Agnum Import Foundation (Slice 1)

Created: 2026-05-18

## Scope

Slice 1 builds the read/import foundation:

- `IAgnumApiClient` with X-API-KEY auth
- Typed product and balance DTOs (defensive barcode handling)
- `AgnumApiClient` implementation in Infrastructure
- Five new entities + EF Core migration
- `AgnumNomenclatureImportService` (product import with conflict detection)
- `AgnumBalanceImportService` (virtual balance import)
- `AgnumImportController` with read/trigger endpoints (new controller, separate from the frozen `AgnumController`)
- Unit tests for import logic

**Out of Slice 1:** Blazor pages, distribution command, 3D search, old export refactor, document export,
MassTransit sagas. See `MVP-SCOPE.md`.

---

## Verified existing names

These names are confirmed in the repository. Use them exactly.

### Application layer — Commands

| Class | Namespace | File |
| --- | --- | --- |
| `RecordStockMovementCommand` | `LKvitai.MES.Modules.Warehouse.Application.Commands` | `Application/Commands/RecordStockMovementCommand.cs` |
| `ReceiveGoodsCommand` | same | `Application/Commands/ReceiveGoodsCommand.cs` |
| `AllocateReservationCommand` | same | `Application/Commands/AllocateReservationCommand.cs` |

### Application layer — Ports

| Interface | Namespace | File |
| --- | --- | --- |
| `IStockLedgerRepository` | `LKvitai.MES.Modules.Warehouse.Application.Ports` | `Application/Ports/IStockLedgerRepository.cs` |
| `IAvailableStockRepository` | same | `Application/Ports/IAvailableStockRepository.cs` |
| `IEventBus` | same | `Application/Ports/IEventBus.cs` |

### Infrastructure — Persistence

| Class | Namespace | File |
| --- | --- | --- |
| `WarehouseDbContext` | `LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence` | `Infrastructure/Persistence/WarehouseDbContext.cs` |

Existing `DbSet` properties relevant to Slice 1:

```csharp
public DbSet<Item> Items => Set<Item>();
public DbSet<ItemBarcode> ItemBarcodes => Set<ItemBarcode>();
public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
public DbSet<SupplierItemMapping> SupplierItemMappings => Set<SupplierItemMapping>();
```

### Integration — Agnum (frozen files)

| Class/Interface | File | Note |
| --- | --- | --- |
| `IAgnumExportService` | `Integration/Agnum/IAgnumExportService.cs` | **Frozen** — old export interface |
| `ExportMode` enum | same | **Frozen** |

### Domain — Entities (frozen Agnum entities)

In `Domain/Entities/MasterDataEntities.cs`:

- `AgnumExportConfig` — **frozen**
- `AgnumMapping` — **frozen**
- `AgnumExportHistory` — **frozen**

### Api — Frozen services and controller

| File | Location | Note |
| --- | --- | --- |
| `AgnumController.cs` | `Api/Api/Controllers/` | **Frozen** |
| `AgnumExportServices.cs` | `Api/Services/` | **Frozen** — contains `IAgnumExportOrchestrator`, `IAgnumSecretProtector` |
| `AgnumReconciliationServices.cs` | `Api/Services/` | **Frozen** |

### Packages already in `Directory.Packages.props`

No `Version=` attribute allowed in any csproj.

| Package | Available |
| --- | --- |
| `Polly` 8.2.1 | Yes |
| `Microsoft.EntityFrameworkCore` | Yes |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Yes |
| `Moq` | Yes (tests) |
| `FluentAssertions` | Yes (tests) |
| `xunit` | Yes (tests) |
| `Microsoft.EntityFrameworkCore.InMemory` | Yes (tests) |

### Infrastructure csproj current packages

`Infrastructure.csproj` currently has: `ClosedXML`, `EFCore.BulkExtensions`, `Marten`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `StackExchange.Redis`, `Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`.

**To add for Slice 1:** `<PackageReference Include="Polly" />` (no version — CPM).
Check whether `Microsoft.Extensions.Http` is needed; `IHttpClientFactory` is available through
`Microsoft.Extensions.DependencyInjection` which ASP.NET Core provides transitively via Api.
If the Infrastructure build fails due to missing `IHttpClientFactory`, add `<PackageReference Include="Microsoft.Extensions.Http" />`.

### Integration csproj current packages

`Integration.csproj` has no `PackageReference` entries — only two `ProjectReference` entries
(Contracts, SharedKernel). The new `AgnumApiClient` lives in **Infrastructure**, not Integration,
to keep Integration free of HTTP/Polly dependencies. See layer placement below.

---

## Layer placement for Slice 1

```
Integration/Agnum/
  IAgnumApiClient.cs          ← interface + AgnumProductDto + AgnumBalanceLineDto (layer 3)

Domain/Entities/
  AgnumImportEntities.cs      ← 5 new entities (layer 3, new file, NOT editing MasterDataEntities.cs)

Application/Ports/
  IAgnumNomenclatureImportService.cs   ← import service interface (layer 4)
  IAgnumBalanceImportService.cs        ← balance import service interface (layer 4)

Infrastructure/Agnum/           ← new directory
  AgnumApiClient.cs             ← IHttpClientFactory-based impl of IAgnumApiClient (layer 5)
  AgnumApiClientOptions.cs      ← config binding POCO
  AgnumNomenclatureImportService.cs   ← uses WarehouseDbContext + IAgnumApiClient (layer 5)
  AgnumBalanceImportService.cs        ← uses WarehouseDbContext + IAgnumApiClient (layer 5)

Infrastructure/Persistence/
  WarehouseDbContext.cs         ← EDIT: add 5 new DbSets + Fluent API for new tables
  Migrations/YYYYMMDD_AddAgnumImportTables.cs  ← new migration

Api/Controllers/
  AgnumImportController.cs     ← NEW controller (do not edit AgnumController.cs)
```

Layer rule check:
- `Integration` references only Contracts + SharedKernel. Adding only an interface + plain DTOs there is safe.
- `Application/Ports` is already the home for port interfaces. New service ports go there.
- `Infrastructure` references Application, Domain, Integration via the build graph. `AgnumApiClient` in Infrastructure can implement `IAgnumApiClient` from Integration.

---

## Files to create

### 1. `Integration/Agnum/IAgnumApiClient.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Integration.Agnum`

```csharp
public interface IAgnumApiClient
{
    // Returns all products for the sndid bound to this client's API key.
    // Does NOT use limit/offset — jar 1.39 pagination is buggy.
    Task<IReadOnlyList<AgnumProductDto>> GetProductsAsync(CancellationToken ct = default);
}
```

Also in the same file or same folder:

```csharp
public sealed class AgnumProductDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string Pcs { get; init; } = string.Empty;
    public decimal Balance { get; init; }       // PRK.KIEKIS — balance for this API key's sndid
    public decimal? Netto { get; init; }
    public decimal? Brutto { get; init; }
    // Barcode fields: API may return either; importer must accept both
    public string? Barcode { get; init; }
    [JsonPropertyName("barcodes")]
    public List<string>? Barcodes { get; init; }
    public DateTime? ModifyDate { get; init; }
    public DateTime? CreateDate { get; init; }
    public string? Group { get; init; }
    public string? Category { get; init; }
    public string? Subgroup { get; init; }
    public string? Direction { get; init; }
    public string? Branch { get; init; }
    public string? Place { get; init; }
    public string? F1 { get; init; }
    public string? F2 { get; init; }
    // F3-F20 add if needed later
}
```

No `Balance` endpoint separate call is needed for Slice 1 — balance is included in the product payload
via `balance` field. `GetProductsAsync` returns products including their `balance` field.

---

### 2. `Infrastructure/Agnum/AgnumApiClientOptions.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum`

```csharp
public sealed class AgnumApiClientOptions
{
    public string BaseUrl { get; set; } = "http://agnum-api:8181";
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed class AgnumWarehouseKeyOptions
{
    public int SndId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}
```

Config binding path: `Agnum:BaseUrl`, `Agnum:TimeoutSeconds`, `Agnum:Warehouses:{name}:SndId`,
`Agnum:Warehouses:{name}:ApiKey`.

---

### 3. `Infrastructure/Agnum/AgnumApiClient.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum`

Implements `IAgnumApiClient`.

- Constructor takes `IHttpClientFactory`, `AgnumWarehouseKeyOptions` (the specific key for this instance), `ILogger<AgnumApiClient>`.
- Uses named `HttpClient` registered as `"agnum-{name}"` in DI.
- Sets `X-API-KEY: {apiKey}` header on every request.
- Calls `GET /api/products/search` (no pagination params).
- Log the request at Debug level; redact the API key in all log messages.
- On HTTP error response: log warning and return empty list (do not throw; let the import service record the error in the run status).
- **Do not use Polly for Slice 1.** Add a TODO comment: `// TODO Slice2: add Polly retry for transient HTTP errors`.
- Deserialize with `System.Text.Json`. Handle both `barcode` (string) and `barcodes` (array) by using `JsonPropertyName` as shown in the DTO above. Verify during deserialization: if `barcode` is non-null and `barcodes` is null, populate a synthetic single-item list for downstream use.

---

### 4. `Domain/Entities/AgnumImportEntities.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Domain.Entities`

New file. Do not edit `MasterDataEntities.cs`.

```csharp
// Configuration: one row per Agnum warehouse (sndid) enabled for import
public sealed class AgnumWarehouseMapping : AuditableEntity  // AuditableEntity exists in MasterDataEntities.cs
{
    public int Id { get; set; }
    public int SndId { get; set; }
    public string AgnumName { get; set; } = string.Empty;
    public string MesVirtualWarehouseCode { get; set; } = string.Empty;
    public string ApiKeyConfigName { get; set; } = string.Empty;  // references Agnum:Warehouses:{name}
    public bool IsImportEnabled { get; set; }
}

// Link between MES Item and Agnum product; one row per (sndid, agnumProductId)
public sealed class AgnumProductLink : AuditableEntity
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int SndId { get; set; }
    public int AgnumProductId { get; set; }
    public string AgnumCode { get; set; } = string.Empty;
    public bool AgnumEnabled { get; set; }
    public DateTime? AgnumModifiedAt { get; set; }
    public DateTime? LastImportedAt { get; set; }
    public string? RawHash { get; set; }

    public Item? Item { get; set; }
}

// Flexible key/value store for Agnum product classifiers (group, category, subgroup, direction, branch, place, f1..f20)
public sealed class ItemExternalAttribute
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string SourceSystem { get; set; } = "AGNUM";
    public string SourceContext { get; set; } = string.Empty;  // sndid as string
    public string Key { get; set; } = string.Empty;            // "group", "category", "f1", etc.
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }

    public Item? Item { get; set; }
}

// Tracks one import run for a specific sndid
public sealed class AgnumBalanceImportRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SndId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";   // Running, Completed, Failed
    public int ProductCount { get; set; }
    public int BalanceCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorSummary { get; set; }
}

// One row per (importRunId, sndId, agnumProductId)
public sealed class AgnumVirtualWarehouseBalance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportRunId { get; set; }
    public int SndId { get; set; }
    public int AgnumProductId { get; set; }
    public int? ItemId { get; set; }           // null if product not yet linked to MES Item
    public string? Sku { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public string? SourceHash { get; set; }

    public AgnumBalanceImportRun? ImportRun { get; set; }
}
```

Table names (use in Fluent API):

| Entity | Table |
| --- | --- |
| `AgnumWarehouseMapping` | `agnum_warehouse_mappings` |
| `AgnumProductLink` | `agnum_product_links` |
| `ItemExternalAttribute` | `item_external_attributes` |
| `AgnumBalanceImportRun` | `agnum_balance_import_runs` |
| `AgnumVirtualWarehouseBalance` | `agnum_virtual_warehouse_balances` |

Unique constraints:

- `AgnumProductLink`: `(SndId, AgnumProductId)` unique; `(SndId, AgnumCode)` unique.
- `ItemExternalAttribute`: `(ItemId, SourceSystem, SourceContext, Key)` unique.
- `AgnumVirtualWarehouseBalance`: `(ImportRunId, SndId, AgnumProductId)` unique.

---

### 5. `Infrastructure/Persistence/WarehouseDbContext.cs` — EDIT

Add five new `DbSet` properties and Fluent API `OnModelCreating` blocks for the five new entities.
Follow the existing pattern in the file.

```csharp
// New DbSets
public DbSet<AgnumWarehouseMapping> AgnumWarehouseMappings => Set<AgnumWarehouseMapping>();
public DbSet<AgnumProductLink> AgnumProductLinks => Set<AgnumProductLink>();
public DbSet<ItemExternalAttribute> ItemExternalAttributes => Set<ItemExternalAttribute>();
public DbSet<AgnumBalanceImportRun> AgnumBalanceImportRuns => Set<AgnumBalanceImportRun>();
public DbSet<AgnumVirtualWarehouseBalance> AgnumVirtualWarehouseBalances => Set<AgnumVirtualWarehouseBalance>();
```

Add Fluent API blocks. Follow the pattern in the existing AgnumExportConfig block in the file.

---

### 6. Migration

Generate with:
```bash
dotnet ef migrations add AddAgnumImportTables \
  --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure \
  --startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api
```

Or write the migration manually following the existing pattern in `20260211070343_AddAgnumExportTables.cs`.

---

### 7. `Application/Ports/IAgnumNomenclatureImportService.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Application.Ports`

```csharp
public interface IAgnumNomenclatureImportService
{
    Task<AgnumImportPreview> PreviewAsync(int sndId, CancellationToken ct = default);
    Task<AgnumImportResult> ApplyAsync(int sndId, CancellationToken ct = default);
}

public sealed class AgnumImportPreview
{
    public int SndId { get; init; }
    public int TotalProducts { get; init; }
    public List<AgnumImportCandidate> ToCreate { get; init; } = new();
    public List<AgnumImportCandidate> ToUpdate { get; init; } = new();
    public List<AgnumImportConflict> Conflicts { get; init; } = new();
}

public sealed class AgnumImportCandidate
{
    public int AgnumProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Pcs { get; init; } = string.Empty;
}

public sealed class AgnumImportConflict
{
    public int AgnumProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;  // "UnknownUoM", "DuplicateSku", "LinkedToDifferentItem"
}

public sealed class AgnumImportResult
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public List<AgnumImportConflict> Conflicts { get; init; } = new();
}
```

---

### 8. `Application/Ports/IAgnumBalanceImportService.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Application.Ports`

```csharp
public interface IAgnumBalanceImportService
{
    Task<Guid> StartImportAsync(int sndId, CancellationToken ct = default);
    Task<AgnumBalanceImportRunStatus> GetRunStatusAsync(Guid runId, CancellationToken ct = default);
}

public sealed class AgnumBalanceImportRunStatus
{
    public Guid RunId { get; init; }
    public int SndId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ProductCount { get; init; }
    public int BalanceCount { get; init; }
    public int ErrorCount { get; init; }
    public string? ErrorSummary { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
```

---

### 9. `Infrastructure/Agnum/AgnumNomenclatureImportService.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum`

Implements `IAgnumNomenclatureImportService`.

Dependencies: `IAgnumApiClient` (resolved by sndId — see DI registration note below), `WarehouseDbContext`, `ILogger<AgnumNomenclatureImportService>`.

**DI note:** `IAgnumApiClient` must be resolved per sndId. Use a factory approach:
define `IAgnumApiClientFactory` with `IAgnumApiClient GetForSndId(int sndId)` in
`Integration/Agnum/` and inject the factory into import services. The factory resolves
the correct named `HttpClient`.

**Conflict detection logic:**

| Conflict type | Condition | Reason string |
| --- | --- | --- |
| `UnknownUoM` | `product.Pcs` not in `UnitOfMeasures` table | `"UnknownUoM"` |
| `DuplicateSku` | Another `Item` with same `InternalSKU` exists and no `AgnumProductLink` for this `(sndId, agnumProductId)` | `"DuplicateSku"` |
| `LinkedToDifferentItem` | `AgnumProductLink` for `(sndId, agnumProductId)` points to a different item | `"LinkedToDifferentItem"` |

**Apply logic:**

- For `ToCreate`: create `Item` (set `InternalSKU = product.Code`, `Name`, `BaseUoM`, `Status`, `Weight = product.Netto`), create `AgnumProductLink`, import barcodes to `ItemBarcode`, import category hierarchy to `ItemCategory` (create levels as needed), store `group/category/subgroup/direction/branch/f1/f2` as `ItemExternalAttribute`.
- For `ToUpdate`: update `Item` fields that differ, update `AgnumProductLink` timestamps and hash.
- Skip conflict rows; include them in `AgnumImportResult.Conflicts`.

---

### 10. `Infrastructure/Agnum/AgnumBalanceImportService.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum`

Implements `IAgnumBalanceImportService`.

Logic for `StartImportAsync`:

1. Create `AgnumBalanceImportRun` with `Status = "Running"`, `StartedAt = UtcNow`. Save.
2. Call `IAgnumApiClient.GetProductsAsync` (via factory for the given sndId).
3. For each product where `balance > 0` (or all, to also record zero balances): upsert `AgnumVirtualWarehouseBalance` matching latest `AgnumProductLink` for `(sndId, product.Id)` to get `ItemId` and `Sku`.
4. Update run with counts and `Status = "Completed"` (or `"Failed"` on exception). Save.
5. Return `run.Id`.

---

### 11. `Api/Controllers/AgnumImportController.cs`

Namespace: `LKvitai.MES.Modules.Warehouse.Api.Controllers`

**New controller — do not edit `AgnumController.cs`.**

```
POST /api/warehouse/v1/agnum/import/products?sndId={sndId}&apply=false
  → returns AgnumImportPreview (preview) or AgnumImportResult (apply)
  → apply=true triggers ApplyAsync

POST /api/warehouse/v1/agnum/import/balances?sndId={sndId}
  → triggers StartImportAsync, returns { runId: guid }

GET  /api/warehouse/v1/agnum/import/status/{runId}
  → returns AgnumBalanceImportRunStatus

GET  /api/warehouse/v1/agnum/virtual-warehouses
  → returns list of AgnumWarehouseMapping (sndId, name, virtualCode, isEnabled)

GET  /api/warehouse/v1/agnum/nomenclature?sndId={sndId}&search={text}&page={n}
  → returns paged list from AgnumProductLink joined to Item

GET  /api/warehouse/v1/agnum/balances?sndId={sndId}
  → returns latest AgnumVirtualWarehouseBalance rows for the most recent Completed run
```

No MediatR required for these endpoints in Slice 1. Direct service injection is fine.
These are read/import orchestration endpoints, not domain command dispatch.

---

### 12. DI registration

In `Infrastructure/DependencyInjection.cs`, add:

- Named `HttpClient` registrations: one per configured warehouse key.
- `AgnumApiClient` instantiation per named client.
- `IAgnumApiClientFactory` → `AgnumApiClientFactory` singleton.
- `IAgnumNomenclatureImportService` → `AgnumNomenclatureImportService` scoped.
- `IAgnumBalanceImportService` → `AgnumBalanceImportService` scoped.

Bind config: `services.Configure<AgnumApiClientOptions>(config.GetSection("Agnum:Api"))`.
Loop `config.GetSection("Agnum:Warehouses").GetChildren()` to register per-warehouse named HttpClients.
Config key for base URL: `Agnum:Api:BaseUrl` (default `http://agnum-api:8181`).
Register `IAgnumApiClientFactory` as **Singleton** (reads config once in constructor).
Register `IAgnumNomenclatureImportService` and `IAgnumBalanceImportService` as **Scoped**.

---

## Tests — Slice 1

Location: `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/`

### Test file 1: `AgnumImportConflictDetectionTests.cs`

Cover `AgnumNomenclatureImportService` conflict detection in isolation using `Moq` + `Microsoft.EntityFrameworkCore.InMemory`:

- Product with unknown `pcs` → conflict `UnknownUoM`
- Product whose code matches existing `Item.InternalSKU` but no link exists → conflict `DuplicateSku`
- Product with existing link pointing to different item → conflict `LinkedToDifferentItem`
- Product with valid `pcs` and no existing item → classified as `ToCreate`
- Product with valid `pcs` and existing linked item → classified as `ToUpdate`

### Test file 2: `AgnumProductDtoDeserializationTests.cs`

Cover defensive barcode deserialization:

- JSON with `"barcode": "12345"` (string) → single barcode available
- JSON with `"barcodes": ["12345", "67890"]` (array) → list available
- JSON with both fields → both parsed without error
- JSON with neither field → no barcode, no exception

### Test file 3: `AgnumBalanceImportServiceTests.cs`

Cover `AgnumBalanceImportService` with mocked `IAgnumApiClient` and in-memory DbContext:

- Happy path: 3 products with balances → run created, 3 balance rows inserted
- No linked item for a product → `ItemId = null` in balance row, run still `Completed`
- API client returns empty list → run `Completed` with zero counts

---

## Definition of done for Slice 1

- `dotnet build src/LKvitai.MES.sln -c Release` — zero errors
- `dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit` — all pass
- `dotnet test tests/ArchitectureTests/...` — all pass
- Migration generates cleanly; `dotnet ef database update` on a clean dev DB succeeds
- `GET /api/warehouse/v1/agnum/virtual-warehouses` returns configured sndid rows
- `POST /api/warehouse/v1/agnum/import/products?sndId=493&apply=false` returns a preview with `ToCreate`, `ToUpdate`, and `Conflicts` sections
- `POST /api/warehouse/v1/agnum/import/balances?sndId=493` returns a run ID and the run reaches `Completed`
- No Blazor pages, no distribution command, no Agnum document export

---

## Ready-to-copy Codex prompt for Slice 1

```
You are implementing Slice 1 of the Agnum import foundation for the Warehouse module.

Read these files before writing any code:
- docs/agnum-integration-plan/MVP-SCOPE.md  (scope gate, frozen files, defaults)
- docs/agnum-integration-plan/CODEX-IMPLEMENTATION-BLUEPRINT.md  (this file — exact names, placement, tests)
- docs/agnum-integration-plan/02-agnum-api-findings.md  (real Agnum API shape)
- docs/agnum-integration-plan/06-master-data-mapping.md  (field mapping and conflict rules)
- CLAUDE.md  (layer rules, forbidden patterns)

Scope: implement only what is listed in CODEX-IMPLEMENTATION-BLUEPRINT.md § "Files to create".

Layer rules (from CLAUDE.md):
- IAgnumApiClient + DTOs → Integration/Agnum/ (no new NuGet refs there; BCL only)
- New domain entities (AgnumImportEntities.cs) → Domain/Entities/ (no NuGet dependencies)
- Application port interfaces → Application/Ports/
- AgnumApiClient implementation, import services, DI → Infrastructure/Agnum/ (new directory)
- AgnumImportController → Api/Controllers/ (new file)
- Do NOT add Marten to Application. Do NOT add EF Core to Domain. Do NOT add Version= to any csproj.
- If Infrastructure needs Polly, add: <PackageReference Include="Polly" /> (no version attribute).

Frozen files — do not touch:
- Api/Api/Controllers/AgnumController.cs
- Api/Services/AgnumExportServices.cs
- Api/Services/AgnumReconciliationServices.cs
- Sagas/AgnumExportSaga.cs
- Integration/Agnum/IAgnumExportService.cs
- Any existing AgnumExportConfig / AgnumMapping / AgnumExportHistory entity or migration

Do not create:
- Blazor pages
- Distribution command or workflow
- 3D/2D product search
- MassTransit sagas
- Agnum document export
- Any POST or PUT to the Agnum API

Definition of done:
- dotnet build src/LKvitai.MES.sln -c Release → zero errors
- dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit → all pass
- Architecture tests pass
- GET /api/warehouse/v1/agnum/virtual-warehouses returns configured rows
- POST /api/warehouse/v1/agnum/import/products?sndId=493&apply=false returns a valid preview
- POST /api/warehouse/v1/agnum/import/balances?sndId=493 returns a run ID that reaches Completed status

Start with the data model and entities. Then the connector. Then the import services. Then the controller. Tests last.
If a name is not in the blueprint's verified tables, check the actual repo files before inventing it.
```
