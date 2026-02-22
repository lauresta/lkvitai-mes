# Marten Usage Inventory (P2.S0.T1)

Generated: 2026-02-19
Source command:

```bash
grep -rn 'Marten\|IDocumentSession\|IDocumentStore\|IQuerySession' src/LKvitai.MES.Application/
```

## Summary

- Total files matched by inventory grep: 13
- Files with direct Marten type usage in C# (`using Marten;`, `IDocumentStore`, `IDocumentSession`, `IQuerySession`): 2
- Application project package reference includes `Marten`: 1 (`src/LKvitai.MES.Application/LKvitai.MES.Application.csproj`)

STOP condition check (`>10` files using Marten types directly): **NOT TRIGGERED**.

## Direct Marten Type Usage

1. `src/LKvitai.MES.Application/Queries/SearchReservationsQueryHandler.cs`
- `using Marten;`
- constructor dependency: `IDocumentStore`
- private field: `IDocumentStore`

2. `src/LKvitai.MES.Application/Queries/VerifyProjectionQuery.cs`
- `using Marten;`
- constructor dependency: `IDocumentStore`
- private field: `IDocumentStore`

## Package-level Marten Coupling

1. `src/LKvitai.MES.Application/LKvitai.MES.Application.csproj`
- `<PackageReference Include="Marten" />`

## Other Matches (Comment/Documentation Mentions)

The inventory grep also matched comment/documentation references to "Marten" in these files:

- `src/LKvitai.MES.Application/Behaviors/IdempotencyBehavior.cs`
- `src/LKvitai.MES.Application/Orchestration/IReceiveGoodsOrchestration.cs`
- `src/LKvitai.MES.Application/Commands/ReceiveGoodsCommandHandler.cs`
- `src/LKvitai.MES.Application/Commands/RecordStockMovementCommandHandler.cs`
- `src/LKvitai.MES.Application/Ports/IReservationRepository.cs`
- `src/LKvitai.MES.Application/Ports/IAvailableStockRepository.cs`
- `src/LKvitai.MES.Application/Ports/IBalanceGuardLock.cs`
- `src/LKvitai.MES.Application/Ports/IStockLedgerRepository.cs`
- `src/LKvitai.MES.Application/Ports/IProcessedCommandStore.cs`
- `src/LKvitai.MES.Application/Ports/IActiveHardLocksRepository.cs`
