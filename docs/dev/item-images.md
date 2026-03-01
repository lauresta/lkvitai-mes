# Item Images: Dev and Ops Notes

## Configuration
`ItemImages` config maps from environment values:

- `ITEMIMAGES__ENDPOINT` -> `ItemImages:Endpoint`
- `ITEMIMAGES__BUCKET` -> `ItemImages:BucketName`
- `ITEMIMAGES__USESSL` -> `ItemImages:UseSsl`
- `ITEMIMAGES__ACCESSKEY` -> `ItemImages:AccessKey`
- `ITEMIMAGES__SECRETKEY` -> `ItemImages:SecretKey`
- `ITEMIMAGES__MAXUPLOADMB` -> `ItemImages:MaxUploadMb`
- `ITEMIMAGES__CACHEMAXAGESECONDS` -> `ItemImages:CacheMaxAgeSeconds`
- `ITEMIMAGES__MODEL_PATH` -> `ItemImages:ModelPath`

Hardcoded object key prefix: `item-photos`.

## Required Variables and Secrets
- Variables:
  - `ITEMIMAGES__ENDPOINT`
  - `ITEMIMAGES__BUCKET`
  - `ITEMIMAGES__USESSL`
  - `ITEMIMAGES__MAXUPLOADMB`
  - `ITEMIMAGES__CACHEMAXAGESECONDS`
  - `ITEMIMAGES__MODEL_PATH`
- Secrets:
  - `ITEMIMAGES__ACCESSKEY`
  - `ITEMIMAGES__SECRETKEY`

Do not commit values for secrets or real production endpoints.

## Buckets and Access
- Buckets are created/managed by ops.
- Application behavior:
  - Validates bucket access (`BucketExists`) before upload/proxy operations.
  - Does **not** create buckets.
  - Returns `503 Service Unavailable` if bucket is missing or access is denied.

## pgvector Prerequisite
- Similarity search requires PostgreSQL `pgvector`.
- Application checks extension availability at runtime with:
  - `SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector');`
- If missing, app still boots; search endpoint returns `503` with reason.
- Optional deployment step (if permissions allow):
  - `CREATE EXTENSION IF NOT EXISTS vector;`

## Model Mounting
- Application never downloads ONNX/model artifacts.
- Ops must mount the model file inside the API container and set `ITEMIMAGES__MODEL_PATH`.
- If `ModelPath` is unset or file not found, search endpoint returns `503` with clear message.

## Caching
- Proxy image endpoint sets:
  - `Cache-Control: public, max-age=<ItemImages:CacheMaxAgeSeconds>`
  - `ETag`
- Requests with `If-None-Match` return `304 Not Modified` when applicable.
