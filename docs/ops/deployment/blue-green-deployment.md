# Blue-Green Deployment

## Artifacts
- `docker-compose.blue-green.yml`
- `scripts/blue-green/deploy-green.sh`
- `scripts/blue-green/switch-to-green.sh`
- `scripts/blue-green/rollback-to-blue.sh`
- `src/tests/LKvitai.MES.Tests.Integration/BlueGreenDeploymentTests.cs`

## Validation
```bash
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --filter "FullyQualifiedName~BlueGreenDeploymentTests"
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln
```

## Flow
1. Deploy green with target tag.
2. Run smoke tests against green.
3. Switch traffic to green.
4. Keep blue hot for rollback window.
5. Roll back instantly to blue if errors breach threshold.
