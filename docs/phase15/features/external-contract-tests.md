# External Contract Tests

## Covered Contracts

- FedEx tracking API request/response contract (`FedExApiContractTests`).
- ERP-facing outbound/stock event payload compatibility (`ErpEventContractTests`).
- Agnum export contract and headers (`AgnumExportServicesTests`).

Run:

```bash
dotnet test src/LKvitai.MES.sln --filter Category=Contract
```

## Intent

- Prevent schema drift in outbound integrations.
- Validate required fields remain present across refactors.
