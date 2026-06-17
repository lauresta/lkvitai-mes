# LKvitai.MES — Shopfloor Module · Smart Workflow Validation

**Module:** Shopfloor (ShopfloorPilot)
**Version:** 0.1 — validation rules + Validate UX contract
**Date:** 2026-06-17
**Status:** Draft — basis for the validator + its presentation
**Parent:** [`shopfloor-11-mvp-authoring-implementation-blueprint.md`](./shopfloor-11-mvp-authoring-implementation-blueprint.md) ·
[`shopfloor-10-mvp-authoring-scope.md`](./shopfloor-10-mvp-authoring-scope.md)

> The **Validate** button is the client's WOW moment: it turns the flow graph into an
> insight report — not a yes/no. It surfaces correctness **errors**, production-flow
> **warnings** (bottlenecks, imbalance, queues), and **hints**, and highlights them on the
> canvas. This doc defines the rules, the severity model, the Validate UX flow, and the
> result contract. The **visual presentation** is specced in the paired design guide
> (§7) — produced via the Claude Designer pass.

---

## 1. The Validate button & flow

- A **Validate** button lives in the editor toolbar, next to Save / Publish / Preview.
- **Anytime, non-destructive:** clicking Validate runs the full rule set **client-side**
  over the current canvas graph and opens the **validation report panel**. It never
  changes data.
- **Findings are clickable** → highlight their targets on the canvas (task node, edge,
  or the affected line/station).
- **Passive badge:** the button shows a live count badge (e.g. `2 ⛔ · 3 ⚠`) updated on
  edit, so the user knows there's something to look at without opening the panel.
- **Tiered enforcement** (consistent with blueprint §7):
  - `PUT …/graph` **save** — lenient structural checks only; **errors do NOT block saving a draft**.
  - `POST …/publish` — server re-runs the **full** rule set; **errors block publish** (422 + the report).
  - **Preview** runs the same full set as Validate (it’s the dry-run’s correctness gate).
- The **same engine** runs in the editor (client) and on the server (publish) — one rule
  catalog, identical results.

---

## 2. Severity model

| Severity | Meaning | Blocks publish? | Visual key |
|----------|---------|-----------------|------------|
| **Error** ⛔ | Flow is structurally invalid / unrunnable | **Yes** | danger (red) |
| **Warning** ⚠ | Flow runs, but has a production problem (queue, bottleneck, imbalance) | No | warn (sand/amber) |
| **Hint** 💡 | Hygiene / optimization suggestion | No | info (teal) |

Errors = the blueprint §7 "full" set. Warnings + Hints are the smart layer on top.

---

## 3. Rules catalog

All rules compute from **graph structure + `durationSec` + `workStationId` + `wip_limit`** —
everything available in the authoring MVP. No order/material/runtime data needed.

### 3.1 Errors ⛔ (block publish)

| ID | Rule | Detects |
|----|------|---------|
| **E1** | Single start | not exactly one `start` node |
| **E2** | Single finish | not exactly one `finish` node (parallel branches must converge to one) |
| **E3** | Unreachable task | a task with no path **from** start — it never begins |
| **E4** | Dead-end task | a task with no path **to** finish — it "just hangs" |
| **E5** | Cycle | a directed cycle — the flow can never complete |
| **E6** | Broken edge | edge endpoint missing, self-loop, or duplicate edge |
| **E7** | Incomplete task | task missing a line (`workStationId`) or `durationSec ≤ 0` |

### 3.2 Warnings ⚠ (smart insight — non-blocking)

| ID | Rule | Detects / why it matters |
|----|------|--------------------------|
| **W1** | Branch imbalance / starvation | At a merge, incoming paths differ a lot in total time → the fast branch waits, WIP piles or the next step starves. *Shows the wait, e.g. "branch B idles 2 h 58 m".* |
| **W2** | Critical path / lead time | Longest start→finish path = theoretical minimum lead time. Highlighted; its total is shown. |
| **W3** | Bottleneck line | Station with the highest aggregate task time = the throughput constraint (Theory of Constraints). |
| **W4** | False parallelism | Two "parallel" branches assigned to the **same** station can't truly run in parallel → hidden serialization; real time = sum, not max. |
| **W5** | WIP / CONWIP risk | More tasks routed through a station than its `wip_limit` → a guaranteed queue. |
| **W6** | Line ping-pong | Flow bounces line A→B→A→B → extra transport/changeovers; suggests grouping consecutive same-line tasks. |
| **W7** | Dominant task | One task is >X% of the critical path → the whole flow hinges on one operation. |
| **W8** | Line load imbalance | One line carries most of the total work while others idle → rebalance opportunity. |
| **W9** | Redundant dependency | Edge A→C exists although A→B→C already does → over-constrains scheduling freedom. |
| **W11** | Long single-line chain | Many sequential tasks on one station → that line is a serial bottleneck, candidate to parallelize. |

### 3.3 Hints 💡 (info)

| ID | Rule | Detects |
|----|------|---------|
| **H1** | Name hygiene | empty / placeholder ("Task", "NEW_TASK") / duplicate task names |
| **H2** | Duration outlier | `0` or absurd duration vs sibling tasks |
| **H3** | Bad station ref | task on an inactive station or a misconfigured work center |
| **H4** | No convergence | parallel branches each go straight to finish with no shared assembly step (maybe intended) |
| **H5** | Isolated cluster | a connected group attached to neither start nor finish |
| **H6** | Family anomaly | task count far from sibling families (e.g. "similar flows have ~6 tasks, this has 1") |

### 3.3.1 Implementation status (v0.2)

The engine `SmartWorkflowValidator` (Domain) implements: **E1–E7**, **W1, W3–W9, W11**,
**H1–H4**. W2 is surfaced as a metric (`metrics.criticalPath`), not a finding. Not yet
implemented: **W10** (runtime), **H5** (overlaps E3/E4), **H6** (needs cross-template
sibling data). Engine entry point: `SmartWorkflowValidator.Validate(graph, stations)`;
result mapped to `ValidationReportDto`. Runs on `POST …/validate`, `POST …/{id}/validate`,
and as the `POST …/{id}/publish` gate (422 + report when not publishable).

### 3.4 Backlog — not in the authoring validator

- **W10 · Changeover clustering** — real setup waste depends on **order** material/colour/size
  (the "by size and color" batching), which is **runtime** data, not the generic graph.
  Moves to the **[RUN]** slice. (There is no Task-Type catalog — `taskTypeCode` is just an
  optional label and drives nothing here.)
- Material availability, cost-of-changeover, takt vs cycle-time — need materials/formulas/orders.

---

## 4. Computed metrics (surfaced by Validate)

Beyond pass/fail, the report exposes numbers the client cares about:

- **Critical path** — node sequence + total duration (= min lead time).
- **Bottleneck** — the constraining station + its load.
- **Throughput estimate** — units/hour implied by the bottleneck (turns the graph into a number).
- **Per-line load** — total time + task count per station.
- **Branch imbalance** — wait time at each merge node.
- **Health score** — 0–100 rollup (errors weigh most, then warnings).

---

## 5. Validation result contract

One shape, produced by the engine, consumed by both the editor panel and (on publish) the API.

```jsonc
ValidationReport {
  score: 0..100,
  publishable: bool,                 // false if any Error
  summary: { errors, warnings, hints },
  metrics: {
    leadTimeSec,
    criticalPath: { nodeIds: [...], durationSec },
    bottleneck:   { stationId, stationName, loadSec },
    throughputPerHour,
    lineLoads: [ { stationId, stationName, loadSec, taskCount } ],
    merges:    [ { nodeId, maxInSec, minInSec, waitSec } ]
  },
  findings: [
    {
      ruleId: "W1",
      severity: "error|warning|hint",
      title, message,
      targets: { nodeIds: [...], edgeIds: [...], stationIds: [...] },
      detail?: { waitSec | percent | loadSec | ... }   // rule-specific
    }
  ]
}
```

`targets` is what the UI highlights when a finding is clicked.

---

## 6. Where each rule runs

| Trigger | Rules | Effect |
|---------|-------|--------|
| Editor **Validate** button | all (E + W + H) | opens report; non-blocking |
| Editor live badge | counts only | passive indicator |
| **Preview** (dry-run) | all | correctness gate before showing the plan |
| `PUT …/graph` save | lenient (E5, E6, JSON/kind/endpoints) | save WIP; never blocks |
| `POST …/publish` | full (all Errors) | **blocks** on any Error (422 + report) |

---

## 7. Presentation (visual guide)

This is the approved Designer pass. The buildable reference mockup is
`wwwroot/prototypes/shopfloor-validation-report-prototype.html` (standalone, inline
CSS/JS, no build) — it extends the editor prototype with the Validate button + badge,
the report drawer, node/edge/line highlighting, the branch-imbalance callout, and the
all-good state. **Reuse the editor tokens verbatim; add no new color families.**

> **Wired into the live editor (v0.3).** The Validate button, count badge, report
> drawer, node/edge highlights, and click-to-locate are now integrated directly in
> `wwwroot/prototypes/shopfloor-workflow-editor-prototype.html` (the iframe the Blazor
> `WorkflowEditor.razor` hosts). Validate posts the current graph over the existing
> `postMessage` bridge → `WorkflowEditor.OnValidateRequested` → `POST
> /api/shopfloor/workflows/validate` → the report is posted back and rendered. Any graph
> edit marks the report **stale** (highlights drop, badge greys) until re-validated.
> The branch-imbalance callout and over-WIP striping remain in the standalone mockup
> only (not yet ported to the live editor).

### 7.1 Severity → visual mapping (locked)

| Severity | Token family | Ring / fill | Icon | Where |
|----------|--------------|-------------|------|-------|
| **Error** ⛔ | `--danger-*` (red) | `--danger-strong` ring | ⛔ | node ring, finding row, badge `err` |
| **Warning** ⚠ | `--warn-* / --sand-*` (amber) | `--warn-strong` ring | ⚠ | node ring, finding row, badge `warn` |
| **Hint** 💡 | `--info-* / --accent-*` (teal) | `--info-strong` ring | 💡 | node ring, finding row, badge `hint` |
| **OK** ✓ | `--ok-* / green` | `--ok-dot` | ✓ | score state, all-good, clean badge |
| **Critical path** | `--accent-*` (teal) | `--accent-500` rail + thick teal edge | — | informational overlay (not a severity) |
| **Bottleneck** | `--n-800` (neutral-dark) | dark chip + dark load bar | ▣ | the one constraint marker — neutral so it never reads as "error" |

Critical-path and bottleneck are **informational**, not problems — that's why they map to
the teal accent and neutral-dark, never to red/amber. They show in the all-good state too.

### 7.2 Surfaces & where they live

1. **Validate button** — editor toolbar, next to Save / Publish. Carries a live
   **count badge** `2 ⛔ · 5 ⚠ · 1 💡` (mono, severity-tinted pills). Clean graph →
   the button goes green with `✓ healthy`. The badge updates on edit; clicking opens the
   report drawer (non-destructive).
2. **Report drawer** — right side, fixed `392px`, four stacked zones:
   - **Header** — score ring (0–100, color by band: ≥80 green, 50–79 amber, <50 red),
     publish-state chip (`Publish blocked` / `Ready to publish`), summary pills.
   - **Metrics** — `Lead time`, `Bottleneck` line, and a full-width **Throughput** tile
     (the one number that matters — accent-washed, big mono `~12 units/hr`).
   - **Per-line load strip** — one bar per station (load = width, mono value inside).
     Bottleneck bar is dark + `▣ BOTTLENECK`; over-WIP bar is sand-striped + `WIP 5/3`.
     Rows are clickable → locate that line's nodes.
   - **Findings** — grouped `Errors → Warnings → Hints`; each row = severity icon, rule
     id chip (`E4`), title, one-line message (ids/numbers in mono), and a locate button.
3. **Node highlights** (canvas) — ring color by precedence (below), corner severity chip
   top-right, `▣ BOTTLENECK` chip, and bottom-left flags `CRITICAL PATH` / `WIP n/limit`.
4. **Edge highlights** — critical path = thick teal solid + sparse flow; cycle = red
   dashed; redundant/over-constraining = thin amber dotted.
5. **Branch-imbalance callout** — a small card anchored left of the merge node: two
   mini-bars (Branch A teal vs Branch B sand) + `Branch B idles 2h 58m at this merge`.
6. **All-good state** — replaces the findings list: green check, "Flow is healthy", and
   the throughput number. Score ring 100, Publish enabled.

### 7.3 Node highlight precedence (overlaps coexist)

A node can be several things at once. Resolve as:

- **Ring color** = single highest severity present: `error > warning > hint > (critical-only)`.
  A critical-path node with a warning shows an **amber** ring (warning wins the ring).
- **Critical-path rail** (teal left stripe + `CRITICAL PATH` flag) renders **regardless**
  of ring color, so a critical+warning node shows amber ring **and** teal rail together.
- **Bottleneck chip** (`▣`, neutral-dark) renders **regardless** of everything else.
- **WIP flag** renders independently at bottom-left.
- Worked example (Assembly merge node): amber ring (W1+W3) + teal critical rail +
  `▣ BOTTLENECK` chip — all visible, instantly legible.

### 7.4 Click-to-locate interaction

Clicking a finding (row or its locate button) — or a line-load row:

1. Marks the finding **active** (teal left-border + `--accent-50` wash).
2. Resolves targets → node ids (expanding any `stationIds` to that line's nodes).
3. **Pans + zooms** the canvas to fit the targets (animated `~520ms`, capped ≤115%).
4. **Pulses** the target nodes (scale 1→1.045 ×2) and target edges (stroke 3→6 ×2).

Edges are re-rendered after the pan settles so socket-anchored beziers stay accurate.

### 7.5 Tiered enforcement reflected in the UI

- Save (`PUT …/graph`) never blocks — badge still shows counts; drawer stays advisory.
- Publish is **disabled while any Error exists**; the header reads `Publish blocked · N
  errors must be fixed`. With zero errors it flips to `Ready to publish` and enables.
- Warnings + Hints never gate Publish — they are insight, not a wall.

---

## 8. Cross-references

- Rules origin & MVP scope: [`shopfloor-10`](./shopfloor-10-mvp-authoring-scope.md),
  [`shopfloor-11`](./shopfloor-11-mvp-authoring-implementation-blueprint.md) §7
- Editor bridge (where Validate lives): `shopfloor-11` §9
- Editor prototype to extend: `wwwroot/prototypes/shopfloor-workflow-editor-prototype.html`
- **Validation report mockup (this §7):** `wwwroot/prototypes/shopfloor-validation-report-prototype.html`
