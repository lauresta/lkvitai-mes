# Product Photos & Image Search — Feature Specification
_LKvitai.MES Warehouse Module | Status: Ready for implementation_

---

## 1. Scope

### In Scope
- Multi-photo support per Item (one-to-many relationship)
- Primary photo designation with automatic fallback on deletion
- Image storage in MinIO with thumbnail generation (200×200 WebP)
- API proxy endpoints for all image access (no direct MinIO URLs in UI)
- CLIP-based visual similarity search with pgvector
- Mobile-first camera capture UI for image search
- Photo management UI in admin item detail page
- Thumbnail display in:
  - `/admin/items` grid (all view modes)
  - `/available-stock` (all view modes)
  - `/warehouse/inbound/shipments/{id}` line items table
- View mode toggles (Table/List/Gallery) for item and stock grids

### Out of Scope
- Bulk photo upload (one file per upload request)
- Photo editing/cropping in UI (upload as-is)
- OCR or text extraction from images
- Video or 3D model support
- Photo versioning or history
- User-uploaded tags (tags field exists but not exposed in initial UI)
- Multi-model embedding support (CLIP ViT-B/32 only)
- GPU acceleration (CPU-only ONNX Runtime)

---

## 2. Architecture Constraints

| Layer | Rule |
|-------|------|
| `Domain` | ZERO NuGet deps. Only `SharedKernel` + `Contracts` project refs. |
| `Application` | Defines ports (interfaces). No Marten, no S3, no ONNX. |
| `Infrastructure` | Implements ports. References Domain + Application. |
| `Api` | Composition root. References Infrastructure. |
| `WebUI` | Blazor Server. Only `MudBlazor` NuGet. Talks to Api via `HttpClient`. |
| CPM | No `Version=` on any `<PackageReference>`. Versions only in `Directory.Packages.props`. |

**CRITICAL IMAGE ACCESS RULE**: The UI **NEVER** constructs or uses direct MinIO URLs. All image access is through API proxy endpoints (`/api/warehouse/v1/items/{itemId}/photos/{photoId}?size=thumb|original`) that stream bytes from MinIO. MinIO credentials and endpoints are internal to the API layer only.

---

## 3. Data Model

### Design Decision: `IsPrimary` flag (no circular FK)

**Rejected alternative**: `Items.PrimaryPhotoId → ItemPhotos.Id` creates a circular FK (`Items ↔ ItemPhotos`) that complicates cascade deletes and migration ordering in EF Core/PostgreSQL.

**Chosen approach**: `IsPrimary bool` on `ItemPhotos` with unique partial index. Application layer enforces one primary per item. Primary thumbnail URL is resolved via LEFT JOIN on `IsPrimary = true` at query time — single join, no N+1.


### 3.1 New Entity: `ItemPhoto`

**Location**: `Domain/Entities/MasterDataEntities.cs`

```csharp
public sealed class ItemPhoto
{
    public int Id { get; set; }
    public int ItemId { get; set; }

    /// <summary>MinIO object key for the original file, e.g. "item-photos/42/abc123-original.webp"</summary>
    public string OriginalKey { get; set; } = string.Empty;

    /// <summary>MinIO object key for the 200×200 WebP thumbnail.</summary>
    public string ThumbKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsPrimary { get; set; }

    /// <summary>Optional comma-separated tags for text search (not exposed in initial UI).</summary>
    public string? Tags { get; set; }

    /// <summary>
    /// CLIP ViT-B/32 visual embedding (512 floats, L2-normalised).
    /// Stored as PostgreSQL vector(512) via pgvector.
    /// Kept as float[] in Domain to avoid NuGet dependency; Infrastructure maps via value converter.
    /// </summary>
    public float[]? ImageEmbedding { get; set; }

    public Item? Item { get; set; }
}
```

### 3.2 Update `Item` Entity

**Location**: `Domain/Entities/MasterDataEntities.cs`

Add navigation property only (no new scalar fields):

```csharp
// Add to existing Item class:
public ICollection<ItemPhoto> Photos { get; set; } = new List<ItemPhoto>();
```

No `PrimaryPhotoId` column on `Items`. Primary is resolved dynamically via `Photos.FirstOrDefault(p => p.IsPrimary)`.

### 3.3 EF Core Configuration

**Location**: `Infrastructure/Persistence/WarehouseDbContext.cs`

```csharp
// In OnModelCreating:
modelBuilder.HasPostgresExtension("vector");

modelBuilder.Entity<ItemPhoto>(e =>
{
    e.ToTable("ItemPhotos");
    e.HasKey(p => p.Id);
    e.Property(p => p.Id).UseIdentityByDefaultColumn();
    e.HasIndex(p => p.ItemId);
    e.HasIndex(p => new { p.ItemId, p.IsPrimary })
     .HasFilter("\"IsPrimary\" = TRUE")
     .IsUnique();  // enforces max 1 primary per item at DB level

    // float[] <-> vector(512) value converter
    e.Property(p => p.ImageEmbedding)
     .HasColumnType("vector(512)")
     .HasConversion(
         v => v == null ? null : new Pgvector.Vector(v),
         v => v == null ? null : v.ToArray());
});

modelBuilder.Entity<Item>()
    .HasMany(i => i.Photos)
    .WithOne(p => p.Item)
    .HasForeignKey(p => p.ItemId)
    .OnDelete(DeleteBehavior.Cascade);
```

### 3.4 Primary Photo Constraint Behavior

- **FK constraint**: `Items.PrimaryPhotoId` is NOT used. Instead, `ItemPhotos.IsPrimary` with unique partial index.
- **On delete primary photo**: SET `IsPrimary = false` for deleted photo. If other photos exist for the item, auto-promote the most recent (by `CreatedAt DESC`) to primary. If no other photos exist, item has no primary photo (null thumbnail URL).
- **Deletion allowed**: Primary photo can be deleted; application handles fallback logic.


### 3.5 Migration: `AddItemPhotos`

**Migration `Up()` must**:
1. `CREATE EXTENSION IF NOT EXISTS vector;`
2. `CREATE TABLE "ItemPhotos" (...)` with all columns
3. `CREATE UNIQUE INDEX ix_item_photos_one_primary ON "ItemPhotos" ("ItemId") WHERE "IsPrimary" = TRUE`
4. `CREATE INDEX ON "ItemPhotos" USING ivfflat ("ImageEmbedding" vector_cosine_ops) WITH (lists = 50)` — add **after** data exists, or in a separate migration if table starts empty (IVFFlat requires at least `lists * 39` rows to build; for empty table this may fail, handle gracefully or defer to ops)

**Startup health check** (see §7) must verify `pgvector` extension is present before the app serves traffic.

---

## 4. Storage & Infrastructure

### 4.1 NuGet Packages

Add to `Directory.Packages.props`:
```xml
<PackageVersion Include="AWSSDK.S3" Version="3.7.400.4" />
<PackageVersion Include="SixLabors.ImageSharp" Version="3.1.7" />
<PackageVersion Include="Microsoft.ML.OnnxRuntime" Version="1.19.2" />
<PackageVersion Include="Pgvector" Version="0.2.1" />
<PackageVersion Include="Pgvector.EntityFrameworkCore" Version="0.2.1" />
```

Add `<PackageReference>` (no `Version=`) to `Infrastructure.csproj`:
```xml
<PackageReference Include="AWSSDK.S3" />
<PackageReference Include="SixLabors.ImageSharp" />
<PackageReference Include="Microsoft.ML.OnnxRuntime" />
<PackageReference Include="Pgvector" />
<PackageReference Include="Pgvector.EntityFrameworkCore" />
```

`Api.csproj`: no new packages needed (infrastructure handles all of the above).

### 4.2 Application Ports

**NEW `Application/Ports/IItemPhotoStore.cs`**

```csharp
namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

public interface IItemPhotoStore
{
    /// <summary>
    /// Saves original + generates 200×200 WebP thumbnail.
    /// Returns the MinIO object keys for both.
    /// </summary>
    Task<(string OriginalKey, string ThumbKey)> SaveAsync(
        int itemId, Stream stream, string contentType,
        CancellationToken ct = default);

    /// <summary>Streams bytes + content-type + ETag. Returns null if key not found.</summary>
    Task<(Stream Bytes, string ContentType, string ETag)?> GetAsync(
        string objectKey, CancellationToken ct = default);

    Task DeleteAsync(string originalKey, string thumbKey, CancellationToken ct = default);
}
```

**NEW `Application/Ports/IImageEmbeddingService.cs`**

```csharp
namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

/// <summary>
/// Computes a CLIP ViT-B/32 visual embedding for an image stream.
/// Returns a unit-normalized 512-dim float array.
/// </summary>
public interface IImageEmbeddingService
{
    Task<float[]> ComputeAsync(Stream imageStream, CancellationToken ct = default);
}
```


### 4.3 Infrastructure Implementations

**`Infrastructure/ItemImages/ItemImageOptions.cs`**

```csharp
public sealed class ItemImageOptions
{
    public const string SectionName = "ItemImages";
    public string Endpoint { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public string ModelPath { get; set; } = string.Empty;
    public int ThumbnailSizePx { get; set; } = 200;
    public int MaxUploadMb { get; set; } = 5;
    public int CacheMaxAgeSeconds { get; set; } = 86400;
    public int EmbeddingDim { get; set; } = 512;
    
    // Hardcoded prefix for object keys (not configurable via env vars)
    public const string ObjectKeyPrefix = "item-photos";
}
```

**`Infrastructure/ItemImages/MinioItemPhotoStore.cs`**

- Constructor: `IOptions<ItemImageOptions>`, `ILogger<MinioItemPhotoStore>`
- Builds `AmazonS3Client` with `ForcePathStyle = true`, `AuthenticationRegion = "us-east-1"`
- Object key format: `item-photos/{itemId}/{guid}-original.webp` and `item-photos/{itemId}/{guid}-thumb.webp` (prefix is hardcoded constant)
- `SaveAsync`:
  1. Validate bucket access (exists + permissions) on first call; if not ok → throw exception with clear message
  2. Load with `SixLabors.ImageSharp.Image.LoadAsync()`
  3. Save original as WebP → upload to MinIO with `CacheControl = "public, max-age={CacheMaxAgeSeconds}"`
  4. Resize to `ThumbnailSizePx × ThumbnailSizePx`, `ResizeMode.Pad`, save as WebP → upload with same cache headers
  5. Return both keys
- `GetAsync`:
  1. `GetObjectAsync()` from MinIO
  2. Return `(ResponseStream, Headers.ContentType, ETag)` — ETag comes from MinIO response
  3. On `AmazonS3Exception` 404 → return `null`
- `DeleteAsync`: `DeleteObjectAsync` for both keys; swallow 404 (already deleted)
- **DO NOT auto-create buckets**: Buckets are managed ops-side (already created per environment). Validate access only.

**`Infrastructure/ItemImages/ClipEmbeddingService.cs`**

- Singleton service that runs in "Disabled" mode if model file is missing or unreadable
- Constructor checks `ItemImageOptions.ModelPath`:
  - If empty, null, or file not found → log warning and set `IsEnabled = false`
  - If file exists → load `InferenceSession` and set `IsEnabled = true`
- **DO NOT throw on startup** — allow app to boot even when disabled
- `ComputeAsync` method:
  - If `IsEnabled = false` → throw `InvalidOperationException` with clear message (caller handles 503)
  - If enabled → run CLIP inference
- CLIP ViT-B/32 visual encoder ONNX. Model file must be provided by ops (see §7.2)
- Input node: `pixel_values` — shape `[1, 3, 224, 224]` float32
- CLIP preprocessing: resize to 224×224, normalize with mean `[0.4815, 0.4578, 0.4082]` std `[0.2686, 0.2613, 0.2758]`
- Output: first output tensor — shape `[1, 512]` float32; apply L2 normalisation
- Verify exact node names from `_session.InputMetadata` and `_session.OutputMetadata` on first run

### 4.4 DI Registration

**Location**: `Api/Program.cs`

```csharp
builder.Services.Configure<ItemImageOptions>(
    builder.Configuration.GetSection(ItemImageOptions.SectionName));
builder.Services.AddSingleton<IItemPhotoStore, MinioItemPhotoStore>();
builder.Services.AddSingleton<IImageEmbeddingService, ClipEmbeddingService>();
```

---

## 5. Environment Configuration

**NO SECRETS IN REPO**. All sensitive values are provided via environment variables or GitHub Environments.

### 5.1 GitHub Environments

Create three environments: `dev`, `test`, `prod`.

Use the **SAME variable/secret names** in each environment; values differ per env.

**Secrets (per env)**:
- `ITEMIMAGES__ACCESSKEY`
- `ITEMIMAGES__SECRETKEY`

**Variables (per env)**:
- `ITEMIMAGES__ENDPOINT` (e.g., `minio-dev.internal:9000`, `minio-prod.internal:9000`)
- `ITEMIMAGES__BUCKET` (e.g., `lkvitai-dev`, `lkvitai-test`, `lkvitai-prod`)
- `ITEMIMAGES__USESSL` (`true` or `false`)
- `ITEMIMAGES__MAXUPLOADMB` (`5`)
- `ITEMIMAGES__CACHEMAXAGESECONDS` (`86400`)
- `ITEMIMAGES__MODEL_PATH` (path inside API container, e.g., `/app/models/clip-vit-b32.onnx`)
- `ITEMIMAGES__EMBEDDING_DIM` (`512`)

**Note**: Object key prefix (`item-photos`) is hardcoded in `ItemImageOptions.ObjectKeyPrefix` and NOT configurable via environment variables. Separate buckets per environment eliminate the need for configurable prefixes.


### 5.2 .NET Configuration Wiring

**Location**: `Api/appsettings.json` — add section (empty values, overridden by env vars)

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

**Environment variable mapping** (12-factor style):

| Config key | Env var |
|------------|---------|
| `ItemImages:Endpoint` | `ITEMIMAGES__ENDPOINT` |
| `ItemImages:BucketName` | `ITEMIMAGES__BUCKET` |
| `ItemImages:AccessKey` | `ITEMIMAGES__ACCESSKEY` |
| `ItemImages:SecretKey` | `ITEMIMAGES__SECRETKEY` |
| `ItemImages:UseSsl` | `ITEMIMAGES__USESSL` |
| `ItemImages:ModelPath` | `ITEMIMAGES__MODEL_PATH` |
| `ItemImages:MaxUploadMb` | `ITEMIMAGES__MAXUPLOADMB` |
| `ItemImages:CacheMaxAgeSeconds` | `ITEMIMAGES__CACHEMAXAGESECONDS` |
| `ItemImages:EmbeddingDim` | `ITEMIMAGES__EMBEDDING_DIM` |

**Note**: No `ITEMIMAGES__PREFIX` env var. Prefix is hardcoded as `"item-photos"` in code.

### 5.3 Deployment Note

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

## 6. API Endpoints

### 6.1 New Controller: `ItemPhotosController`

**Route**: `[Route("api/warehouse/v1/items/{itemId:int}/photos")]`

**Authorization**: All endpoints require `OperatorOrAbove` unless noted.

#### POST `/api/warehouse/v1/items/{itemId}/photos` — Upload Photo

**Authorization**: `ManagerOrAdmin`

**Request**: `multipart/form-data`, field `file` (IFormFile), optional field `tags` (string)

**Response**: `201 Created` → `ItemPhotoDto`

**Validation**:
- Content-Type whitelist: `image/jpeg`, `image/png`, `image/webp`
- Max size: **5 MB** (`[RequestSizeLimit(5 * 1024 * 1024)]`)
- Magic bytes check (first 4 bytes): JPEG `FF D8 FF`, PNG `89 50 4E 47`, WebP `52 49 46 46...57 45 42 50`
- Item must exist (404 if not)

**Behavior**:
1. Validate item exists in `WarehouseDbContext.Items`
2. Validate MinIO bucket access (exists + permissions); if not ok → return `503 Service Unavailable` with message: `"Image storage unavailable: bucket {BucketName} not accessible"`
3. Call `IItemPhotoStore.SaveAsync()` → get `(OriginalKey, ThumbKey)`
4. Call `IImageEmbeddingService.ComputeAsync()` on stream → get `float[512]` (if service is enabled; if disabled, skip embedding)
5. Persist `ItemPhoto` with `IsPrimary = !await db.ItemPhotos.AnyAsync(p => p.ItemId == itemId)` (first photo auto-becomes primary)
6. Invalidate Redis cache key `item:{itemId}` (if caching is implemented)
7. Return `ItemPhotoDto`

#### GET `/api/warehouse/v1/items/{itemId}/photos` — List Photos

**Authorization**: `OperatorOrAbove`

**Response**: `200 OK` → `IReadOnlyList<ItemPhotoDto>` ordered by `CreatedAt DESC`


#### GET `/api/warehouse/v1/items/{itemId}/photos/{photoId}?size=thumb|original` — Proxy Stream

**Authorization**: `OperatorOrAbove`

**Response**: `200 OK` → image stream (image/webp)

**Headers**:
- `Cache-Control: public, max-age=86400`
- `ETag: "{photoId}-{size}"`
- `Vary: Accept-Encoding`

**ETag check**: if `Request.Headers.IfNoneMatch` matches → return `304 Not Modified` (no body).

**Select object key**:
- `size=thumb` (default) → `photo.ThumbKey`
- `size=original` → `photo.OriginalKey`

Call `IItemPhotoStore.GetAsync(key)` → stream response.

#### POST `/api/warehouse/v1/items/{itemId}/photos/{photoId}/make-primary` — Set Primary

**Authorization**: `ManagerOrAdmin`

**Response**: `204 No Content`

**Behavior**:
1. Verify photo belongs to `itemId` (404 if not)
2. `UPDATE ItemPhotos SET IsPrimary = FALSE WHERE ItemId = @itemId`
3. `UPDATE ItemPhotos SET IsPrimary = TRUE WHERE Id = @photoId`
4. Invalidate `item:{itemId}` cache
5. Return `204 No Content`

#### DELETE `/api/warehouse/v1/items/{itemId}/photos/{photoId}` — Delete Photo

**Authorization**: `ManagerOrAdmin`

**Response**: `204 No Content`

**Behavior**:
1. Load photo, verify belongs to `itemId` (404 if not)
2. If `IsPrimary`, auto-promote next photo (by `CreatedAt DESC`) to primary (if any exist)
3. `IItemPhotoStore.DeleteAsync(originalKey, thumbKey)`
4. Delete from DB
5. Invalidate cache
6. Return `204 No Content`

### 6.2 DTO: `ItemPhotoDto`

```csharp
public sealed record ItemPhotoDto(
    int Id,
    int ItemId,
    string ThumbnailUrl,    // proxy route: /api/warehouse/v1/items/{itemId}/photos/{id}?size=thumb
    string OriginalUrl,     // proxy route: /api/warehouse/v1/items/{itemId}/photos/{id}?size=original
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    bool IsPrimary,
    string? Tags);
```

### 6.3 Updates to `ItemsController`

#### Updated `ItemListItemDto` — Add `PrimaryThumbnailUrl`

```csharp
public sealed record ItemListItemDto(
    int Id,
    string InternalSKU,
    string Name,
    int CategoryId,
    string CategoryName,
    string BaseUoM,
    string Status,
    bool RequiresLotTracking,
    bool RequiresQC,
    string? PrimaryBarcode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? PrimaryThumbnailUrl);   // ← new; null if no photos
```

**URL format**: `/api/warehouse/v1/items/{Id}/photos/{primaryPhotoId}?size=thumb`

Constructed server-side from the LEFT JOIN result.


#### Updated `GetAsync` (list) — LEFT JOIN to Primary Photo

**Avoid N+1**: thumbnail URLs (proxy routes) must be present in list DTOs via single batch join.

```csharp
var items = await query
    .Select(i => new
    {
        Item = i,
        PrimaryPhotoId = i.Photos
            .Where(p => p.IsPrimary)
            .Select(p => (int?)p.Id)
            .FirstOrDefault()
    })
    .OrderBy(x => x.Item.InternalSKU)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);

var dtos = items.Select(x => new ItemListItemDto(
    x.Item.Id, x.Item.InternalSKU, x.Item.Name, x.Item.CategoryId,
    x.Item.Category?.Name ?? string.Empty,
    x.Item.BaseUoM, x.Item.Status,
    x.Item.RequiresLotTracking, x.Item.RequiresQC,
    x.Item.PrimaryBarcode, x.Item.CreatedAt, x.Item.UpdatedAt,
    x.PrimaryPhotoId.HasValue
        ? $"/api/warehouse/v1/items/{x.Item.Id}/photos/{x.PrimaryPhotoId}?size=thumb"
        : null)).ToList();
```

Include `Photos` in the query: `_dbContext.Items.Include(i => i.Photos.Where(p => p.IsPrimary))` (filtered include).

#### Updated `GetByIdAsync` — Include Photos List

Return a richer `ItemDetailDto`:

```csharp
public sealed record ItemDetailDto(
    int Id,
    string InternalSKU,
    string Name,
    int CategoryId,
    string CategoryName,
    string BaseUoM,
    string Status,
    bool RequiresLotTracking,
    bool RequiresQC,
    string? PrimaryBarcode,
    string? ProductConfigId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? PrimaryThumbnailUrl,
    IReadOnlyList<ItemPhotoDto> Photos);
```

### 6.4 NEW Endpoint: `POST /api/warehouse/v1/items/search-by-image` — Image Similarity Search

**Authorization**: `OperatorOrAbove`

**Request**: `multipart/form-data`, field `file` (IFormFile, ≤ 5 MB)

**Response**: `200 OK` → `IReadOnlyList<ItemSimilarityResultDto>`

**Response on missing dependencies**: `503 Service Unavailable` with clear message:
- If `ModelPath` missing, empty, or file not found: `"Image search unavailable: ONNX model not configured or file not found"`
- If `pgvector` extension missing: `"Image search unavailable: pgvector extension not installed in database"`
- If no items have embeddings (empty result): return `200 OK` with empty array (not 503)

**NO SILENT FAILURES**: Endpoint must return explicit 503 with reason if dependencies are missing.

**Logic**:
1. Check if `IImageEmbeddingService` is enabled; if not → return `503` with message
2. Check if `pgvector` extension is available; if not → return `503` with message
3. Compute `float[512]` embedding from uploaded image via `IImageEmbeddingService`
4. Run similarity search with raw SQL (EF Core `FromSqlRaw`):

```sql
SELECT
    p."ItemId",
    MIN(p."ImageEmbedding" <=> {0}::vector) AS "Distance"
FROM "ItemPhotos" p
WHERE p."ImageEmbedding" IS NOT NULL
GROUP BY p."ItemId"
ORDER BY "Distance"
LIMIT 20
```

3. For each matched `ItemId`, fetch `InternalSKU`, `Name`, and `PrimaryPhotoId`
4. Return `ItemSimilarityResultDto` list (empty array if no matches, not 503)

```csharp
public sealed record ItemSimilarityResultDto(
    int ItemId,
    string InternalSKU,
    string Name,
    string? PrimaryThumbnailUrl,
    float Score);          // 1 - Distance, range [0,1]
```

**Rate-limiting note**: CLIP inference is CPU-bound (~50–500ms depending on hardware). Consider rate-limiting this endpoint to N requests/minute per user/IP via a simple middleware or Polly, especially if the host lacks a GPU.


### 6.5 Updates to Available Stock Endpoint

Find the controller serving the available stock search (route pattern `stock/available` or similar). After fetching the paged SKU list, do a **single batch join** — no per-row calls:

```csharp
var skus = rows.Select(r => r.SKU).Distinct().ToList();

var primaryThumbsBySku = await _dbContext.Items
    .AsNoTracking()
    .Where(i => skus.Contains(i.InternalSKU))
    .Select(i => new {
        i.InternalSKU,
        PrimaryPhotoId = i.Photos
            .Where(p => p.IsPrimary)
            .Select(p => (int?)p.Id)
            .FirstOrDefault(),
        ItemId = i.Id
    })
    .ToDictionaryAsync(x => x.InternalSKU, cancellationToken);

// When building each AvailableStockItemDto row:
var primaryPhotoId = primaryThumbsBySku.GetValueOrDefault(row.SKU)?.PrimaryPhotoId;
var itemId        = primaryThumbsBySku.GetValueOrDefault(row.SKU)?.ItemId;
var thumbUrl      = (primaryPhotoId.HasValue && itemId.HasValue)
    ? $"/api/warehouse/v1/items/{itemId}/photos/{primaryPhotoId}?size=thumb"
    : null;
```

---

## 7. Ops / DB Prerequisites

### 7.1 pgvector Extension Requirement

**pgvector is REQUIRED** for image similarity search.

**Ops prerequisite**: Postgres must have pgvector extension available (installed at OS/container level).

**During deploy of each environment** (dev/test/prod), run once:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

If permissions prevent creating extensions automatically, document it as a manual step in ops runbook.

**Startup health check** (see §7.3) reports:
- `pgvector available: yes/no`
- `embedding search enabled: yes/no` (model present + pgvector present)

### 7.2 ONNX Model Deployment

The CLIP ONNX model is **NOT committed to the repo** (too large). Deployment steps:

1. **Ops must provide** the CLIP ViT-B/32 visual encoder ONNX model file
2. Place at path set by `ITEMIMAGES__MODEL_PATH` (e.g., `/app/models/clip-vit-b32.onnx`)
3. Docker volume mount or server filesystem — set env var to point to the file
4. **Codex must NOT download models from the internet** — ops provides and mounts the model

**Reference**: CLIP ViT-B/32 visual encoder ONNX can be obtained from HuggingFace `Xenova/clip-vit-base-patch32` (file: `onnx/vision_model.onnx`), but this is for ops reference only.

**Graceful degradation**: If `ModelPath` is missing, empty, or file not found:
- `ClipEmbeddingService` runs in "Disabled" mode (does not throw on startup)
- App boots successfully
- `/items/search-by-image` endpoint returns `503 Service Unavailable` with message: `"Image search unavailable: ONNX model not configured or file not found at {ModelPath}"`

### 7.3 Startup Health Check

**Location**: `Api/Program.cs` (before `app.Run()`)

```csharp
// Verify pgvector is installed
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
var extExists = await db.Database.ExecuteSqlRawAsync(
    "SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'vector')");
if (!extExists)
{
    Log.Warning("PostgreSQL extension 'pgvector' is not installed. " +
                "Image search will be unavailable. Run: CREATE EXTENSION vector;");
    // DO NOT fail startup — allow app to boot
}

// Check ONNX model file (informational only, do not fail startup)
var imageOptions = scope.ServiceProvider.GetRequiredService<IOptions<ItemImageOptions>>().Value;
if (string.IsNullOrWhiteSpace(imageOptions.ModelPath) || !File.Exists(imageOptions.ModelPath))
{
    Log.Warning("ONNX model file not found at {ModelPath}. Image search will be unavailable.", 
                imageOptions.ModelPath ?? "(not configured)");
}

// Check MinIO bucket access (informational only, do not fail startup)
// Actual validation happens on first upload attempt
```

**Health check endpoint** (add to existing `/health` or create new):

```json
{
  "status": "healthy",
  "pgvector_available": true,
  "embedding_search_enabled": true,
  "minio_bucket_accessible": true
}
```

**CRITICAL**: App must boot successfully even when model, pgvector, or MinIO bucket are unavailable. Feature endpoints return 503 with clear messages when dependencies are missing.


### 7.4 Npgsql EF Core pgvector Wiring

**Location**: `WarehouseDbContext` or `Program.cs`

In the existing `.UseNpgsql()` call, add `UseVector()`:

```csharp
options.UseNpgsql(connStr, o => o.UseVector());
```

### 7.5 Dev Verification Steps

**Dev verification steps** (for local testing):

1. Enable pgvector in dev DB: `CREATE EXTENSION IF NOT EXISTS vector;`
2. Provide ONNX model at the configured `ItemImages:ModelPath`
3. Call `POST /api/warehouse/v1/items/search-by-image` with a sample image; expect `200` and a ranked list
4. If model missing → expect `503` with message `"ModelPath missing or file not found"`
5. If pgvector missing → expect `503` with message `"pgvector extension not installed"`

**NO SILENT FAILURES**: Always return explicit 503 with reason if dependencies are missing.

---

## 8. WebUI Changes

### 8.1 New / Updated DTOs

**Location**: `WebUI/Models/`

**`MasterDataDtos.cs`** — `AdminItemDto` add:
```csharp
public string? PrimaryThumbnailUrl { get; init; }
```

**`AvailableStockItemDto.cs`** add:
```csharp
public string? PrimaryThumbnailUrl { get; init; }
```

**NEW `ItemPhotoDtos.cs`**:
```csharp
public record ItemPhotoDto
{
    public int Id { get; init; }
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string OriginalUrl { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsPrimary { get; init; }
    public string? Tags { get; init; }
}

public record ItemSimilarityResultDto
{
    public int ItemId { get; init; }
    public string InternalSKU { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? PrimaryThumbnailUrl { get; init; }
    public float Score { get; init; }
}
```

### 8.2 New Service: `ItemPhotoClient`

**Location**: `WebUI/Services/ItemPhotoClient.cs`

```csharp
public sealed class ItemPhotoClient
{
    public Task<IReadOnlyList<ItemPhotoDto>> GetPhotosAsync(int itemId, ...)
        // GET /api/warehouse/v1/items/{itemId}/photos

    public Task<ItemPhotoDto> UploadPhotoAsync(int itemId, byte[] bytes, string contentType, string? tags, ...)
        // POST multipart

    public Task MakePrimaryAsync(int itemId, int photoId, ...)
        // POST .../make-primary → 204

    public Task DeletePhotoAsync(int itemId, int photoId, ...)
        // DELETE .../photos/{photoId} → 204

    public Task<IReadOnlyList<ItemSimilarityResultDto>> SearchByImageAsync(byte[] bytes, string contentType, ...)
        // POST /api/warehouse/v1/items/search-by-image
}
```

Register as `services.AddScoped<ItemPhotoClient>()` in `WebUI/Program.cs`.

**`MasterDataAdminClient.cs`** — add:
```csharp
public Task<AdminItemDto> GetItemByIdAsync(int id, ...)
    // GET /api/warehouse/v1/items/{id}
```


### 8.3 Shared UI Components

**Location**: `WebUI/Components/Stock/`

#### `ItemThumbnail.razor`

- Parameters: `Url` (string?), `Alt` (string), `Size` (int = 36)
- If `Url` not null: `<img src="@Url" width="@Size" height="@Size" style="object-fit:contain" loading="lazy" alt="@Alt" />`
- If null: grey placeholder SVG same dimensions

#### `ViewModeToggle.razor`

- Parameters: `Mode` (StockViewMode), `OnModeChanged` (EventCallback<StockViewMode>)
- Three-button group: Table / List / Gallery (icons: `oi-list` / `oi-menu` / `oi-grid-three-up`)
- `StockViewMode` enum: `Table, List, Gallery`
- **Client-side state only** (not query param, not persisted between sessions)

#### `wwwroot/images/items/placeholder.svg`

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200">
  <rect width="200" height="200" fill="#e9ecef"/>
  <text x="50%" y="52%" dominant-baseline="middle" text-anchor="middle"
        font-family="sans-serif" font-size="11" fill="#adb5bd">No image</text>
</svg>
```

### 8.4 `/admin/items` (AdminItems.razor) — Changes

- Add `<ViewModeToggle>` button group above table
- `StockViewMode _viewMode = StockViewMode.Table` in `@code`
- **Table mode**: add thumbnail `<th>` (48px) + first `<td>` per row with `<ItemThumbnail Url="@row.PrimaryThumbnailUrl" Size="36" />` wrapped in `<a href="/admin/items/@row.Id">`. SKU column becomes a link too.
- **List mode**: horizontal rows — `<ItemThumbnail Size="64">` + SKU, Name, Category, Status, Edit/Deactivate buttons
- **Gallery mode**: `row-cols-2 row-cols-md-4` grid, each card: `<ItemThumbnail Size="130">` + SKU + Name + Status badge
- Pagination (`<Pagination>`) sits below all three modes
- `colspan` on empty-state row: update to match new column count

### 8.5 `/admin/items/{id:int}` — NEW `Pages/Admin/ItemDetail.razor`

**Route**: `@page "/admin/items/{Id:int}"`

**Inject**: `ItemPhotoClient`, `MasterDataAdminClient`, `ToastService`, `NavigationManager`

**Layout**: two-column card.

**Left column — photo gallery**:
- `IReadOnlyList<ItemPhotoDto> _photos` loaded on init
- Display all photos in a responsive `row-cols-2 row-cols-md-3` grid
- Each photo card:
  - `<img src="@photo.ThumbnailUrl" loading="lazy">` (proxy URL — no direct MinIO)
  - Star icon if `IsPrimary`
  - Buttons: "Set as primary" (hidden if already primary), "Delete"
  - Link "View original" (`href="@photo.OriginalUrl"` `target="_blank"`)
- Upload zone at the bottom:
  - `<InputFile OnChange="OnUploadAsync" accept="image/jpeg,image/png,image/webp" />`
  - Optional text box for `Tags` (not exposed in initial UI, can be added later)
  - Max 5 MB enforced client-side before upload
  - `MaxAllowedFiles="1"` per upload (multiple calls to add more)
  - Show spinner during upload; show error via `ErrorBanner`

**Right column — item metadata** (read-only):
- `<dl class="row">` with: SKU, Name, Category, BaseUoM, Status, Barcode, LotTracking, QC required


### 8.6 `/available-stock` (AvailableStock.razor) — Changes

- Add `<ViewModeToggle>` to toolbar
- `StockViewMode _viewMode = StockViewMode.Table`
- **Table mode**: add `<TemplateColumn>` as first column with `<ItemThumbnail Url="@context.Item.PrimaryThumbnailUrl" Size="36" />`
- **List mode**: replace MudDataGrid with `@foreach` rows:
  - `<ItemThumbnail Size="64">` + SKU + Location + Avail qty + `<StaleBadge>`
- **Gallery mode**: `row-cols-2 row-cols-md-4` cards:
  - `<ItemThumbnail Size="130">` + SKU + Location + Avail qty
- **Paging**: MudDataGrid manages state in all modes; List and Gallery render `_items` (already paged)
- **API DTO is the same** for all view modes; UI just renders differently

### 8.7 `/warehouse/inbound/shipments/{id}` (InboundShipmentDetail.razor) — Changes

**MUST**: Add thumbnail column to line items table.

**Location**: `Pages/InboundShipmentDetail.razor`

**Changes**:
1. Add new `<th>` column header "Photo" (or icon) as first column in the line items table
2. Add new `<td>` as first cell in each row with `<ItemThumbnail Url="@line.PrimaryThumbnailUrl" Size="36" />`
3. Update `InboundShipmentLineDto` (or equivalent DTO) to include `PrimaryThumbnailUrl` field
4. Update API endpoint `GET /api/warehouse/v1/inbound/shipments/{id}` to include `PrimaryThumbnailUrl` in line DTOs via batch join (no N+1)

**SHOULD** (optional, lower priority):
- Add thumbnails to other inbound/receiving pages if line items are displayed (e.g., `/warehouse/inbound/shipments/create`, QC queue)

### 8.8 `/search-by-image` — NEW `Pages/Items/SearchByImage.razor`

**Route**: `@page "/search-by-image"`

**Inject**: `ItemPhotoClient`, `NavigationManager`

**UX flow (mobile-first)**:

1. **Initial state**: centred UI — large circular camera button
   - `<InputFile accept="image/*" ... />` styled as a button, `capture` attribute set to `"environment"` (rear camera on mobile, file picker on desktop)
2. **After file selection** → show `LoadingSpinner`, label "Identifying item…"
3. Call `ItemPhotoClient.SearchByImageAsync(bytes, contentType)` → `List<ItemSimilarityResultDto>`
4. **Smart redirect**:
   - If `results[0].Score >= 0.85` → `NavigationManager.NavigateTo($"/admin/items/{results[0].ItemId}")`
   - Otherwise → show gallery of results (cards with `<ItemThumbnail>` + SKU + Name + match % badge)
5. Badge colour: Score ≥ 0.85 → `bg-success`, ≥ 0.65 → `bg-warning`, else → `bg-secondary`
6. Tap on any result card → navigate to `/admin/items/{ItemId}`
7. "Search again" button → resets state

**Camera capture attribute**:

```razor
<InputFile OnChange="OnFileSelectedAsync"
           accept="image/*"
           AdditionalAttributes="@(new Dictionary<string, object>
               { ["capture"] = "environment" })"
           style="display:none"
           id="camera-input" />
<label for="camera-input" class="btn btn-primary btn-lg">
    <i class="oi oi-camera-slr"></i> Take Photo
</label>
```

**Auto-redirect threshold**:

```csharp
private const float AutoNavigateThreshold = 0.85f;
```

**NavMenu entry**:

Add to `Components/NavMenu.razor` under the **Stock** section:

```razor
<li class="nav-item">
    <NavLink class="nav-link" href="search-by-image" Match="NavLinkMatch.All">
        <span class="oi oi-camera-slr" aria-hidden="true"></span> Search by Image
    </NavLink>
</li>
```

---

## 9. Performance & Security

| Concern | Implementation |
|---------|----------------|
| No N+1 | `PrimaryThumbnailUrl` always included in list DTOs via single batch join |
| Lazy loading images | `loading="lazy"` on every `<img>` |
| Browser caching | `Cache-Control: public, max-age=86400` + `ETag` on all proxy responses; `304 Not Modified` on match |
| File type validation | Content-Type whitelist + magic bytes check (4 bytes) server-side |
| Max upload size | 5 MB enforced by `[RequestSizeLimit(5 * 1024 * 1024)]` and client-side pre-check |
| No direct MinIO URLs | All `<img src>` values are `/api/...` proxy routes |
| ONNX inference load | `InferenceSession` is singleton (loaded once); consider rate-limiting search-by-image endpoint |
| Rate limiting | Recommend rate-limiting `/items/search-by-image` to N requests/minute per user/IP (note only, not implemented) |


---

## 10. Acceptance Criteria

```gherkin
AC-01: Thumbnails in available-stock grid
  Given: an item has at least one photo with IsPrimary = true
  When:  user opens /available-stock and searches
  Then:  each matching row shows a 36x36 proxy-URL thumbnail in the first column
  And:   no additional HTTP requests are made per row (URL is in the row DTO)

AC-02: Placeholder when no photo
  Given: an item has zero photos
  When:  that item appears in any list or gallery
  Then:  a grey placeholder SVG is shown (no broken image icon)

AC-03: View toggle — available-stock
  Given: /available-stock has results
  When:  user clicks List mode
  Then:  items render as horizontal rows with 64x64 thumbnail, SKU, location, quantities
  When:  user clicks Gallery mode
  Then:  items render as cards with ~130px thumbnail, SKU, location, available qty

AC-04: Admin item list
  Given: /admin/items has items
  Then:  Table mode shows 36x36 thumbnails; SKU is a clickable link to /admin/items/{id}
  And:   List and Gallery modes show the same content in their respective layouts

AC-05: Item detail — multiple photos
  Given: admin opens /admin/items/{id}
  Then:  all uploaded photos are shown in a grid
  And:   the primary photo is marked with a star/indicator
  When:  admin clicks "Set as primary" on a non-primary photo
  Then:  that photo becomes primary; previous primary loses its mark
  When:  admin uploads a new photo
  Then:  it appears in the gallery; if it is the first photo it becomes primary automatically
  When:  admin deletes the primary photo and another exists
  Then:  the next photo (by CreatedAt DESC) auto-becomes primary

AC-06: Image proxy caching
  Given: a proxy endpoint is called twice for the same photo
  When:  second call includes If-None-Match with the ETag from first response
  Then:  server responds 304 Not Modified
  And:   all proxy responses carry Cache-Control: public, max-age=86400

AC-07: Upload validation
  Given: user tries to upload a 6 MB file
  Then:  server returns 400 with clear error message
  Given: user uploads a .txt file renamed to .jpg
  Then:  server rejects it (magic bytes check fails)

AC-08: Search by image — mobile flow
  Given: user opens /search-by-image on a mobile browser
  Then:  a large camera button is shown; tapping opens native camera
  When:  user takes a photo and submits
  Then:  a loading indicator shows ("Identifying item…")
  When:  the top result has Score >= 0.85
  Then:  the browser auto-navigates to /admin/items/{id} without showing gallery
  When:  top Score < 0.85
  Then:  a gallery of top results is shown with match % badges
  When:  user taps a result card
  Then:  navigates to /admin/items/{id}

AC-09: Search by image — no items with embeddings
  Given: no items have photos (no embeddings stored)
  When:  user submits a search image
  Then:  server returns 200 OK with empty array (not 503)
  And:   UI shows empty result state with message "No items with images found"

AC-10: Search by image — missing dependencies
  Given: ONNX model file is missing OR pgvector extension is not installed
  When:  user calls POST /api/warehouse/v1/items/search-by-image
  Then:  server returns 503 Service Unavailable with clear message indicating what is missing
  And:   NO SILENT FAILURES — always explicit 503 with reason
  And:   App must have booted successfully (no startup crash)

AC-11: Inbound shipment line items — thumbnails
  Given: user opens /warehouse/inbound/shipments/{id}
  Then:  each line item row shows a 36x36 thumbnail in the first column
  And:   thumbnail URLs are included in the line DTO (no N+1 calls)

AC-12: Build & architecture checks
  Then:  dotnet build src/LKvitai.MES.sln -c Release succeeds with no warnings-as-errors
  And:   dotnet test tests/ArchitectureTests/... passes (layer rules enforced)
  And:   dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/... has no regressions
```

---

## 11. Complete File Change List

| File | Action |
|------|--------|
| `Directory.Packages.props` | Add 5 new packages |
| `Infrastructure/LKvitai.MES.Modules.Warehouse.Infrastructure.csproj` | Add 5 `<PackageReference>` |
| `Domain/Entities/MasterDataEntities.cs` | Add `ItemPhoto` class; add `Photos` nav to `Item` |
| `Infrastructure/Persistence/WarehouseDbContext.cs` | Configure `ItemPhotos` table + pgvector extension + value converter |
| `Infrastructure/Persistence/Migrations/AddItemPhotos.cs` | NEW migration |
| `Application/Ports/IItemPhotoStore.cs` | NEW |
| `Application/Ports/IImageEmbeddingService.cs` | NEW |
| `Infrastructure/ItemImages/ItemImageOptions.cs` | NEW |
| `Infrastructure/ItemImages/MinioItemPhotoStore.cs` | NEW |
| `Infrastructure/ItemImages/ClipEmbeddingService.cs` | NEW |
| `Api/appsettings.json` | Add `ItemImages` section (empty values) |
| `Api/Program.cs` | Register services; add pgvector startup check; `UseVector()` |
| `Api/Controllers/ItemPhotosController.cs` | NEW — 5 endpoints |
| `Api/Controllers/ItemsController.cs` | Update list + detail DTOs; add `search-by-image` endpoint |
| Available stock controller (find by route) | Add batch join for `PrimaryThumbnailUrl` |
| Inbound shipment controller (find by route) | Add batch join for `PrimaryThumbnailUrl` in line DTOs |
| `WebUI/Models/MasterDataDtos.cs` | Add `PrimaryThumbnailUrl` to `AdminItemDto` |
| `WebUI/Models/AvailableStockItemDto.cs` | Add `PrimaryThumbnailUrl` |
| `WebUI/Models/ItemPhotoDtos.cs` | NEW |
| `WebUI/Services/ItemPhotoClient.cs` | NEW |
| `WebUI/Services/MasterDataAdminClient.cs` | Add `GetItemByIdAsync` |
| `WebUI/Program.cs` | Register `ItemPhotoClient` as scoped |
| `WebUI/Components/Stock/ViewModeToggle.razor` | NEW |
| `WebUI/Components/Stock/ItemThumbnail.razor` | NEW |
| `WebUI/Pages/AdminItems.razor` | View toggle + 3 render modes |
| `WebUI/Pages/Admin/ItemDetail.razor` | NEW |
| `WebUI/Pages/AvailableStock.razor` | View toggle + 3 render modes |
| `WebUI/Pages/InboundShipmentDetail.razor` | Add thumbnail column to line items table |
| `WebUI/Pages/Items/SearchByImage.razor` | NEW |
| `WebUI/Components/NavMenu.razor` | Add "Search by Image" entry |
| `WebUI/wwwroot/images/items/placeholder.svg` | NEW |

---

## 12. References

- Draft spec: `cozy-dazzling-flute.md`
- CLIP model: HuggingFace `Xenova/clip-vit-base-patch32`
- pgvector: https://github.com/pgvector/pgvector
- ImageSharp: https://docs.sixlabors.com/
- ONNX Runtime: https://onnxruntime.ai/

