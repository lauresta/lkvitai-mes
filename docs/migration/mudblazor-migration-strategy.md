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
| Bootstrap class usage in Razor | Present in many unmigrated pages/components — Bootstrap must stay until page migration is complete |
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

**Goal:** Mud theme and shared infrastructure wired on main. Bootstrap CDN stays — **do not touch it in this phase**. Portal palette applied to Mud. Script order fixed. Coexistence confirmed.

> **Rule:** Phase 0 is additive only. Nothing is removed. The app must look and behave exactly as before Phase 0, except Mud components now render with the correct portal palette.

**Designer follow-up decisions locked for Phase 1+:**
- Keep `.lk` globally on the Warehouse `MudLayout`. Overrides are scoped to `.lk .mud-*`, so Bootstrap-only pages are unaffected and any Mud component rendered from an unmigrated page still receives the approved design language.
- Reserve `.lk-bare` as a rare escape hatch for raw Mud experiments; prefer adding targeted resets there over removing the global `.lk` wrapper.
- Forms use one bordered base style, plus `.lk-field--filter` for 32px interactive toolbar filters with external mono-caps labels and `.lk-field--inline` for row-action/inline fields without labels.
- Dialogs use neutral headers by default. Destructive confirmations use `.lk-dialog--danger` on the header only, neutral body, and filled danger only for the final confirmation button.
- Disabled Mud buttons use the dashed disabled grammar globally. Destructive row actions are outlined danger; filled danger is reserved for final destructive confirmation.
- Use Material Outlined icons. Filled icons are allowed only for active nav and critical status signals. Nav icons, including collapsed sidebar icons, are 18px.
- Operational table rows are 30px everywhere, including admin/catalog and read-heavy reports.
- Admin/list screens use one density split: `--row-h: 30px` for display rows, `--control-h: 32px` for interactive inputs/buttons. Filter toolbar and grid should live in one `MudPaper.panel`, with the `Total`/page strip folded into the grid panel.
- `.lk-state` is for page/panel state; `.chip` is for one record, row, cell, or field.
- Golden Phase 1 screens: `AvailableStock` (operational list), `Admin/Lots` (CRUD), `StockAdjustments` (workflow/confirmations/state banners).

**What is safe to do in Phase 0:**

1. **Add `AppTheme.cs`** — new file, zero risk. Correct palette to match portal tokens and designer handoff (not the blue from the frozen branch):
   ```csharp
   Palette = new PaletteLight {
       Primary          = "#2f8f8b", // --accent-500
       PrimaryDarken    = "#1d5d5a", // --accent-700
       PrimaryLighten   = "#56aaa7", // --accent-400
       Secondary        = "#56aaa7", // teal family, never Mud pink
       Info             = "#1d5d5a", // --info-fg, never Mud blue
       Success          = "#19744a", // --ok-fg
       Warning          = "#7c5f2a", // --warn-fg
       Error            = "#8a1f12", // --danger-fg
       Background       = "#f5f6f8", // --n-50
       Surface          = "#ffffff", // --n-0
       AppbarBackground = "#20242c", // --dark
       DrawerBackground = "#20242c", // --dark
   }
   ```

2. **Wire theme in `App.razor`** — `<MudThemeProvider Theme="AppTheme.Default" />` (already has the tag, just add the attribute).

3. **Fix script order in `_Layout.cshtml`** — move `MudBlazor.min.js` to load BEFORE `blazor.server.js` (per ADR-006). Keep Bootstrap CDN and Bootstrap Icons CDN exactly where they are:
   ```html
   <!-- portal CSS — keep -->
   <link href="_content/LKvitai.MES.BuildingBlocks.WebUI/css/portal-tokens.css" ... />
   <link href="_content/LKvitai.MES.BuildingBlocks.WebUI/css/portal-shell.css" ... />
   <!-- Mud CSS — keep -->
   <link href="_content/MudBlazor/MudBlazor.min.css" ... />
   <!-- Bootstrap CDN — KEEP, do not remove -->
   <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/..." ... />
   <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/..." ... />
   ...
   <!-- script order: Mud JS first, then Blazor -->
   <script src="_content/MudBlazor/MudBlazor.min.js"></script>
   <script src="_framework/blazor.server.js"></script>
   ```

4. **Add shared Mud components**: SharedConfirmDialog, SharedErrorAlert (new files, additive). Skin them through opt-in bridge classes from `docs/migration/mudblazor-designer-handoff-annotated.html`: `.lk`, `.lk-dialog`, `.lk-state`, `.lk-grid`, `.panel`.

5. **Copy CI guard script** `validate-webui-no-bootstrap.sh` — but configure it in **warn-only mode** (log output, do NOT `exit 1` yet). Activating it as a hard gate happens in Phase 3 when Bootstrap is actually gone.

6. **Verify**: existing AvailableStock + Lots pages still work, all other pages visually unchanged.

**What is NOT allowed in Phase 0:**
- Removing Bootstrap CDN or Bootstrap Icons CDN
- Removing Bootstrap class tokens from any page
- Replacing `bi-*` icons
- Changing MainLayout or NavMenu structure (risk: navigation regression across all pages)

**Exit criteria for Phase 0:**
- `dotnet build` green
- Smoke: `AvailableStock_FullFlow` + `Lots_FullFlow` pass
- Portal teal (`#2f8f8b`) renders on Mud components (AvailableStock, Lots)
- Mud grids use 30px operational rows and scoped `.lk-grid` overrides
- All non-migrated pages visually unchanged
- Bootstrap CDN still loading
- Portal teal visible in topbar/primary button

---

### Phase 1 — Golden warehouse screens (pattern-driven, 2–4 days)

**Goal:** Migrate a small set of representative screens first, then reuse their settled density, spacing, and interaction patterns for the rest of Warehouse WebUI. Coexistence with unmigrated pages is still active.

**Golden screens:**

| Pattern | Page | Route | What it defines |
|---------|------|-------|-----------------|
| Dense operational list | Available Stock | `/available-stock` | Filter bar, result toolbar, dense grid, list/gallery alternatives, compact empty state |
| Admin CRUD/list | Lots | `/admin/lots` | Single panel with filters + server grid + pager, 30px rows, 32px controls |
| Workflow + history | Stock Adjustments | `/warehouse/stock/adjustments` | Create/action panel, warning state, dense history table, signed quantity chips |

**Deferred after golden screens:** Items, Locations, UoM, Suppliers, Inbound Shipments, Transfers, Receiving QC, Putaway, reports, picking tasks, reservations, labels, dashboards, and the remaining warehouse workflows. These should follow the closest golden pattern instead of inventing page-local density rules.

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

1. Migrate the 7 unmigrated pages (CrossDock, PickingTasks, WavePicking, AnalyticsFulfillment, AnalyticsQuality, AdminImport, Index) — these are low business priority, write fresh
2. Replace any remaining `bi-` icons with `Icons.Material.*`
3. Remove Bootstrap 5.3.3 CDN from `_Layout.cshtml`
4. Remove Bootstrap Icons CDN
5. Wire `validate-webui-no-bootstrap.sh` to CI as a hard gate
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
- [ ] Bootstrap CDN and Bootstrap Icons CDN **still present** in `_Layout.cshtml`
- [ ] portal-tokens.css and portal-shell.css still loading
- [ ] `MudBlazor.min.js` loads before `blazor.server.js`
- [ ] Portal teal (#2f8f8b) renders as primary color on Mud components (AvailableStock, Lots)
- [ ] All non-migrated pages visually unchanged

### Phase 1 (per page)
- [ ] Page renders without visual regressions
- [ ] No Bootstrap `class=""` tokens in migrated file
- [ ] Page-level smoke test passes
- [ ] DTO fields verified against current main models
- [ ] Mud components follow `docs/migration/mudblazor-designer-handoff-annotated.html`: `.panel`, `.lk-grid`, `.lk-state`, `.chip`, `.lk-toolbar`; 30px table rows; Material Outlined icons
- [ ] Compare migrated pages against the three golden screens: `AvailableStock`, `Admin/Lots`, `StockAdjustments`

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
src/.../WebUI/Components/SharedConfirmDialog.razor ← new, copy
src/.../WebUI/Components/SharedErrorAlert.razor    ← new, copy
scripts/validate-webui-no-bootstrap.sh             ← copy
docs/adr/006-mudblazor-render-mode-server-and-shell-stability.md ← copy
```

Do not port `MainLayout.razor` or `NavMenu.razor` in Phase 0. They already provide the current application shell and changing them risks global navigation regressions. Revisit shell changes only after page migrations make them necessary.

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
