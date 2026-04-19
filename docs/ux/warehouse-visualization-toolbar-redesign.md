# Warehouse Visualization — Toolbar Redesign Blueprint

**Target file:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/Visualization/Warehouse3D.razor`
**CSS file:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/Visualization/Warehouse3D.razor.css`
**Branch:** `feature/warehouse-3d-rack-blueprint`

---

## Problem Statement

The current toolbar is a two-column CSS grid with a large left "meta" block (2rem h1 title, subtitle, 4 info badges) and a right controls grid (6 form fields with uppercase labels). This takes ~180–220px of vertical space before the 3D model appears.

**Primary usability failures:**
1. `h1 "Warehouse Visualization"` + subtitle + 4 counter badges waste the dominant left half of the toolbar — none of these are actionable
2. Two separate search inputs (location address + item code) with equal visual weight — neither feels primary
3. Zone and Status filters are always fully visible — they dominate the form even when not in use
4. `"2D View"` is styled as `btn-warning` (amber primary CTA) — misleads visual hierarchy; it is a mode toggle, not an action
5. `"Hide Labels"` / `"Show Labels"` button has no persistent visual state — user cannot tell labels are currently on or off
6. The entire visual language is "enterprise form" — the page is actually a visual workspace
7. `viz-page` uses `min-height: calc(100vh - 120px)` with `padding: 1rem` and the toolbar has `border-radius: 18px` with `padding: 1rem` — the 3D canvas (`height: 72vh`) starts well below the fold

---

## Target Layout (after redesign)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  TOOLBAR (48px, sticky)                                                       │
│                                                                               │
│  [WH2 – Secondary ▾]  [⟳ 2m ago]           [🔍 Search SKU or location... ✕] │
│                                              [3D|2D]  [Labels●]  [Cam▾]       │
│                                              [Filters (2)]                    │
└──────────────────────────────────────────────────────────────────────────────┘
┌──────────────────────────────────────────────────────────────────────────────┐
│  CONTEXT STRIP (32px — only visible when filters are active OR item search   │
│  results are showing)                                                         │
│                                                                               │
│  [Zone: A ✕]  [Status: Low ✕]  [Clear all]                                  │
│   — or (when item search results exist) —                                    │
│  Found: "BOLT-M8" (SKU: BLT-M8)  ·  [WH2-A1-03  12.5] [WH2-B3-09  4.0]    │
│  [Also in: WH1 (2 bins)]  [✕ Clear item search]                             │
└──────────────────────────────────────────────────────────────────────────────┘
┌─────────────────────────────────┐  ┌──────────────────────────────────────┐
│                                 │  │  (empty state / selected bin detail)  │
│    3D / 2D VIEWER               │  │  — shown when a bin/slot/rack is     │
│    (fills all remaining height) │  │    selected                          │
│                                 │  │                                      │
└─────────────────────────────────┘  └──────────────────────────────────────┘
```

**What is removed from the toolbar:**
- `h1 "Warehouse Visualization"` — page title lives in the nav/breadcrumb or is simply gone (the 3D model makes the purpose self-evident)
- Subtitle `"Bins, racks and slot occupancy in one screen."` — removed entirely
- Counter badges (`1 bins`, `7 racks`, `124 slots`) — removed; not actionable
- All `viz-label` uppercase labels (WAREHOUSE, VIEW, LOCATION OR ADDRESS, etc.) — removed; placeholder text is sufficient
- The `viz-toolbar-meta` left column — removed entirely

**What moves / changes:**
- Item search results panel (`viz-item-panel`) moves into the **context strip** as an inline row — not a separate card below the toolbar
- Zone + Status filters move inside a **Filters popover** behind a single button
- `"2D View"` amber button → **3D | 2D segmented control**
- `"Hide/Show Labels"` button → **Labels toggle pill** with persistent ON/OFF state
- Camera mode `<select>` stays but is renamed to just a dropdown icon button (compact)

---

## Detailed Implementation Tasks

### Task 1 — Remove the left meta block

**In `Warehouse3D.razor`**, delete lines 20–33:

```razor
<!-- DELETE THIS ENTIRE BLOCK -->
<div class="viz-toolbar-meta">
    <div class="viz-heading-row">
        <div>
            <h1 class="viz-title">Warehouse Visualization</h1>
            <p class="viz-subtitle">Bins, racks and slot occupancy in one screen.</p>
        </div>
        <div class="viz-meta-badges">
            <span class="viz-badge">@CurrentWarehouseLabel</span>
            <span class="viz-badge">@($"{_model?.Bins.Count ?? 0} bins")</span>
            <span class="viz-badge">@($"{_model?.Racks.Count ?? 0} racks")</span>
            <span class="viz-badge">@($"{_model?.Slots.Count ?? 0} slots")</span>
        </div>
    </div>
</div>
```

**In `Warehouse3D.razor.css`**, delete the CSS blocks for:
- `.viz-toolbar-meta`
- `.viz-heading-row`
- `.viz-title`
- `.viz-subtitle`
- `.viz-meta-badges`
- `.viz-badge`
- `.viz-label`

---

### Task 2 — Rebuild the toolbar as a single flex row

**Replace** the existing `<div class="viz-toolbar">` content with:

```razor
<div class="viz-toolbar">

    <!-- LEFT: warehouse selector + refresh -->
    <div class="viz-toolbar-left">
        <select class="viz-warehouse-pill"
                @bind="_warehouseCode"
                @bind:after="OnWarehouseChangedAsync"
                aria-label="Select warehouse">
            @foreach (var warehouse in _warehouses)
            {
                <option value="@warehouse.Code">@warehouse.Code – @warehouse.Name</option>
            }
        </select>

        <button class="viz-icon-btn"
                title="Refresh"
                aria-label="Refresh warehouse data"
                @onclick="RefreshAsync">
            <i class="bi bi-arrow-clockwise @(_isLoading ? "viz-spin" : "")"></i>
        </button>
        @if (_lastRefreshedAt.HasValue)
        {
            <span class="viz-refresh-age" title="@_lastRefreshedAt.Value.ToString("HH:mm:ss")">
                @FormatRefreshAge(_lastRefreshedAt.Value)
            </span>
        }
    </div>

    <!-- RIGHT: search + view controls -->
    <div class="viz-toolbar-right">

        <!-- unified search -->
        <div class="viz-search-box @(_searchExpanded ? "viz-search-expanded" : "")">
            <i class="bi bi-search viz-search-icon"></i>
            <input class="viz-search-input"
                   placeholder="Search SKU, item name, or location..."
                   value="@_unifiedSearchQuery"
                   @oninput="OnUnifiedSearchInput"
                   @onfocus="() => _searchExpanded = true"
                   @onblur="OnSearchBlur"
                   @onkeydown="HandleUnifiedSearchKeyAsync"
                   aria-label="Search warehouse" />
            @if (!string.IsNullOrWhiteSpace(_unifiedSearchQuery))
            {
                <button class="viz-search-clear"
                        aria-label="Clear search"
                        @onclick="ClearUnifiedSearchAsync">
                    <i class="bi bi-x"></i>
                </button>
            }
            @if (_showSuggestions && _autocompleteSuggestions.Count > 0)
            {
                <div class="list-group viz-suggestions">
                    @foreach (var suggestion in _autocompleteSuggestions)
                    {
                        <button type="button"
                                class="list-group-item list-group-item-action"
                                @onclick="() => SelectSuggestionAsync(suggestion)">
                            @suggestion
                        </button>
                    }
                </div>
            }
        </div>

        <!-- 3D / 2D segmented control -->
        <div class="viz-segmented" role="group" aria-label="View mode">
            <button class="viz-seg-btn @(_is3dView ? "viz-seg-active" : "")"
                    @onclick="() => EnsureViewModeAsync(true)"
                    aria-pressed="@_is3dView.ToString().ToLowerInvariant()">
                3D
            </button>
            <button class="viz-seg-btn @(!_is3dView ? "viz-seg-active" : "")"
                    @onclick="() => EnsureViewModeAsync(false)"
                    aria-pressed="@(!_is3dView).ToString().ToLowerInvariant()">
                2D
            </button>
        </div>

        <!-- labels toggle (3D only) -->
        @if (_is3dView)
        {
            <button class="viz-toggle-pill @(_showRackLabels ? "viz-toggle-on" : "viz-toggle-off")"
                    title="@(_showRackLabels ? "Labels visible — click to hide" : "Labels hidden — click to show")"
                    aria-label="Toggle rack labels"
                    aria-checked="@_showRackLabels.ToString().ToLowerInvariant()"
                    role="switch"
                    @onclick="ToggleRackLabelsAsync">
                <i class="bi bi-tag"></i>
                Labels
                <span class="viz-toggle-dot"></span>
            </button>
        }

        <!-- camera mode (3D only, compact) -->
        @if (_is3dView)
        {
            <select class="viz-cam-select"
                    value="@_cameraMode"
                    @onchange="OnCameraModeChangedAsync"
                    aria-label="Camera angle"
                    title="Camera angle">
                <option value="overview">Overview</option>
                <option value="low">Low angle</option>
            </select>
        }

        <!-- filters button -->
        <div class="viz-filters-wrap">
            <button class="viz-filters-btn @(_filtersPopoverOpen ? "viz-filters-open" : "")"
                    aria-label="Open filters (@ActiveFilterCount active)"
                    @onclick="ToggleFiltersPopover">
                <i class="bi bi-funnel"></i>
                Filters
                @if (ActiveFilterCount > 0)
                {
                    <span class="viz-filter-badge">@ActiveFilterCount</span>
                }
            </button>

            @if (_filtersPopoverOpen)
            {
                <div class="viz-filters-popover" role="dialog" aria-label="Filters">
                    <div class="viz-filters-section">
                        <div class="viz-filters-section-label">Zone</div>
                        <div class="viz-filters-chips">
                            @foreach (var zone in ZoneOptions)
                            {
                                <button class="viz-chip @(_zoneFilter == zone ? "viz-chip-active" : "")"
                                        @onclick="() => SetZoneFilterAsync(zone)">
                                    @zone
                                </button>
                            }
                        </div>
                    </div>
                    <div class="viz-filters-section">
                        <div class="viz-filters-section-label">Status</div>
                        <div class="viz-filters-chips">
                            @foreach (var (value, label) in StatusOptions)
                            {
                                <button class="viz-chip @(_statusFilter == value ? "viz-chip-active" : "")"
                                        @onclick="() => SetStatusFilterAsync(value)">
                                    @label
                                </button>
                            }
                        </div>
                    </div>
                    <div class="viz-filters-footer">
                        <button class="btn btn-sm btn-outline-secondary" @onclick="ResetFiltersAsync">Reset</button>
                        <button class="btn btn-sm btn-primary" @onclick="() => _filtersPopoverOpen = false">Done</button>
                    </div>
                </div>
            }
        </div>

    </div>
</div>
```

---

### Task 3 — Add context strip

**Add this block immediately after the closing `</div>` of `viz-toolbar`**, before the item panel / loading / content:

```razor
@{
    var showContextStrip = ActiveFilterCount > 0 || _hasItemSearchResults;
}
@if (showContextStrip)
{
    <div class="viz-context-strip">

        @if (_hasItemSearchResults)
        {
            <!-- item search results row -->
            <span class="viz-ctx-label">
                <strong>@_itemSearchResolvedLabel</strong>
                @if (!string.IsNullOrWhiteSpace(_itemSearchResolvedSku))
                {
                    <span class="viz-ctx-sku">@_itemSearchResolvedSku</span>
                }
            </span>

            @if (_currentWarehouseItemHits.Count > 0)
            {
                @foreach (var hit in _currentWarehouseItemHits)
                {
                    <button class="viz-hit-chip"
                            @onclick="() => FocusBinAsync(hit.LocationCode)"
                            title="@hit.LocationCode — qty @hit.PhysicalQty.ToString("0.###", CultureInfo.InvariantCulture)">
                        @hit.LocationCode
                        <strong>@hit.PhysicalQty.ToString("0.###", CultureInfo.InvariantCulture)</strong>
                    </button>
                }
            }
            else
            {
                <span class="viz-ctx-warn">Not in @CurrentWarehouseCode</span>
            }

            @if (_otherWarehouseSummaries.Count > 0)
            {
                <span class="viz-ctx-other">
                    Also in: @string.Join(", ", _otherWarehouseSummaries.Select(x => $"{x.WarehouseCode} ({x.BinCount})"))
                </span>
            }

            <button class="viz-ctx-clear"
                    aria-label="Clear item search"
                    @onclick="ClearItemSearchAsync">
                <i class="bi bi-x"></i>
            </button>
        }
        else if (ActiveFilterCount > 0)
        {
            <!-- active filter chips -->
            @if (_zoneFilter != "ALL")
            {
                <span class="viz-filter-chip">
                    Zone: @_zoneFilter
                    <button aria-label="Remove zone filter" @onclick="() => SetZoneFilterAsync("ALL")">
                        <i class="bi bi-x"></i>
                    </button>
                </span>
            }
            @if (_statusFilter != "ALL")
            {
                <span class="viz-filter-chip">
                    Status: @_statusFilter
                    <button aria-label="Remove status filter" @onclick="() => SetStatusFilterAsync("ALL")">
                        <i class="bi bi-x"></i>
                    </button>
                </span>
            }
            <button class="viz-ctx-clear-all" @onclick="ResetFiltersAsync">Clear all</button>
        }

    </div>
}
```

**Remove** the old `viz-item-panel` block (lines 147–192 in the original razor file) since its content is now in the context strip.

---

### Task 4 — Fix page layout to maximize 3D view height

**In `Warehouse3D.razor.css`**, replace the `.viz-page` and `.viz-content` / `.viz-canvas` rules:

```css
/* REPLACE .viz-page */
.viz-page {
    display: flex;
    flex-direction: column;
    height: calc(100vh - 56px);   /* subtract nav height; adjust if nav changes */
    overflow: hidden;
    background:
        radial-gradient(circle at top left, rgba(191, 219, 254, 0.18), transparent 28%),
        linear-gradient(180deg, #f8fbff 0%, #f4f6f8 100%);
}

/* REPLACE .viz-toolbar */
.viz-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    height: 48px;
    padding: 0 12px;
    background: rgba(255, 255, 255, 0.97);
    border-bottom: 1px solid #d9e2ec;
    flex-shrink: 0;
    position: sticky;
    top: 0;
    z-index: 200;
    /* remove border-radius, box-shadow, backdrop-filter — those belong on the old card */
}

/* REPLACE .viz-content */
.viz-content {
    display: grid;
    grid-template-columns: 1fr 320px;
    gap: 0;
    flex: 1;
    min-height: 0;
    overflow: hidden;
}

/* REPLACE .viz-canvas */
.viz-canvas {
    width: 100%;
    height: 100%;
    border-radius: 0;
    overflow: hidden;
}

/* REPLACE .viz-2d-canvas */
.viz-2d-canvas {
    width: 100%;
    height: 100%;
    background: #ffffff;
}

/* REPLACE .viz-canvas-wrap, .viz-details borders */
.viz-canvas-wrap {
    position: relative;
    overflow: hidden;
    border: none;
    border-right: 1px solid #dee2e6;
    border-radius: 0;
}

.viz-details {
    padding: 1rem;
    overflow-y: auto;
    border: none;
    border-radius: 0;
}
```

---

### Task 5 — New CSS for toolbar components

**Add** these new rules to `Warehouse3D.razor.css` (replace / extend removed blocks):

```css
/* ── Toolbar layout ─────────────────────────────────────────────── */
.viz-toolbar-left {
    display: flex;
    align-items: center;
    gap: 6px;
    flex-shrink: 0;
}

.viz-toolbar-right {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-left: auto;
}

/* ── Warehouse selector pill ─────────────────────────────────────── */
.viz-warehouse-pill {
    height: 32px;
    padding: 0 10px 0 8px;
    border: 1px solid #d9e2ec;
    border-radius: 6px;
    background: #f8fbff;
    color: #1f3a5f;
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    max-width: 200px;
}
.viz-warehouse-pill:focus { outline: 2px solid #3b82f6; outline-offset: 1px; }

/* ── Icon button ─────────────────────────────────────────────────── */
.viz-icon-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    border: 1px solid #d9e2ec;
    border-radius: 6px;
    background: transparent;
    color: #52606d;
    cursor: pointer;
    font-size: 15px;
}
.viz-icon-btn:hover { background: #f1f5f9; color: #1f3a5f; }
.viz-icon-btn:focus { outline: 2px solid #3b82f6; outline-offset: 1px; }

.viz-spin { animation: viz-spin 1s linear infinite; }
@keyframes viz-spin { to { transform: rotate(360deg); } }

.viz-refresh-age {
    font-size: 11px;
    color: #8a9ab0;
    white-space: nowrap;
    user-select: none;
}

/* ── Unified search box ──────────────────────────────────────────── */
.viz-search-box {
    position: relative;
    display: flex;
    align-items: center;
    width: 220px;
    transition: width 180ms ease;
}
.viz-search-box.viz-search-expanded { width: 300px; }

.viz-search-icon {
    position: absolute;
    left: 9px;
    color: #8a9ab0;
    font-size: 13px;
    pointer-events: none;
}

.viz-search-input {
    width: 100%;
    height: 32px;
    padding: 0 28px 0 30px;
    border: 1px solid #d9e2ec;
    border-radius: 6px;
    background: #f8fbff;
    color: #212529;
    font-size: 13px;
}
.viz-search-input:focus {
    outline: 2px solid #3b82f6;
    outline-offset: 1px;
    background: #fff;
}
.viz-search-input::placeholder { color: #adb5bd; }

.viz-search-clear {
    position: absolute;
    right: 6px;
    display: flex;
    align-items: center;
    justify-content: center;
    width: 20px;
    height: 20px;
    border: none;
    background: none;
    color: #8a9ab0;
    cursor: pointer;
    font-size: 14px;
    padding: 0;
}
.viz-search-clear:hover { color: #343a40; }

/* ── Segmented control (3D / 2D) ─────────────────────────────────── */
.viz-segmented {
    display: flex;
    border: 1px solid #d9e2ec;
    border-radius: 6px;
    overflow: hidden;
    flex-shrink: 0;
}

.viz-seg-btn {
    padding: 0 12px;
    height: 32px;
    border: none;
    background: transparent;
    color: #52606d;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    white-space: nowrap;
}
.viz-seg-btn + .viz-seg-btn {
    border-left: 1px solid #d9e2ec;
}
.viz-seg-btn.viz-seg-active {
    background: #1f3a5f;
    color: #fff;
}
.viz-seg-btn:focus { outline: 2px solid #3b82f6; outline-offset: -2px; }

/* ── Labels toggle pill ──────────────────────────────────────────── */
.viz-toggle-pill {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    height: 32px;
    padding: 0 10px;
    border-radius: 6px;
    border: 1px solid #d9e2ec;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    white-space: nowrap;
    transition: background 120ms, color 120ms, border-color 120ms;
}
.viz-toggle-pill .viz-toggle-dot {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    margin-left: 2px;
}
.viz-toggle-on {
    background: #e0f2fe;
    border-color: #7dd3fc;
    color: #0369a1;
}
.viz-toggle-on .viz-toggle-dot { background: #0369a1; }
.viz-toggle-off {
    background: transparent;
    border-color: #d9e2ec;
    color: #8a9ab0;
}
.viz-toggle-off .viz-toggle-dot { background: #d1d5db; }

/* ── Camera select ───────────────────────────────────────────────── */
.viz-cam-select {
    height: 32px;
    padding: 0 8px;
    border: 1px solid #d9e2ec;
    border-radius: 6px;
    background: transparent;
    color: #52606d;
    font-size: 12px;
    cursor: pointer;
    max-width: 110px;
}

/* ── Filters button + popover ────────────────────────────────────── */
.viz-filters-wrap {
    position: relative;
    flex-shrink: 0;
}

.viz-filters-btn {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    height: 32px;
    padding: 0 10px;
    border: 1px solid #d9e2ec;
    border-radius: 6px;
    background: transparent;
    color: #52606d;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    white-space: nowrap;
}
.viz-filters-btn:hover { background: #f1f5f9; }
.viz-filters-btn.viz-filters-open { border-color: #3b82f6; color: #1d4ed8; background: #eff6ff; }
.viz-filter-badge {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-width: 18px;
    height: 18px;
    padding: 0 4px;
    border-radius: 9px;
    background: #f59e0b;
    color: #fff;
    font-size: 10px;
    font-weight: 700;
    line-height: 1;
}

.viz-filters-popover {
    position: absolute;
    top: calc(100% + 6px);
    right: 0;
    width: 260px;
    border: 1px solid #d9e2ec;
    border-radius: 10px;
    background: #fff;
    box-shadow: 0 8px 24px rgba(15, 23, 42, 0.12);
    padding: 12px;
    z-index: 300;
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.viz-filters-section-label {
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: #8a9ab0;
    margin-bottom: 6px;
}

.viz-filters-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 5px;
}

.viz-chip {
    display: inline-flex;
    align-items: center;
    height: 26px;
    padding: 0 10px;
    border: 1px solid #d9e2ec;
    border-radius: 13px;
    background: transparent;
    color: #343a40;
    font-size: 12px;
    cursor: pointer;
}
.viz-chip:hover { background: #f1f5f9; }
.viz-chip.viz-chip-active {
    background: #1f3a5f;
    border-color: #1f3a5f;
    color: #fff;
}

.viz-filters-footer {
    display: flex;
    justify-content: flex-end;
    gap: 6px;
    padding-top: 4px;
    border-top: 1px solid #f1f5f9;
}

/* ── Context strip ───────────────────────────────────────────────── */
.viz-context-strip {
    display: flex;
    align-items: center;
    gap: 6px;
    height: 36px;
    padding: 0 12px;
    background: #f8fbff;
    border-bottom: 1px solid #e9eef4;
    flex-shrink: 0;
    overflow-x: auto;
    scrollbar-width: none;
    white-space: nowrap;
}
.viz-context-strip::-webkit-scrollbar { display: none; }

.viz-ctx-label {
    font-size: 12px;
    color: #1f3a5f;
    font-weight: 600;
    display: flex;
    align-items: center;
    gap: 5px;
}

.viz-ctx-sku {
    font-size: 11px;
    color: #52606d;
    font-weight: 400;
    background: #e9eef4;
    padding: 1px 6px;
    border-radius: 4px;
}

.viz-ctx-warn {
    font-size: 12px;
    color: #b45309;
    font-style: italic;
}

.viz-ctx-other {
    font-size: 11px;
    color: #8a9ab0;
}

.viz-hit-chip {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    height: 24px;
    padding: 0 10px;
    border: 1px solid #bfdbfe;
    border-radius: 12px;
    background: #eff6ff;
    color: #1d4ed8;
    font-size: 12px;
    cursor: pointer;
    white-space: nowrap;
}
.viz-hit-chip:hover { background: #dbeafe; }
.viz-hit-chip strong { color: #1e40af; }

.viz-filter-chip {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    height: 24px;
    padding: 0 8px;
    border-radius: 12px;
    background: #e0f2fe;
    border: 1px solid #7dd3fc;
    color: #0369a1;
    font-size: 12px;
}
.viz-filter-chip button {
    background: none;
    border: none;
    padding: 0;
    cursor: pointer;
    color: inherit;
    opacity: 0.7;
    display: flex;
    align-items: center;
    font-size: 13px;
    line-height: 1;
}
.viz-filter-chip button:hover { opacity: 1; }

.viz-ctx-clear {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    border: none;
    background: none;
    color: #8a9ab0;
    cursor: pointer;
    font-size: 15px;
    margin-left: auto;
    flex-shrink: 0;
}
.viz-ctx-clear:hover { color: #343a40; }

.viz-ctx-clear-all {
    margin-left: auto;
    border: none;
    background: none;
    color: #1d4ed8;
    font-size: 12px;
    cursor: pointer;
    padding: 0;
    flex-shrink: 0;
}
.viz-ctx-clear-all:hover { text-decoration: underline; }
```

---

### Task 6 — C# state changes in `@code` block

**Add these new fields:**

```csharp
// Unified search (replaces _searchQuery + _itemSearchQuery split)
private string _unifiedSearchQuery = string.Empty;
private bool _searchExpanded;

// Filters popover state
private bool _filtersPopoverOpen;

// Refresh timestamp
private DateTimeOffset? _lastRefreshedAt;
```

**Add these new computed properties:**

```csharp
private int ActiveFilterCount =>
    (_zoneFilter != "ALL" ? 1 : 0) +
    (_statusFilter != "ALL" ? 1 : 0);

private static IReadOnlyList<(string Value, string Label)> StatusOptions =>
[
    ("EMPTY", "Empty"),
    ("LOW", "Low"),
    ("MEDIUM", "Medium"),
    ("FULL", "Full"),
    ("RESERVED", "Reserved"),
    ("OVER_CAPACITY", "Over capacity"),
];
```

**Replace** `RefreshAsync` to track timestamp:

```csharp
private async Task RefreshAsync()
{
    await LoadVisualizationAsync();
    _lastRefreshedAt = DateTimeOffset.Now;
}
```

**Add** `LoadVisualizationAsync` timestamp update — at the end of the `finally` block in the existing method add:
```csharp
_lastRefreshedAt = DateTimeOffset.Now;
```

**Add** new methods for unified search, filter helpers, and popover:

```csharp
private Task OnUnifiedSearchInput(ChangeEventArgs args)
{
    _unifiedSearchQuery = args.Value?.ToString() ?? string.Empty;
    // update autocomplete from location codes
    _autocompleteSuggestions = WarehouseVisualizationSearch.GetSuggestions(
        _model?.Bins ?? [],
        _unifiedSearchQuery,
        maxResults: 8);
    _showSuggestions = _autocompleteSuggestions.Count > 0;
    return Task.CompletedTask;
}

private async Task HandleUnifiedSearchKeyAsync(KeyboardEventArgs args)
{
    if (args.Key == "Enter")
    {
        _showSuggestions = false;
        // try location search first; fall back to item search
        if (_model is not null && !string.IsNullOrWhiteSpace(_unifiedSearchQuery))
        {
            var match = WarehouseVisualizationSearch.FindBestMatch(
                _model.Bins, _unifiedSearchQuery.Trim());
            if (match is not null)
            {
                await FocusBinAsync(match.Code);
                return;
            }
        }
        // fall back to item/SKU search
        _itemSearchQuery = _unifiedSearchQuery;
        await SearchItemAsync();
    }
    else if (args.Key == "Escape")
    {
        _showSuggestions = false;
        _searchExpanded = false;
        await ClearUnifiedSearchAsync();
    }
}

private Task OnSearchBlur()
{
    // small delay to allow click on suggestion
    _searchExpanded = !string.IsNullOrWhiteSpace(_unifiedSearchQuery);
    return Task.CompletedTask;
}

private async Task ClearUnifiedSearchAsync()
{
    _unifiedSearchQuery = string.Empty;
    _itemSearchQuery = string.Empty;
    _searchQuery = string.Empty;
    _showSuggestions = false;
    await ClearItemSearchAsync();
}

private void ToggleFiltersPopover()
{
    _filtersPopoverOpen = !_filtersPopoverOpen;
}

private async Task SetZoneFilterAsync(string zone)
{
    _zoneFilter = zone;
    await OnZoneFilterChangedInternalAsync();
}

private async Task SetStatusFilterAsync(string status)
{
    _statusFilter = status;
    await OnStatusFilterChangedInternalAsync();
}

private async Task ResetFiltersAsync()
{
    _zoneFilter = "ALL";
    _statusFilter = "ALL";
    _filtersPopoverOpen = false;
    if (_is3dView)
    {
        _renderRequested = true;
        await InvokeAsync(StateHasChanged);
    }
}

private async Task OnZoneFilterChangedInternalAsync()
{
    if (_is3dView)
    {
        _renderRequested = true;
        await InvokeAsync(StateHasChanged);
    }
}

private async Task OnStatusFilterChangedInternalAsync()
{
    if (_is3dView)
    {
        _renderRequested = true;
        await InvokeAsync(StateHasChanged);
    }
}

private async Task EnsureViewModeAsync(bool want3d)
{
    if (_is3dView == want3d) return;
    await ToggleViewAsync();
}

private static string FormatRefreshAge(DateTimeOffset refreshedAt)
{
    var age = DateTimeOffset.Now - refreshedAt;
    if (age.TotalSeconds < 60) return "just now";
    if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}m ago";
    return $"{(int)age.TotalHours}h ago";
}
```

**Remove** the old `OnZoneFilterChangedAsync(ChangeEventArgs)` and `OnStatusFilterChangedAsync(ChangeEventArgs)` methods — they are replaced by the internal versions above.

---

### Task 7 — Close filters popover on outside click

Add a `@onclick` close handler to the page wrapper, using JS interop or a simple `@onclick` on a backdrop div:

```razor
@if (_filtersPopoverOpen)
{
    <div class="viz-popover-backdrop" @onclick="() => _filtersPopoverOpen = false"></div>
}
```

```css
.viz-popover-backdrop {
    position: fixed;
    inset: 0;
    z-index: 299;
    background: transparent;
}
```

---

### Task 8 — Update responsive breakpoints

**In `Warehouse3D.razor.css`**, replace the `@media` breakpoints:

```css
@media (max-width: 1200px) {
    .viz-content {
        grid-template-columns: 1fr;
    }
    .viz-details {
        max-height: 300px;
        border-top: 1px solid #dee2e6;
    }
}

@media (max-width: 900px) {
    .viz-toolbar-right {
        gap: 4px;
    }
    .viz-search-box {
        width: 160px;
    }
    .viz-search-box.viz-search-expanded {
        width: 220px;
    }
    .viz-cam-select {
        display: none; /* hide camera select on smaller viewports — use default */
    }
}
```

---

## What Is NOT Changed

The following existing code is left completely untouched:

| Area | Reason |
|---|---|
| `warehouseVisualization.render(...)` JS interop | Not part of toolbar scope |
| `warehouseVisualization.focusBin(...)` JS interop | Not part of toolbar scope |
| `OnBinSelectedFromJs` / `OnSlotSelectedFromJs` / `OnRackSelectedFromJs` | Not part of toolbar scope |
| `viz-details` panel HTML structure (bin/slot/rack detail) | Works well; not changed |
| `viz-legend` overlay | Works well; not changed |
| 2D SVG rendering block | Not part of toolbar scope |
| `WarehouseVisualizationSearch` service | Not part of toolbar scope |
| `VisualizationClient` / `StockClient` calls | Not part of toolbar scope |
| `IAsyncDisposable` pattern | Not part of toolbar scope |
| `BuildRenderModel()` | Not part of toolbar scope |

---

## Acceptance Criteria

### Layout

- [ ] Toolbar renders as a single 48px row — no second row, no form card, no left meta block
- [ ] `h1`, subtitle, and counter badges (`N bins`, `N racks`, `N slots`) are not rendered anywhere on the page
- [ ] 3D/2D viewer occupies the full remaining viewport height below toolbar + optional context strip
- [ ] Context strip is not rendered when no filters are active and no item search results exist
- [ ] Context strip appears when Zone or Status filter is not "ALL"
- [ ] Context strip appears when item search has results
- [ ] Page does not scroll vertically; the 3D canvas fills available height

### Warehouse selector

- [ ] Warehouse selector is a compact `<select>` styled as a pill, not a labeled form field
- [ ] Changing warehouse triggers data reload; loading spinner shows inside toolbar (refresh icon spins)

### Search

- [ ] Single search input replaces both the old location input and item input
- [ ] Pressing Enter: first attempts location/bin match; if no match, falls back to SKU/item search
- [ ] When item results exist, they appear in the context strip as clickable chips (not a card below the toolbar)
- [ ] Each hit chip shows location code + quantity; clicking it focuses the bin in the 3D view
- [ ] `✕` button in context strip clears item search and removes the strip
- [ ] Autocomplete suggestions still appear from the location search as before

### View mode

- [ ] 3D/2D segmented control renders with exactly two segments
- [ ] Active segment is visually filled (dark bg + white text); inactive is ghost
- [ ] `aria-pressed` reflects current state on each segment button

### Labels toggle

- [ ] Toggle shows clearly different ON vs OFF visual state (filled blue vs outline muted)
- [ ] `aria-checked` reflects current state
- [ ] Toggle is not rendered in 2D mode

### Filters

- [ ] Zone and Status selects are not visible in the toolbar by default
- [ ] Filters button shows amber badge with count when any filter is active
- [ ] Clicking Filters button opens popover with Zone chips and Status chips
- [ ] Active chip (matching current filter) is filled dark; inactive is outline
- [ ] Clicking an active chip deselects it (sets back to ALL)
- [ ] "Reset" button in popover sets all filters to ALL
- [ ] "Done" closes the popover
- [ ] Clicking outside the popover closes it
- [ ] When filters are active, chips appear in the context strip with individual `✕` dismiss buttons
- [ ] "Clear all" appears in context strip when ≥1 filter is active; clicking it resets all

### Refresh

- [ ] Refresh is an icon-only button (no label text)
- [ ] After load completes, a "N min ago" or "just now" age text appears next to the refresh button
- [ ] Refresh icon spins while `_isLoading` is true

### Accessibility

- [ ] All icon-only buttons have `aria-label` and `title`
- [ ] Search input has `aria-label="Search warehouse"`
- [ ] Segmented buttons have `aria-pressed`
- [ ] Labels toggle has `role="switch"` and `aria-checked`
- [ ] Filters popover has `role="dialog"` and `aria-label="Filters"`
- [ ] Filter chip dismiss buttons have `aria-label="Remove [filter name] filter"`
- [ ] No duplicate IDs in the rendered DOM

---

## Assumptions

1. The app's main nav has height ≈ 56px. The `.viz-page` height calculation `calc(100vh - 56px)` must be adjusted to match the actual nav height if it differs.
2. `WarehouseVisualizationSearch.GetSuggestions` and `FindBestMatch` are existing static helpers — they accept `IReadOnlyList<VisualizationBinDto>` and a string query. The unified search reuses them as-is.
3. There is no existing JS click-outside handler. The backdrop `<div>` pattern is used instead of JS interop for simplicity.
4. The `_itemSearchQuery` field is kept internally for the `SearchItemAsync` fallback path. It is no longer bound to a visible input.
5. `OnZoneFilterChangedAsync(ChangeEventArgs)` and `OnStatusFilterChangedAsync(ChangeEventArgs)` are removed; their logic is inlined into `SetZoneFilterAsync` and `SetStatusFilterAsync`.
