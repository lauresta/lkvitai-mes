# Disaster Recovery Communication Template

## Incident Header
- Incident ID:
- Scenario:
- Start Time (UTC):
- Incident Commander:
- Communications Owner:

## Initial Notification
Subject: `[DR] Incident Declared - <Scenario> - <Timestamp UTC>`

Body:
- Incident declared for scenario: `<Scenario>`
- Current impact: `<services/users impacted>`
- DR runbook activated.
- Next update by: `<time UTC>`

## Status Update
Subject: `[DR] Status Update - <Timestamp UTC>`

Body:
- Current phase: `<restore | dns-switch | verification>`
- Completed actions:
- In-progress actions:
- Issues/risks:
- ETA to recovery:
- Next update by:

## Recovery Confirmation
Subject: `[DR] Recovery Confirmed - <Timestamp UTC>`

Body:
- Services restored on DR target.
- Actual RTO:
- Data recovery point:
- Remaining risks:
- Post-incident review scheduled:

## Post-Incident Actions
- Root cause summary:
- Gaps identified:
- Remediation owners:
- Due dates:
