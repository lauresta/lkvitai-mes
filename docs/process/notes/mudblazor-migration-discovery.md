# MudBlazor Migration Discovery

Date: 2026-03-03  
Repo path (actual session): `/Users/bykovas/CodeRepos/clients/lauresta/LKvitai.MES`  
Branch (actual session): `main`

## Block 1 - Baseline

### 1) What the MudBlazor spike already changed (from `feature/ui-lib-spike-mudblazor` lineage)

Verified spike commits and follow-up hardening in git history:
- `708d34f` - add MudBlazor and register services (spike)
- `8b25ff3` - add Mud providers and minimal layout support (spike)
- `d7d9de5` - migrate Lots page to `MudDataGrid` + `ServerData` (spike)
- `dc9e183` - migrate Available Stock page to `MudDataGrid` (spike)
- `823f401` - fix conflicting `MudTextField` attrs on Lots
- `db44226` - stabilize Lots page-size selector against Mud DOM
- `07ae700` - close Mud overlay before Lots grid actions in UI tests
- `40233a4` - pin MudBlazor version for deterministic restore

Merged via PR chain:
- Merge `4cf3fdb` (PR #15, spike infra + first Mud migration work)
- Merge `c7288e5` (PR #16, Mud package/runtime stabilization)
- Merge `91aadcc` (PR #17, continuation on spike branch)

Current verified baseline in code:
- MudBlazor package referenced in WebUI (`MudBlazor`) and services wired in `Program.cs`.
- Providers enabled in `App.razor`: `MudThemeProvider`, `MudDialogProvider`, `MudSnackbarProvider`.
- Mud assets loaded in `_Layout.cshtml`.
- `/warehouse/admin/lots` and `/available-stock` have Mud grid-based flows covered by E2E tests.

### 2) data-testid and Playwright E2E presence

Current footprint:
- `data-testid` occurrences in WebUI Razor files: **34** (from ripgrep count).
- Dedicated UI E2E project present: `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E`.
- Mud smoke-style full-flow tests present:
  - `MudBlazorGridFullFlowTests.Lots_FullFlow`
  - `MudBlazorGridFullFlowTests.AvailableStock_FullFlow`

Baseline conclusion:
- MudBlazor foundation is real and test-backed, but migration is partial and still mixed with Bootstrap + legacy wrappers.
