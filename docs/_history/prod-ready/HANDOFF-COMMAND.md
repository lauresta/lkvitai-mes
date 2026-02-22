# Handoff Command for Codex

**Date:** February 12, 2026
**Session:** Sprint 7-9 Spec Fix (Partial Completion)
**Status:** ⚠️ ONLY 5/60 TASKS READY FOR EXECUTION

---

## CRITICAL: DO NOT EXECUTE STUB TASKS

**Executable Tasks:** PRD-1601 to PRD-1605 ONLY (5 tasks)
**Stub Tasks:** PRD-1606 to PRD-1660 (55 tasks - NOT EXECUTABLE)

---

## What Happened

Attempted to fix S7-S9 specs by removing placeholders. Discovered ALL 60 tasks contain:
- "See description above" (110+ occurrences)
- "See implementation" (110+ occurrences)
- Generic Gherkin scenarios (no domain data)
- No concrete API contracts
- No specific data models

**Completed:** 5 tasks in Sprint 7 (Valuation epic) fully specified
**Remaining:** 55 tasks need complete rewrite

---

## Files Modified

1. `docs/prod-ready/prod-ready-tasks-PHASE15-S7.md` - PARTIAL (5/20 tasks complete)
2. `docs/prod-ready/prod-ready-tasks-progress-S789.md` - Updated with warning
3. `docs/prod-ready/SPRINT-789-STATUS.md` - Detailed analysis
4. `docs/prod-ready/HANDOFF-COMMAND.md` - THIS FILE

---

## Verification Commands

```bash
# Count remaining placeholders
grep -c "See description above" docs/prod-ready/prod-ready-tasks-PHASE15-S7.md
# Expected: 15 (tasks PRD-1606 to PRD-1620)

grep -c "See description above" docs/prod-ready/prod-ready-tasks-PHASE15-S8.md
# Expected: 20 (all tasks)

grep -c "See description above" docs/prod-ready/prod-ready-tasks-PHASE15-S9.md
# Expected: 20 (all tasks)

# Total: 55 stub tasks remaining
```

---

## Option 1: Implement Only Complete Tasks (SAFE)

```bash
# Implement ONLY PRD-1601 to PRD-1605 (Valuation epic)
echo "Implement PRD-1601 to PRD-1605 reading S7 spec (lines 1-600 only)"
echo "Log issues to docs/prod-ready/codex-suspicions.md"
echo "Final summary to docs/prod-ready/codex-run-summary.md"
```

**Expected Outcome:** 5 tasks implemented successfully

---

## Option 2: Complete Remaining Specs First (RECOMMENDED)

```bash
# DO NOT START IMPLEMENTATION
# Instead: Complete remaining 55 task specifications

echo "Next session: Rewrite PRD-1606 to PRD-1660 specifications"
echo "Read universe.md for each epic"
echo "Expand all stubs with concrete requirements, APIs, Gherkin, validation"
echo "Estimated time: 8-12 hours"
```

**Expected Outcome:** All 60 tasks ready for Codex execution

---

## Option 3: Codex Execution on Stubs (NOT RECOMMENDED)

```bash
# WARNING: This will fail
echo "Implement PRD-1601 to PRD-1660 reading S7/S8/S9 specs"
```

**Expected Outcome:** 
- PRD-1601 to PRD-1605: SUCCESS
- PRD-1606 to PRD-1660: FAILURE (missing concrete requirements)

---

## Recommended Next Action

**STOP IMPLEMENTATION. COMPLETE SPECS FIRST.**

1. Read `docs/prod-ready/prod-ready-universe.md` sections for:
   - Agnum Integration (§4.Epic D)
   - 3D Visualization (§4.Epic E)
   - Cycle Counting (§4.Epic M)
   - Label Printing (§4.Epic F)
   - Inter-Warehouse Transfers (§4.Epic G)
   - Admin Configuration (§4.Epic P)
   - Security Hardening (§5.Security)
   - Compliance (§5.Compliance)
   - Data Retention/GDPR (§5.DataRetention)
   - Performance (§5.Performance)
   - Monitoring (§5.Observability)
   - Testing (§5.Testing)
   - Deployment (§5.Deployment)

2. For each task, write:
   - Concrete functional requirements (numbered list)
   - Specific data models (C# code)
   - Exact API contracts (HTTP method, route, request/response JSON)
   - 3-5 domain-specific Gherkin scenarios (with real data)
   - Exact validation commands (curl with real endpoints)
   - 10-15 task-specific DoD items

3. Remove ALL instances of:
   - "See description above"
   - "See implementation"
   - "RESTful endpoints following existing patterns"
   - Generic Gherkin ("Given valid input")

---

## BATON Token

**BATON:** 2026-02-12T12:30:00Z-PHASE15-S789-PARTIAL-FIX-x9k4p2w7

---

## Summary

**DONE:**
- Identified 110+ placeholder occurrences across S7-S9
- Completed 5/60 tasks (PRD-1601 to PRD-1605)
- Updated progress ledgers with warnings
- Created detailed status analysis

**NOT DONE:**
- 55 tasks still contain placeholders
- S7 tasks PRD-1606 to PRD-1620 (15 tasks)
- S8 tasks PRD-1621 to PRD-1640 (20 tasks)
- S9 tasks PRD-1641 to PRD-1660 (20 tasks)

**NEXT SESSION:**
- Complete remaining 55 task specifications
- OR implement only PRD-1601 to PRD-1605
- DO NOT attempt Codex execution on stub tasks

