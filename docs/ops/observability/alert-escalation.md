# Alert Escalation (PRD-1648)

## Overview
This implementation adds a PagerDuty-oriented escalation service and webhook endpoint for Alertmanager-style alert payloads.

Implemented components:
- Config sections in `appsettings*.json`:
  - `PagerDuty`
  - `AlertEscalation`
- Escalation service:
  - `src/LKvitai.MES.Api/Services/AlertEscalationService.cs`
- Webhook endpoint:
  - `POST /api/monitoring/v1/alerts/escalation`
  - Controller: `src/LKvitai.MES.Api/Api/Controllers/AlertEscalationController.cs`

## Behavior
- Routing:
  - `critical` -> PagerDuty trigger
  - `warning` -> email route label (non-PagerDuty in this implementation)
  - `info` -> slack route label (non-PagerDuty in this implementation)
- Deduplication:
  - Same `alertname + severity` is deduplicated within configured window (`DeduplicationWindowMinutes`, default 5)
- Resolution handling:
  - `status=resolved` or `endsAt <= now` sends PagerDuty `resolve` event
- Escalation metadata:
  - L1/L2/L3 timings and on-call rotation are attached as custom details
- Runbook linkage:
  - `RunbookBaseUrl` + alert name is added to payload metadata

## Validation
```bash
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln --no-build
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~AlertEscalationTests"
```

Runtime/manual checks (requires running API + external systems):
```bash
cat src/LKvitai.MES.Api/appsettings.json | grep PagerDuty
curl -X POST http://localhost:5000/api/monitoring/v1/alerts/escalation -H "Content-Type: application/json" -d @sample-alert.json
```

## Notes
- PagerDuty dispatch is skipped when `PagerDuty:ApiKey` is empty.
- On-call scheduling metadata is configuration-driven; actual paging rosters are managed in PagerDuty/Ops tooling.
