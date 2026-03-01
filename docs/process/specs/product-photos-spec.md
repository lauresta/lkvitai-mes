# Product Photos + Image Search Spec

## Scope
- Warehouse module supports multiple item photos.
- Each item can have one primary photo used for list thumbnails.
- All image delivery uses same-origin API proxy endpoints.
- Similarity search endpoint accepts an image and returns top 20 item matches.

## Data Model
- `item_photos` table fields:
  - `Id` (uuid)
  - `ItemId` (FK -> `items.Id`)
  - `OriginalKey`
  - `ThumbKey`
  - `ContentType`
  - `SizeBytes`
  - `CreatedAt`
  - `IsPrimary`
  - `Tags` (optional)
  - `ImageEmbedding` (`vector(512)`, nullable)
- Constraint: unique primary photo per item with filtered unique index (`IsPrimary = true`).

## API
- Photo management:
  - `POST /api/warehouse/v1/items/{id}/photos`
  - `GET /api/warehouse/v1/items/{id}/photos`
  - `GET /api/warehouse/v1/items/{id}/photos/{photoId}?size=thumb|original`
  - `POST /api/warehouse/v1/items/{id}/photos/{photoId}/make-primary`
  - `DELETE /api/warehouse/v1/items/{id}/photos/{photoId}`
- Similarity search:
  - `POST /api/warehouse/v1/items/search-by-image`
  - Returns 503 with clear reason if model file is missing or pgvector is unavailable.

## Security and Storage
- No direct MinIO URLs in UI.
- API proxies image bytes and sets cache headers + ETag.
- Buckets are ops-managed; application only validates access and never creates buckets.
- No secrets are committed in source control.

## UI
- `/admin/items`: thumbnail column (36x36), thumbnail/SKU opens detail page.
- `/admin/items/{id}`: read-only item fields, photo gallery, upload/delete/make-primary, view original.
- `/available-stock`: thumbnail column + Table/List/Gallery modes with shared paging.
- `/warehouse/inbound/shipments/{id}`: inbound line item thumbnails.
- `/search-by-image`: mobile-first upload with camera capture hint (`capture="environment"`), result gallery, auto-open item when score >= `0.85`.
