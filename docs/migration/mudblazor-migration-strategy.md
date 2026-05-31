# MudBlazor Migration Strategy
**Date:** 2026-05-31  
**Branch analysed:** `feature/mudblazor-migration-discovery` vs `main`  
**Author:** Claude (analysis) — requires human review before implementation

---

## 1. Current State

### main branch
| Dimension | Status |
|-----------|--------|
| MudBlazor installed | Yes — `6.20.0` (referenced in csproj, registered in DI, providers in App.razor) |
| Pages using Mud components | **4 of 102** (App.razor, MainLayout.razor, AvailableStock.razor, Admin/Lots.razor) |
| Bootstrap CDN | **Present** — Bootstrap 5.3.3 + Bootstrap Icons 1.11.3 in `_Layout.cshtml` |
| Bootstrap class usage in Razor | Effectively 0 raw class tokens (grep clean) — but layout/shell still Bootstrap-structured |
| Portal design system | Active — `portal-tokens.css` + `portal-shell.css` loaded from `BuildingBlocks.WebUI` |
| AppTheme (Mud palette config) | **Not present** — no `AppTheme.cs`, Mud runs with default blue palette |

### feature/mudblazor-migration-discovery branch
| Dimension | Status |
|-----------|--------|
| Diverged from main | 2026-03-03 (commit `7b3e2d5`) |
| Commits ahead of main | **64** |
| Commits main added since diverge | **316** — significant drift |
| MudBlazor version | `6.20.0` — same as main, **no version gap** |
| Pages using Mud | **83 of 102** |
| Pages still on Bootstrap | **7** (AdminImport, AnalyticsFulfillment, AnalyticsQuality, CrossDock, Index, PickingTasks, WavePicking) |
| Bootstrap CDN in layout | **Removed** — layout is clean Mud-only |
| Bootstrap Icons (`bi-`) | **0 references** — fully cleaned |
| AppTheme.cs | Present in `Infrastructure/AppTheme.cs` — but **wrong palette** (uses blue `#0b57a4`, portal system is teal `#2f8f8b`) |
| portal-tokens.css loading | **Missing from `_Layout.cshtml`** — branch layout does not load BuildingBlocks CSS |
| CI bootstrap guard script | Present — `scripts/validate-webui-no-bootstrap.sh` |
| UI smoke tests (Playwright) | Present — 33–36 tests passing at freeze time |
| ADR | ADR-006 documents MudDrawer instability and resolution |
| Discovery notes | `docs/process/notes/mudblazor-migration-discovery.md` — 38 KB of detailed analysis |

---

## 2. Reuse Assessment

### What can be taken almost verbatim (~70% of the branch)
| Artefact | Source file(s) | Reuse quality |
|----------|---------------|---------------|
| Mud infrastructure (App.razor, DI wiring) | `App.razor`, `Program.cs` | High — identical Mud setup, only minor delta |
| AppTheme.cs skeleton | `Infrastructure/AppTheme.cs` | High — structure is good, palette needs correction |
| Migrated pages (76 of 83) | All pages except the 7 priority warehouse forms below | High for structure — **must verify each page against current main DTOs** because main moved 316 commits |
| SharedConfirmDialog, SharedErrorAlert | `Components/Shared*.razor` | High — drop-in |
| CI guard script | `scripts/validate-webui-no-bootstrap.sh` | Copy as-is |
| ADR-006 | `docs/adr/006-*.md` | Copy as-is |
| UI smoke test harness | `tests/.../E2E` | High — test structure is solid |

### What must be rewritten or reconciled (~30%)
| Item | Why |
|------|-----|
| `_Layout.cshtml` | Branch removed portal-tokens.css + portal-shell.css by accident — must be restored |
| `AppTheme.cs` palette | Branch uses `#0b57a4` (generic blue). Must map to portal teal: `--accent-500: #2f8f8b` etc. |
| Priority warehouse pages (forms) | `InboundShipmentCreate/Detail`, `Transfers/Create+Execute`, `StockAdjustments`, `AdminItems/Detail`, `AdminLocations`, `ReceivingQc`, `Putaway` — branch versions may use stale DTOs from 316 commits ago. **Treat as fresh migration using branch as template only.** |
| 7 unmigrated pages | AdminImport, AnalyticsFulfillment, AnalyticsQuality, CrossDock, Index, PickingTasks, WavePicking — not done at all |
| MainLayout nav shell | Port from branch but verify against current NavMenu entries (main added routes in 316 commits) |

### Reuse percentage estimate
- Infrastructure + non-priority pages: **~65–70% direct cherry-pick** with minor DTO validation  
- Priority warehouse forms: **~30–40% reuse** (structure/patterns yes, specific bindings need checking)  
- Fully new work: **7 unmigrated pages + palette + layout fix** = ~10% of total surface

**Overall: ~60–65% of the mudblazor branch work is directly actionable with verification, not rewrite.**

---

## 3. Migration Strategy

### Core principle: Forward-port selected files, do NOT merge the branch

The mudblazor branch is 316 commits behind main. A full merge would be a high-risk operation mixing UI and backend changes. Instead:

> **Create a new `feat/mudblazor` branch from `main`, cherry-pick infrastructure, then migrate pages in priority order.**

This gives:
- Clean commit history on main
- No risk of stale backend code from the old branch affecting production
- Ability to ship individual pages as they're validated

---

### Phase 0 — Infrastructure (prerequisite for everything, ~2–3 days)

**Goal:** Mud shell running on main, Bootstrap CDN removed, portal palette applied, coexistence confirmed.

**Steps:**

1. **Create integration branch** from `main` HEAD:
   ```
   git checkout -b feat/mudblazor-v2 main
   ```

2. **Port AppTheme.cs** from mudblazor branch, correcting palette to match portal tokens:
   ```csharp
   Palette = new PaletteLight {
       Primary      = "#2f8f8b",   // --accent-500
       PrimaryDarken = "#1d5d5a",  // --accent-700
       Secondary    = "#56aaa7",   // --accent-400
       Success      = "#19744a",   // --ok-fg
       Warning      = "#7c5f2a",   // --warn-fg
       Error        = "#8a1f12",   // --danger-fg
       Background   = "#f5f6f8",   // --n-50
       Surface      = "#ffffff",   // --n-0
       AppbarBackground = "#1f2632", // --n-800 (dark topbar)
       AppbarText   = "#ffffff",
       DrawerBackground = "#1f2632",
       DrawerText   = "#eef1f4",   // --n-100
   }
   ```

3. **Fix `_Layout.cshtml`** — keep portal-tokens.css and portal-shell.css, remove Bootstrap CDN, correct script order (MudBlazor.min.js BEFORE blazor.server.js per ADR-006):
   ```html
   <link href="_content/LKvitai.MES.BuildingBlocks.WebUI/css/portal-tokens.css" rel="stylesheet" />
   <link href="_content/LKvitai.MES.BuildingBlocks.WebUI/css/portal-shell.css" rel="stylesheet" />
   <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
   <!-- NO Bootstrap CDN -->
   ...
   <script src="_content/MudBlazor/MudBlazor.min.js"></script>
   <script src="_framework/blazor.server.js"></script>
   ```

4. **Port MainLayout.razor + NavMenu** from mudblazor branch, verify all routes match current main NavMenu.

5. **Port shared components**: SharedConfirmDialog, SharedErrorAlert; migrate ToastService bridge and LoadingSpinner internals to Mud.

6. **Port App.razor** theme binding (already partially done on main).

7. **Copy CI guard script** `validate-webui-no-bootstrap.sh` and wire to CI.

8. **Verify**: existing AvailableStock + Lots pages still work, smoke passes.

**Exit criteria for Phase 0:**
- `dotnet build` green
- Smoke: `AvailableStock_FullFlow` + `Lots_FullFlow` pass
- No Bootstrap CDN in layout
- Portal teal visible in topbar/primary button

---

### Phase 1 — Priority warehouse pages (business-driven, 1–2 weeks)

**Goal:** Migrate pages business has flagged as highest priority. Coexistence with unmigrated pages is still active.

**Priority order** (suggest confirming with business):

| Priority | Page | Route | Branch reuse? |
|----------|------|-------|--------------|
| P1 | Items list + detail | `/admin/items`, `/admin/items/{id}` | Template only — verify DTOs |
| P1 | Stock Adjustments | `/warehouse/stock/adjustments` | Template only |
| P1 | Inbound Shipments (list + create + detail) | `/warehouse/inbound/shipments/*` | Template only |
| P1 | Transfers (list + create + execute) | `/warehouse/transfers/*` | Template only |
| P2 | Receiving QC + Putaway | `/warehouse/inbound/qc`, `/warehouse/putaway` | Template only |
| P2 | Locations admin | `/admin/locations` | Template only |
| P2 | UoM + Suppliers | `/admin/uom`, `/admin/suppliers` | Template only |
| P3 | Stock Movement reports | `/reports/stock-movements` | Template only |
| P3 | Picking tasks | `/warehouse/picking/tasks` | Not migrated in branch |

**Per-page migration checklist:**
- [ ] Compare mudblazor branch version vs main version (diff DTOs, API calls, behaviors)
- [ ] Apply Mud component structure from branch, update any stale bindings to match main
- [ ] Add/verify `data-testid` anchors on page root, grid, form, error, submit
- [ ] Add UI smoke test (`PageName_PageSmoke`)
- [ ] Confirm build + smoke green before merging

---

### Phase 2 — Remaining pages (bulk port, ~1 week)

**Goal:** Close out remaining non-priority pages. Use mudblazor branch versions as direct source since these are lower-risk (read-only reports, admin catalog pages).

Pages to port in bulk (high branch reuse confidence):
- All Admin/* pages not done in Phase 1
- Reports/* pages
- Agnum/*, CycleCounts/*, Valuation/*
- Dashboard, AllocationDashboard
- Reservations, Labels, SalesOrders*, OutboundOrders*, etc.

Per page: copy from branch → verify DTO compatibility → build → smoke → merge.

---

### Phase 3 — Bootstrap cleanup and hardening (~2–3 days)

**Goal:** Remove all Bootstrap remnants, enforce via CI.

1. Remove Bootstrap 5.3.3 CDN from `_Layout.cshtml` (should already be done in Phase 0)
2. Remove Bootstrap Icons CDN
3. Replace any remaining `bi-` icons with `Icons.Material.*`
4. Wire `validate-webui-no-bootstrap.sh` to CI (if not done in Phase 0)
5. Migrate the 7 unmigrated pages (CrossDock, PickingTasks, WavePicking, AnalyticsFulfillment, AnalyticsQuality, AdminImport, Index) — these are low business priority, write fresh
6. Final full smoke run

---

## 4. Coexistence Architecture

During Phases 1–2, pages that haven't been migrated yet will coexist with Mud pages. This works because:

- MudBlazor CSS and Bootstrap CSS are **both loaded** until Phase 3 removes Bootstrap
- Mud providers (Dialog, Snackbar, Theme) are app-wide and don't affect Bootstrap pages
- Each page is a self-contained Blazor component — switching one doesn't affect others
- The shared layout shell (MainLayout) is Mud from Phase 0 onward, but Bootstrap page *content* is still valid inside it

**Risk:** CSS class collision between Bootstrap and Mud. Mitigation:
- Keep portal-tokens.css as the canonical palette, Mud theme reads from it
- Mud's CSS is scoped to its components; Bootstrap utility classes on unmigrated pages won't bleed
- If visual conflicts appear, use `!important` overrides in `site.css` as a short-term bridge — document each one for cleanup in Phase 3

---

## 5. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| DTO drift — branch page bindings reference removed/renamed fields | High | Medium | Per-page diff check before porting; build catches most cases |
| CSS conflict (Mud + Bootstrap coexistence) | Medium | Low | portal-tokens.css as single palette; test each page after Phase 0 |
| MudDrawer instability (ADR-006) | Low | Medium | ADR-006 decision stands: keep static aside sidebar, not MudDrawer |
| Smoke test selector fragility after merge | Medium | Medium | Follow `data-testid` / `getByRole` policy from branch |
| Priority pages have complex business logic that drifted heavily | Medium | High | Treat as fresh migration using branch as UI template; re-read current page logic |
| Bootstrap CDN removal breaking unmigrated pages | Low | High | Remove Bootstrap CDN only in Phase 3, after all pages are migrated |

---

## 6. Definition of Done per Phase

### Phase 0
- [ ] `dotnet build src/LKvitai.MES.sln` green
- [ ] `dotnet test ... --filter FullyQualifiedName~.Ui.` passes (existing smoke)
- [ ] No Bootstrap CDN or Bootstrap Icons CDN in `_Layout.cshtml`
- [ ] portal-tokens.css and portal-shell.css still loading
- [ ] Portal teal (#2f8f8b) renders as primary color in AppBar

### Phase 1 (per page)
- [ ] Page renders without visual regressions
- [ ] No Bootstrap `class=""` tokens in migrated file
- [ ] Page-level smoke test passes
- [ ] DTO fields verified against current main models

### Phase 2 (per page batch)
- [ ] All pages in batch build clean
- [ ] Existing smoke suite unchanged (no regressions)
- [ ] No new Bootstrap class references introduced

### Phase 3
- [ ] `scripts/validate-webui-no-bootstrap.sh` passes
- [ ] CI bootstrap guard runs on every PR
- [ ] All 102 Razor files use Mud components
- [ ] Full UI smoke suite passes (target ≥ 50 tests)

---

## 7. Files to Port from Branch (Reference List)

### Copy as-is (or with minimal changes)
```
src/.../WebUI/Infrastructure/AppTheme.cs           ← fix palette
src/.../WebUI/App.razor                            ← already done on main, minor delta
src/.../WebUI/Pages/_Layout.cshtml                 ← fix: restore portal CSS, correct script order
src/.../WebUI/Shared/MainLayout.razor              ← port + verify nav entries
src/.../WebUI/Components/SharedConfirmDialog.razor ← new, copy
src/.../WebUI/Components/SharedErrorAlert.razor    ← new, copy
scripts/validate-webui-no-bootstrap.sh             ← copy
docs/adr/006-mudblazor-render-mode-server-and-shell-stability.md ← copy
```

### Port with DTO verification (high reuse)
All pages with `<Mud` in the mudblazor branch that are NOT in the priority warehouse list:
- Admin/* (10 pages)
- Reports/* (7 pages)
- Agnum/* (2 pages), CycleCounts/* (4 pages), Valuation/* (4 pages)
- Dashboard, AllocationDashboard, AvailableStock (already on main), StockDashboard
- SalesOrders, OutboundOrders, InboundShipments list, Reservations, Labels, etc.

### Port as template + rewrite bindings (priority warehouse forms)
- AdminItems.razor, AdminItemDetail.razor
- AdminLocations.razor, AdminSuppliers.razor
- InboundShipmentCreate.razor, InboundShipmentDetail.razor
- Transfers/Create.razor, Transfers/Execute.razor, Transfers/List.razor
- StockAdjustments.razor
- ReceivingQc.razor, Putaway.razor

### Write fresh (not migrated in branch)
- AdminImport.razor
- PickingTasks.razor
- WavePicking.razor
- CrossDock.razor
- AnalyticsFulfillment.razor, AnalyticsQuality.razor
- Index.razor (typically trivial redirect)

---

## 8. Implementation Blueprint Prerequisites

Before writing implementation blueprints, confirm:
1. **Palette sign-off** — confirm teal `#2f8f8b` as Mud `Primary`. See `docs/ux/lkvitai-mes-ux-handoff.html` for full mapping.
2. **Business page priority list** — get ordered list of pages from warehouse team.
3. **Smoke infra** — confirm Playwright can run in local dev (E2E project setup from `tests/.../E2E`).
4. **Branch access** — `feature/mudblazor-migration-discovery` must remain accessible for cherry-picking. Do not delete it.
