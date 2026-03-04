# ADR-006: MudBlazor Render Mode Server and Shell Stability

**Status:** Accepted  
**Date:** 2026-03-04  
**Decision Makers:** WebUI migration stream

## Context

During MudBlazor migration, UI smoke runs intermittently failed with Blazor circuit errors triggered by Mud JS interop calls (`mudElementRef.getBoundingClientRect`, `mudScrollManager.*`) associated with `MudDrawer` lifecycle.

At the same time, local runtime profiles showed inconsistent serving of `_content/MudBlazor/MudBlazor.min.js` when launched outside the standard development profile, which amplified interop failures.

Blocking requirement: stop phase progression until `MudDrawer.UpdateHeight` instability is reduced to zero for migration smoke gates.

## Decision

Keep app root render mode as `render-mode="Server"` (non-prerendered) and replace `MudDrawer` shell usage with a deterministic responsive sidebar (`MudAppBar` + static `<aside>` nav).

Also enforce static web asset reliability and script order:
- enable `UseStaticWebAssets()` in `Program.cs`;
- load `_content/MudBlazor/MudBlazor.min.js` before `_framework/blazor.server.js`.

## Why this decision

- Removes the unstable component path (`MudDrawer.UpdateHeight`) from critical shell.
- Preserves MudBlazor as primary UI library without blocking on drawer-specific JS lifecycle behavior.
- Keeps smoke suite stable while migration of grids/forms proceeds.
- Avoids introducing prerender complexity while baseline migration is still active.

## Consequences

Positive:
- No observed drawer-related intermittent circuit errors in current smoke gate.
- Navigation remains responsive on desktop/mobile via static sidebar state toggle.

Trade-offs:
- Current shell does not use `MudDrawer`; we temporarily lose direct drawer feature parity.
- `render-mode="Server"` initial render characteristics differ from `ServerPrerendered`.

## Return Criteria (for `MudDrawer` and/or `ServerPrerendered`)

Reconsider `MudDrawer` and/or prerender mode only when all are true:
1. `_content/MudBlazor/MudBlazor.min.js` is consistently available in target runtime profiles.
2. 20 consecutive smoke runs complete with zero Mud JS interop/circuit errors.
3. E2E validates mobile and desktop navigation parity with stable `data-testid` selectors.
4. Decision is recorded in migration notes before switch.

## Verification

- UI smoke command:
  - `dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/LKvitai.MES.Tests.Warehouse.E2E.csproj --filter FullyQualifiedName~.Ui.`
- Latest gate at decision time: Passed `5/5`.
