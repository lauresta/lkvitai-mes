# High Availability

## Scope
PRD-1653 failover testing coverage for:
- Database primary/standby promotion
- API instance failover behind load balancer
- Recovery time and data integrity checks

## HA Test Assets
- Integration tests: `src/tests/LKvitai.MES.Tests.Integration/FailoverTests.cs`
- Failover script: `scripts/failover/promote-standby.sh`
- Runbook: `docs/operations/failover-runbook.md`

## Validation Commands
```bash
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --filter "FullyQualifiedName~FailoverTests"
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln
```

## Runtime Failover Drill (Staging)
```bash
docker-compose stop postgres-primary
docker exec postgres-standby pg_ctl promote
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items

docker-compose stop api-1
sleep 35
curl -H "Authorization: Bearer $TOKEN" http://localhost/api/warehouse/v1/items
```

Expected outcomes:
- API returns `200` after DB promotion completes.
- Load balancer routes around failed API node without request loss.
- Post-failover integrity checks confirm no corruption or FK violations.
