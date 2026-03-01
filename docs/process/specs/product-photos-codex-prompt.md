# Codex Implementation Prompt: Product Photos & Image Search

**Feature**: Multi-photo support with CLIP-based visual similarity search for LKvitai.MES Warehouse

**Spec reference**: `docs/process/specs/product-photos-spec.md`

---

## Instructions for Implementing Agent

This is a self-contained implementation plan. Do not re-explore the codebase — implement in the commit order below. All file paths are relative to repo root. Respect all architecture constraints in the spec.

**CRITICAL RULES**:
- UI must NEVER use direct MinIO URLs. All images are served via API proxy endpoints.
- No secrets in repo. All sensitive values via environment variables.
- Follow Central Package Management (CPM): no `Version=` on `<PackageReference>`.
- Respect layer boundaries: Domain has zero NuGet deps; Application defines ports; Infrastructure implements ports.
- Keep changes minimal. Do NOT refactor unrelated UI or code.

---

## STOP CONDITIONS

**STOP and ask for guidance if**:
1. pgvector extension cannot be created (permissions issue) — implement graceful 503, do NOT crash on startup
2. MinIO credentials are missing or invalid — implement graceful 503 on upload, do NOT crash on startup
3. MinIO bucket does not exist or is not accessible — implement graceful 503, do NOT attempt to create buckets (buckets are managed ops-side)
4. ONNX model file is missing — implement graceful 503 on search endpoint, do NOT crash on startup, do NOT download models from the internet
5. Auth/authorization middleware is unclear — verify role names (`OperatorOrAbove`, `ManagerOrAdmin`)
6. Existing DTO structure conflicts with spec — ask before breaking changes
7. Architecture tests fail after changes — do NOT proceed until resolved

**CRITICAL**: App must boot successfully even when model, pgvector, or MinIO bucket are unavailable. Feature endpoints return 503 with clear messages when dependencies are missing.

---

## Environment Configuration

**NO SECRETS IN REPO**. All sensitive values are provided via environment variables.

### GitHub Environments

Create three environments: `dev`, `test`, `prod`.

Use the **SAME variable/secret names** in each environment; values differ per env.

**Secrets (per env)**:
- `ITEMIMAGES__ACCESSKEY`
- `ITEMIMAGES__SECRETKEY`

**Variables (per env)**:
- `ITEMIMAGES__ENDPOINT` (e.g., `minio-dev.internal:9000`)
- `ITEMIMAGES__BUCKET` (e.g., `lkvitai-dev`, `lkvitai-test`, `lkvitai-prod`)
- `ITEMIMAGES__USESSL` (`true` or `false`)
- `ITEMIMAGES__MAXUPLOADMB` (`5`)
- `ITEMIMAGES__CACHEMAXAGESECONDS` (`86400`)
- `ITEMIMAGES__MODEL_PATH` (path inside API container, e.g., `/app/models/clip-vit-b32.onnx`)
- `ITEMIMAGES__EMBEDDING_DIM` (`512`)

**Note**: Object key prefix (`item-photos`) is hardcoded in `ItemImageOptions.ObjectKeyPrefix` and NOT configurable via environment variables. Separate buckets per environment eliminate the need for configurable prefixes.

### .NET Configuration Wiring

Add to `Api/appsettings.json` (empty values, overridden by env vars):

```json
"ItemImages": {
  "Endpoint": "",
  "BucketName": "",
  "AccessKey": "",
  "SecretKey": "",
  "UseSsl": false,
  "ModelPath": "",
  "MaxUploadMb": 5,
  "CacheMaxAgeSeconds": 86400,
  "EmbeddingDim": 512
}
```

**Note**: `Prefix` is NOT in config — it's hardcoded as `ItemImageOptions.ObjectKeyPrefix = "item-photos"`.

### Deployment Note

GitHub Actions workflows should pass env vars from the GitHub Environment to docker compose for the API container. Example:

```yaml
- name: Deploy to ${{ inputs.environment }}
  env:
    ITEMIMAGES__ENDPOINT: ${{ vars.ITEMIMAGES__ENDPOINT }}
    ITEMIMAGES__BUCKET: ${{ vars.ITEMIMAGES__BUCKET }}
    ITEMIMAGES__ACCESSKEY: ${{ secrets.ITEMIMAGES__ACCESSKEY }}
    ITEMIMAGES__SECRETKEY: ${{ secrets.ITEMIMAGES__SECRETKEY }}
    ITEMIMAGES__USESSL: ${{ vars.ITEMIMAGES__USESSL }}
    ITEMIMAGES__MODEL_PATH: ${{ vars.ITEMIMAGES__MODEL_PATH }}
  run: |
    docker compose -f docker-compose.prod.yml up -d api
```

---

## Commit Plan

### Commit 1 — `feat: add ItemPhoto entity and pgvector migration`

**Files to create/modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/Entities/MasterDataEntities.cs`
  - Add `ItemPhoto` class (see spec §3.1)
  - Add `Photos` navigation property to `Item` class
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/Persistence/WarehouseDbContext.cs`
  - Add `HasPostgresExtension("vector")` in `OnModelCreating`
  - Add `ItemPhoto` entity configuration (see spec §3.3)
  - Add `UseVector()` to `.UseNpgsql()` call

**Scaffold migration**:
```bash
dotnet ef migrations add AddItemPhotos \
  --project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure \
  --startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api
```

**Edit migration `Up()`**:
- Prepend: `migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");`
- Append: `CREATE UNIQUE INDEX ix_item_photos_one_primary ON "ItemPhotos" ("ItemId") WHERE "IsPrimary" = TRUE`
- Append: `CREATE INDEX ON "ItemPhotos" USING ivfflat ("ImageEmbedding" vector_cosine_ops) WITH (lists = 50)` (handle gracefully if table is empty)

**Verify**:
- Build succeeds
- Migration file exists

---

### Commit 2 — `feat: add item image storage and embedding infrastructure`

**Files to create/modify**:
- `Directory.Packages.props`
  - Add 5 packages: `AWSSDK.S3`, `SixLabors.ImageSharp`, `Microsoft.ML.OnnxRuntime`, `Pgvector`, `Pgvector.EntityFrameworkCore` (see spec §4.1)
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/LKvitai.MES.Modules.Warehouse.Infrastructure.csproj`
  - Add 5 `<PackageReference>` (no `Version=`)
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/ItemImages/ItemImageOptions.cs` (NEW)
  - See spec §4.3
  - **CRITICAL**: `Prefix` is NOT a config property — use hardcoded constant `ObjectKeyPrefix = "item-photos"`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Ports/IItemPhotoStore.cs` (NEW)
  - See spec §4.2
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Ports/IImageEmbeddingService.cs` (NEW)
  - See spec §4.2
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/ItemImages/MinioItemPhotoStore.cs` (NEW)
  - See spec §4.3
  - **CRITICAL**: Do NOT auto-create buckets. Validate bucket access only. If bucket not accessible → throw exception with clear message (caller handles 503)
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/ItemImages/ClipEmbeddingService.cs` (NEW)
  - See spec §4.3
  - **CRITICAL**: Do NOT throw on startup if model missing. Run in "Disabled" mode. Throw `InvalidOperationException` in `ComputeAsync` if disabled (caller handles 503)

**Verify**:
- Build succeeds
- No architecture test failures

---

### Commit 3 — `feat: register services and add startup health check`

**Files to modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Program.cs`
  - Add `UseVector()` to `.UseNpgsql()` call (if not already done in Commit 1)
  - Register `IItemPhotoStore` and `IImageEmbeddingService` as singletons (see spec §4.4)
  - Add pgvector health check (see spec §7.3) — **WARNING only, do NOT fail startup**
  - Add ONNX model file check (warning if missing, not fatal)
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/appsettings.json`
  - Add empty `ItemImages` section (see spec §5.2)
  - **CRITICAL**: Do NOT include `Prefix` in config

**Verify**:
- Build succeeds
- App starts successfully even when model file is missing or pgvector is not installed (warnings logged, acceptable)

---

### Commit 4 — `feat: add ItemPhotosController with upload/list/proxy/primary/delete`

**Files to create/modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Controllers/ItemPhotosController.cs` (NEW)
  - Route: `[Route("api/warehouse/v1/items/{itemId:int}/photos")]`
  - 5 endpoints: POST (upload), GET (list), GET (proxy stream), POST (make-primary), DELETE (see spec §6.1)
  - Add `ItemPhotoDto` record (see spec §6.2)
  - **CRITICAL**: Upload endpoint must validate MinIO bucket access and return 503 if not accessible

**Verify**:
- Build succeeds
- Endpoints are accessible (401/403 if auth required, acceptable)
- Upload endpoint returns 503 if MinIO bucket not accessible (acceptable)

---

### Commit 5 — `feat: update ItemsController with PrimaryThumbnailUrl and search-by-image`

**Files to modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Controllers/ItemsController.cs`
  - Update `ItemListItemDto` to include `PrimaryThumbnailUrl` (see spec §6.3)
  - Update `GetAsync` (list) to LEFT JOIN primary photo and construct proxy URL (see spec §6.3)
  - Update `GetByIdAsync` to return `ItemDetailDto` with photos list (see spec §6.3)
  - Add `POST /api/warehouse/v1/items/search-by-image` endpoint (see spec §6.4)
  - Add `ItemSimilarityResultDto` record (see spec §6.4)
  - **CRITICAL**: Search endpoint must check if `IImageEmbeddingService` is enabled and pgvector is available; return 503 with clear message if not

**Files to find and modify** (available stock controller):
- Find controller serving `/stock/available` or similar route
- Add batch join for `PrimaryThumbnailUrl` (see spec §6.5)
- Update `AvailableStockItemDto` to include `PrimaryThumbnailUrl`

**Files to find and modify** (inbound shipment controller):
- Find controller serving `/api/warehouse/v1/inbound/shipments/{id}` or similar
- Add batch join for `PrimaryThumbnailUrl` in line DTOs
- Update `InboundShipmentLineDto` (or equivalent) to include `PrimaryThumbnailUrl`

**Verify**:
- Build succeeds
- List endpoints return `PrimaryThumbnailUrl` (null if no photos, acceptable)
- Search-by-image endpoint returns 503 if model missing or pgvector not installed (acceptable)

---

### Commit 6 — `feat: WebUI DTOs, ItemPhotoClient, and shared components`

**Files to create/modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Models/MasterDataDtos.cs`
  - Add `PrimaryThumbnailUrl` to `AdminItemDto`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Models/AvailableStockItemDto.cs`
  - Add `PrimaryThumbnailUrl`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Models/ItemPhotoDtos.cs` (NEW)
  - Add `ItemPhotoDto` and `ItemSimilarityResultDto` (see spec §8.1)
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Services/ItemPhotoClient.cs` (NEW)
  - See spec §8.2
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Services/MasterDataAdminClient.cs`
  - Add `GetItemByIdAsync` method (see spec §8.2)
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Program.cs`
  - Register `ItemPhotoClient` as scoped
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Components/Stock/ViewModeToggle.razor` (NEW)
  - See spec §8.3
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Components/Stock/ItemThumbnail.razor` (NEW)
  - See spec §8.3
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/wwwroot/images/items/placeholder.svg` (NEW)
  - See spec §8.3

**Verify**:
- Build succeeds
- Components render without errors (may show placeholders, acceptable)

---

### Commit 7 — `feat: admin items list and detail pages with photo management`

**Files to modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/AdminItems.razor`
  - Add `<ViewModeToggle>` button group
  - Add thumbnail column to Table mode
  - Add List and Gallery render modes (see spec §8.4)
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/Admin/ItemDetail.razor` (NEW)
  - Route: `@page "/admin/items/{Id:int}"`
  - Two-column layout: photo gallery + item metadata (see spec §8.5)

**Verify**:
- Build succeeds
- `/admin/items` shows thumbnails (or placeholders)
- `/admin/items/{id}` shows photo gallery and upload UI

---

### Commit 8 — `feat: available-stock view modes`

**Files to modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/AvailableStock.razor`
  - Add `<ViewModeToggle>` to toolbar
  - Add thumbnail column to Table mode
  - Add List and Gallery render modes (see spec §8.6)

**Verify**:
- Build succeeds
- `/available-stock` shows thumbnails in all view modes

---

### Commit 9 — `feat: inbound shipment line items with thumbnails`

**Files to modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/InboundShipmentDetail.razor`
  - Add thumbnail column as first column in line items table (see spec §8.7)

**Verify**:
- Build succeeds
- `/warehouse/inbound/shipments/{id}` shows thumbnails per line item

---

### Commit 10 — `feat: mobile search-by-image page`

**Files to create/modify**:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/Items/SearchByImage.razor` (NEW)
  - Route: `@page "/search-by-image"`
  - Camera capture → embedding search → auto-redirect or gallery (see spec §8.8)
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Components/NavMenu.razor`
  - Add "Search by Image" nav entry under Stock section (see spec §8.8)

**Verify**:
- Build succeeds
- `/search-by-image` page renders with camera button
- Clicking camera button opens file picker (or camera on mobile)

---

## After All Commits

### Build & Test Verification

Run the following commands to verify the implementation:

```bash
# Build solution
dotnet build src/LKvitai.MES.sln -c Release

# Run architecture tests (layer rules)
dotnet test tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/ -c Release

# Run unit tests (no regressions)
dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/ -c Release
```

**STOP if any of the above fail**. Do NOT proceed until resolved.

### Manual Verification (requires running instance)

**Prerequisites**:
1. PostgreSQL with pgvector extension enabled: `CREATE EXTENSION IF NOT EXISTS vector;`
2. MinIO instance running with credentials configured via env vars
3. ONNX model file at path specified by `ITEMIMAGES__MODEL_PATH` (optional for non-search features)

**Verify AC-01 through AC-12** (see spec §10):
- AC-01: Thumbnails in available-stock grid
- AC-02: Placeholder when no photo
- AC-03: View toggle — available-stock
- AC-04: Admin item list
- AC-05: Item detail — multiple photos
- AC-06: Image proxy caching
- AC-07: Upload validation
- AC-08: Search by image — mobile flow
- AC-09: Search by image — no items with embeddings
- AC-10: Search by image — missing dependencies (503 with clear message)
- AC-11: Inbound shipment line items — thumbnails
- AC-12: Build & architecture checks (already verified above)

---

## Implementation Notes

### Magic Bytes Validation

**JPEG**: `FF D8 FF`
**PNG**: `89 50 4E 47`
**WebP**: `52 49 46 46` (first 4 bytes) + `57 45 42 50` (bytes 8-11)

Example validation:

```csharp
private static bool IsValidImageMagicBytes(byte[] bytes, string contentType)
{
    if (bytes.Length < 12) return false;

    return contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
        "image/webp" => bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                        bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
        _ => false
    };
}
```

### CLIP Preprocessing

**Resize**: 224×224 (center crop or pad)
**Normalize**: mean `[0.4815, 0.4578, 0.4082]`, std `[0.2686, 0.2613, 0.2758]`
**Channel order**: RGB (not BGR)
**Output**: L2-normalized 512-dim float array

**CRITICAL**: If model file is missing or unreadable, `ClipEmbeddingService` must run in "Disabled" mode and throw `InvalidOperationException` in `ComputeAsync` (not in constructor). Do NOT crash on startup.

Example preprocessing (pseudo-code):

```csharp
// Load image with ImageSharp
using var image = await Image.LoadAsync<Rgb24>(stream);

// Resize to 224x224
image.Mutate(x => x.Resize(new ResizeOptions
{
    Size = new Size(224, 224),
    Mode = ResizeMode.Crop
}));

// Convert to float array and normalize
var pixels = new float[1 * 3 * 224 * 224];
var mean = new[] { 0.4815f, 0.4578f, 0.4082f };
var std = new[] { 0.2686f, 0.2613f, 0.2758f };

// Fill pixels array with normalized values (CHW format: [1, 3, 224, 224])
// ...

// Run ONNX inference
var inputTensor = new DenseTensor<float>(pixels, new[] { 1, 3, 224, 224 });
var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("pixel_values", inputTensor) };
using var results = _session.Run(inputs);
var output = results.First().AsEnumerable<float>().ToArray();

// L2 normalize
var norm = Math.Sqrt(output.Sum(x => x * x));
return output.Select(x => x / (float)norm).ToArray();
```

### ETag Generation

Use `{photoId}-{size}` as ETag value. Example:

```csharp
var etag = $"\"{photoId}-{size}\"";
Response.Headers.ETag = etag;

if (Request.Headers.IfNoneMatch == etag)
{
    return StatusCode(304); // Not Modified
}
```

### Thumbnail Generation

Use `SixLabors.ImageSharp` with `ResizeMode.Pad` to maintain aspect ratio:

**CRITICAL**: If MinIO bucket is not accessible, throw exception with clear message (caller handles 503). Do NOT attempt to create buckets.

```csharp
using var image = await Image.LoadAsync(stream);
image.Mutate(x => x.Resize(new ResizeOptions
{
    Size = new Size(200, 200),
    Mode = ResizeMode.Pad,
    Sampler = KnownResamplers.Lanczos3
}));

using var thumbStream = new MemoryStream();
await image.SaveAsWebpAsync(thumbStream);
thumbStream.Position = 0;
// Upload to MinIO (validate bucket access first)
```

### Primary Photo Auto-Promotion on Delete

When deleting a primary photo, auto-promote the next photo (by `CreatedAt DESC`):

```csharp
if (photo.IsPrimary)
{
    var nextPhoto = await _dbContext.ItemPhotos
        .Where(p => p.ItemId == itemId && p.Id != photoId)
        .OrderByDescending(p => p.CreatedAt)
        .FirstOrDefaultAsync(cancellationToken);

    if (nextPhoto is not null)
    {
        nextPhoto.IsPrimary = true;
    }
}
```

---

## Scope Control

**DO NOT**:
- Refactor unrelated UI components or pages
- Change existing API endpoints beyond adding `PrimaryThumbnailUrl` field
- Add features not in spec (e.g., bulk upload, photo editing, OCR)
- Modify authentication/authorization logic (use existing roles)
- Change database schema beyond `ItemPhotos` table and `Items.Photos` navigation
- Download ONNX models from the internet (ops provides models)
- Auto-create MinIO buckets (buckets are managed ops-side)
- Crash on startup when dependencies are missing (graceful degradation with 503)

**DO**:
- Keep changes minimal and focused on spec requirements
- Follow existing code patterns and conventions
- Respect layer boundaries and architecture constraints
- Add clear error messages for missing dependencies (503 with reason)
- Use existing UI components and styles where possible

---

## Final Checklist

Before marking implementation complete, verify:

- [ ] All 10 commits are complete
- [ ] `dotnet build` succeeds with no warnings-as-errors
- [ ] Architecture tests pass
- [ ] Unit tests pass (no regressions)
- [ ] No secrets in repo (all via env vars)
- [ ] UI never uses direct MinIO URLs (all via API proxy)
- [ ] pgvector extension is documented as prerequisite
- [ ] ONNX model deployment is documented (ops provides, Codex does NOT download)
- [ ] Search endpoint returns 503 with clear message if dependencies missing
- [ ] Upload endpoint returns 503 with clear message if MinIO bucket not accessible
- [ ] App boots successfully even when model, pgvector, or MinIO bucket are unavailable
- [ ] No auto-creation of MinIO buckets (validation only)
- [ ] Object key prefix is hardcoded (not configurable via env vars)
- [ ] All acceptance criteria (AC-01 through AC-12) are testable

---

## References

- Spec: `docs/process/specs/product-photos-spec.md`
- CLIP model: HuggingFace `Xenova/clip-vit-base-patch32`
- pgvector: https://github.com/pgvector/pgvector
- ImageSharp: https://docs.sixlabors.com/
- ONNX Runtime: https://onnxruntime.ai/

