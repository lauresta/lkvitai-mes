# Chaos Engineering

## Scope
PRD-1652 adds staging-focused chaos validation for:
- Database failure
- Redis failure
- Network partition
- High latency injection

## Implementation
- Package added: `Polly.Contrib.Simmy` in `src/LKvitai.MES.Api/LKvitai.MES.Api.csproj`
- Service added: `IChaosResilienceService` / `ChaosResilienceService`
  - Exponential retry (`3` attempts)
  - Circuit breaker (opens after repeated failures)
  - Fault injection by scenario
  - Latency injection (default `500ms`)
  - Graceful degradation path (Redis failure -> fallback)
  - Transaction rollback hook for zero-data-loss checks

## Automated Tests
`src/tests/LKvitai.MES.Tests.Integration/ChaosTests.cs` includes:
- `DatabaseFailure_Returns503_AndOpensCircuitAfterThreeFailures`
- `RedisFailure_UsesFallback_AndAvoids500`
- `NetworkPartition_Returns503_Not500`
- `HighLatency_AddsAtLeastConfiguredDelay_AndReturnsSuccess`
- `DatabaseFailure_TransactionalExecution_RollsBackAndPreservesConsistency`

## Validation Commands
```bash
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --filter "FullyQualifiedName~ChaosTests"
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln
```

Manual docker kill/restore scenarios from the sprint spec still require a live compose stack and auth tokens.
