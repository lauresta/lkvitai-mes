# Transaction Export Format Specification

`PRD-1631` export endpoint: `POST /api/warehouse/v1/admin/compliance/export-transactions`

## Supported Formats

- `CSV`
- `JSON`

## CSV Columns

1. `EventId`
2. `EventType`
3. `Timestamp`
4. `AggregateId`
5. `UserId`
6. `Payload`

## JSON Shape

Each export file contains an array of records:

```json
[
  {
    "eventId": "b7f8f6e5-4a1f-4dc6-bec8-8d6f2c6a8a17",
    "eventType": "stock_moved",
    "timestamp": "2026-02-12T12:00:00+00:00",
    "aggregateId": "stock-ledger-wh-1-A1-SKU-001",
    "userId": "admin",
    "payloadJson": "{\"quantity\":5}"
  }
]
```

## File Naming

- Single file: `transactions-<timestamp>-<nonce>.csv` or `.json`
- Split export (over size limit): `transactions-<timestamp>-<nonce>-part1.csv`, `...-part2.csv`

## Size & Split Rules

- Maximum file size target: `500MB` (`Compliance:TransactionExport:MaxFileSizeBytes`)
- If export exceeds limit, output is split into multiple files with `-partN` suffix.

## History Tracking

History endpoint: `GET /api/warehouse/v1/admin/compliance/exports`

Stored fields:

- `ExportId`
- `StartDate`
- `EndDate`
- `Format`
- `RowCount`
- `FilePaths`
- `Status`
- `ErrorMessage`
- `ExportedBy`
- `ExportedAt`

## Optional SFTP Upload

When `sftpUpload=true`, export files are uploaded to the configured remote destination.

If `deleteLocalAfterUpload=true`, local export files are removed after successful upload.
