# Inter-Warehouse Transfer UI (Sprint 7)

## Routes
- `/warehouse/transfers`
- `/warehouse/transfers/create`
- `/warehouse/transfers/{id}/execute`

## Implemented
- `src/LKvitai.MES.WebUI/Pages/Transfers/Create.razor`
  - From/To warehouse dropdowns
  - transfer lines table (item, qty, from/to location)
  - add/remove line actions
  - validation: required fields, different warehouses, qty > 0
- `src/LKvitai.MES.WebUI/Pages/Transfers/List.razor`
  - transfer list with status filter
  - actions: View, Submit, Approve, Execute, Cancel
  - approval modal with optional reason input
  - submit/approve actions wired to backend APIs
- `src/LKvitai.MES.WebUI/Pages/Transfers/Execute.razor`
  - transfer details and lines
  - execute action with confirmation dialog
  - execute API call and refresh flow

## Client updates
- `src/LKvitai.MES.WebUI/Services/TransfersClient.cs`
  - added `SubmitTransferAsync`
- `src/LKvitai.MES.WebUI/Models/TransferDtos.cs`
  - added submit request DTO
  - added transfer `ExecutedBy`
  - added line `LotId`
