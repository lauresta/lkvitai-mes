# ZPL Template Engine (Sprint 7)

## Implemented
- Added `LabelTemplateEngine` in `src/LKvitai.MES.Api/Services/LabelTemplateEngine.cs`.
- Added template contracts:
  - `LabelTemplateType` (`LOCATION`, `HANDLING_UNIT`, `ITEM`)
  - `LabelTemplate`
  - `LabelData`
- Added default ZPL templates for:
  - Location (4x2) with Code 128 location barcode and aisle/rack/level/bin fields
  - Handling unit (4x6) with HU barcode, SKU, qty, lot, expiry
  - Item (2x1) with SKU barcode and description
- Added placeholder rendering with case-insensitive dictionary replacement.
- Added preview API contract:
  - `POST /api/warehouse/v1/labels/preview`
  - body: `{ "templateType": "...", "data": { ... } }`
  - response: `application/pdf`
- Added template discovery API:
  - `GET /api/warehouse/v1/labels/templates`

## Notes
- Legacy `GET /api/warehouse/v1/labels/preview` remains available for backward compatibility.
- Preview rendering uses local PDF generation from rendered ZPL content.
