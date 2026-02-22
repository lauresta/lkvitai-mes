# Maintenance Runbook

## Procedure 1: Log and Disk Maintenance
1. Check disk usage for logs and artifacts.
2. Rotate/compress logs according to retention policy.
3. Verify no service interruption during rotation.
4. Record before/after disk usage.

## Procedure 2: Backup Verification
1. Verify daily backup job status.
2. Validate backup artifact integrity checksums.
3. Execute periodic restore test in staging.
4. Record restore duration and pass/fail outcome.

## Procedure 3: Dependency and Security Patch Window
1. Review dependency vulnerabilities and OS patch advisories.
2. Apply patches in staging and run smoke tests.
3. Schedule production patch window with rollback plan.
4. Confirm post-patch monitoring baseline.

## Procedure 4: Quarterly DR and Runbook Drill
1. Execute DR drill scripts end-to-end in staging.
2. Measure RTO/RPO and compare to targets.
3. Capture gaps and update this runbook.
4. Obtain operations sign-off for drill completion.
