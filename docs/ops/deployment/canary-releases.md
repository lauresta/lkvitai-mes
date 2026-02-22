# Canary Releases

## Artifacts
- `scripts/canary/deploy-canary.sh`
- `scripts/canary/progress-canary.sh`
- `scripts/canary/rollback-canary.sh`
- `scripts/canary/inject-errors.sh`
- `src/tests/LKvitai.MES.Tests.Integration/CanaryDeploymentTests.cs`

## Validation
```bash
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --filter "FullyQualifiedName~CanaryDeploymentTests"
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln
```

## Rollout Model
- Phase 1: 10%
- Phase 2: 50%
- Phase 3: 100%

Rollback policy:
- Immediate rollback on high synthetic error indicator or manual trigger.
