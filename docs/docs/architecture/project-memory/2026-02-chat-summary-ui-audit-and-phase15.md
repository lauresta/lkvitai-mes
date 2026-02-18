# LKvitai.MES - Project Memory (chat digest)
Date: 2026-02
Scope: Phase 1.5 specs (S1-S9), UI↔API audit closure, repo refactor discussion context

## What matters (facts)
- Phase 1.5 specifications exist as sprint packs S1–S9 with TaskIds PRD-1501..PRD-1660 under:
  - docs/prod-ready/prod-ready-tasks-PHASE15-S*.md
  - docs/prod-ready/prod-ready-tasks-progress*.md
- Codex implemented Sprint 1–2 earlier; manual API validation had 403 until dev-token/auth workflow existed.
- UI↔API audit artifacts live under docs/audit:
  - UI-API-COVERAGE.md
  - UI-API-GAPS.md
  - UI-API-COUNTS.md
  - UI-UNIVERSE-TRACE.md
  - UI-VERIFY-STEPS.md
  - UI-FINAL-STATUS.md
- A “GapWorkbench” (generic endpoint-caller admin page) was proposed/attempted, then treated as a hack and removed.
- Claude performed an independent verification: GAP count is 0 via grep against UI-API-GAPS.md, and pages are workflow UI (not god-page/workbench). INTENTIONAL_NO_UI entries have justifications.
- Build is green; dotnet test had failing unit tests (handled separately; not used as proof that UI is incomplete).

## Decisions / rules (for future work)
- No generic “endpoint caller / workbench / god-page” UI to close audit gaps.
- UI is Blazor Server (src/LKvitai.MES.WebUI). Close gaps via real workflow pages/actions or mark INTENTIONAL_NO_UI with justification.
- Audit is source-of-truth and must be consistent (COUNTS/GAPS/COVERAGE/TRACE/FINAL must agree).

## Known documents referenced in discussions
- Repo refactor audit vs target structure:
  - docs/architecture/2026-02-16-repo-audit-vs-target.md
  - Target direction: Modules/*, BuildingBlocks/*, tests/ at root, Directory.Packages.props, fix layer leaks:
    - Application must not depend on Marten
    - SharedKernel must not depend on MediatR
    - Contracts must be dependency-free DTOs
  - Docker/compose cleanup (RabbitMQ dev-only / not deployed by LKvitai stack), reduce hardcoded csproj paths in Dockerfiles.

## Next topics planned (for a new chat / new session)
- Philosophy + execution plan for repo refactor (incremental, PR-based, with safety net).
- Establish reproducible scripts for audit generation and enforcement gates in CI.
