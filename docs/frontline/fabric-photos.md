# Frontline Fabric Photos

Fabric photos are stored separately from Warehouse `item_photos`.

Frontline fabric codes such as `R85`, `R88`, and `R101` come from the legacy
fabric catalogue and do not imply a Warehouse `items.Id`. MES stores only a
photo mapping table plus object-storage keys; the legacy database remains the
source of truth for fabric name, stock, status, widths, suppliers, and incoming
meters.

## Storage

Objects live in the existing MinIO/object-storage bucket configured by the
`ItemImages` settings. The `ITEMIMAGES__...` variables are the repo's existing
shared media/object-storage configuration; fabric photos are separated by the
`fabric-photos/...` prefix and `fabric_photos` table, not by a separate bucket.

- original: `fabric-photos/{fabricCode}/{photoId}/original{extension}`
- thumbnail: `fabric-photos/{fabricCode}/{photoId}/thumb.webp`

Example:

```text
fabric-photos/R85/018f6f2e/original.JPG
fabric-photos/R85/018f6f2e/thumb.webp
```

## Database

The MES table is `public.fabric_photos`.

Important columns:

- `Id` (`uuid`) stable photo id
- `FabricCode` legacy fabric code, for example `R85`
- `OriginalObjectKey`, `ThumbObjectKey`
- `SourceImageUrl`, `SourcePageUrl`, `SourceImageFileName`
- `Sha256`, `ImageWidth`, `ImageHeight`, `FileSizeBytes`
- `IsPrimary`, `CreatedAt`, `UpdatedAt`

`fabric_photos` has no foreign key to Warehouse `items` and does not write to
`item_photos`.

## Import

The importer reads a prepared package:

```text
output/fabric_photos.csv
images/original/
images/thumb/
```

Required configuration:

```bash
export ConnectionStrings__WarehouseDb='Host=...;Port=5432;Database=...;Username=...;Password=...'
export ITEMIMAGES__ENDPOINT='minio.example.com:9000'
export ITEMIMAGES__BUCKET='...'
export ITEMIMAGES__ACCESSKEY='...'
export ITEMIMAGES__SECRETKEY='...'
export ITEMIMAGES__USESSL='false'
```

`ConnectionStrings__WarehouseDb` is the existing MES PostgreSQL state database
connection name in this repo. It does not mean `fabric_photos` belongs to the
Warehouse domain. Where supported, `ConnectionStrings__FabricPhotosDb` can be
used as a clearer alias/fallback.

Run:

```bash
dotnet run --project tools/FabricPhotoImporter -- --input /Users/bykovas/Desktop/fabrics/fabric-image-import
```

If the package is placed exactly at `~/Desktop/fabric-image-import`, pass that
path instead. The importer is idempotent: the stable photo id is derived from
`FabricCode + Sha256` when possible, and `FabricCode + Sha256` updates existing
rows instead of creating duplicate logical photos.

Prepared CSV packages may contain duplicate rows for the same fabric image.
Duplicate detection is by `FabricCode + Sha256`, and `IsPrimary` is monotonic:
once a logical photo is primary, later duplicate rows cannot downgrade it to
non-primary. If a non-primary duplicate is seen first and the primary duplicate
arrives later, the existing row is promoted to primary.

## API

Frontline enriches legacy fabric responses with MES photo URLs:

- `GET /api/frontline/fabric/{code}`
- `GET /api/frontline/fabric/low-stock`

Photo streams are served by:

- `GET /api/frontline/fabric/{fabricCode}/photo?size=thumb`
- `GET /api/frontline/fabric/{fabricCode}/photo?size=original`

List views use thumbnail URLs only. The stream endpoint uses object-storage
ETags and `Cache-Control: public, max-age={ITEMIMAGES__CACHEMAXAGESECONDS}`.
