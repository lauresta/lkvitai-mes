# TCP 9100 Printer Integration (Sprint 7)

## Implemented
- Added `LabelPrintingConfig` in `src/LKvitai.MES.Api/Services/LabelPrintingServices.cs`:
  - `PrinterIP`
  - `PrinterPort` (default `9100`)
  - `RetryCount` (default `3`)
  - `RetryDelayMs` (default `1000`)
  - `SocketTimeoutMs` (default `5000`)
- Added config binding in `src/LKvitai.MES.Api/Program.cs`:
  - `builder.Services.Configure<LabelPrintingConfig>(...)`
- Added transport abstraction and TCP implementation:
  - `ILabelPrinterTransport`
  - `TcpLabelPrinterTransport`
- Updated `TcpLabelPrinterClient` to:
  - open raw TCP socket to configured printer
  - retry failed send attempts using configured retry count/delay
  - enforce per-attempt socket timeout
- Updated print flow fallback behavior:
  - if printer stays unavailable after retries, `POST /api/warehouse/v1/labels/print` returns `500`
  - response includes fallback PDF URL for manual print

## Configuration
`src/LKvitai.MES.Api/appsettings.json` and `src/LKvitai.MES.Api/appsettings.Development.json` now include:

```json
"LabelPrinting": {
  "PrinterIP": "127.0.0.1",
  "PrinterPort": 9100,
  "RetryCount": 3,
  "RetryDelayMs": 1000,
  "SocketTimeoutMs": 5000
}
```
