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

How findings are shown — node/line highlighting, the report panel, badges, critical-path
overlay, bottleneck badge, score/throughput display, click-to-locate, empty/all-good
state — is defined by the **paired design guide** produced via the Claude Designer pass.
That guide pastes in here as §7 once approved. Design constraints: reuse the existing
editor prototype tokens (Cikada.MES teal; danger=red, warn=sand, ok=green, info=teal);
no new color families; WOW but enterprise-clean.

---

## 8. Cross-references

- Rules origin & MVP scope: [`shopfloor-10`](./shopfloor-10-mvp-authoring-scope.md),
  [`shopfloor-11`](./shopfloor-11-mvp-authoring-implementation-blueprint.md) §7
- Editor bridge (where Validate lives): `shopfloor-11` §9
- Prototype to extend: `wwwroot/prototypes/shopfloor-workflow-editor-prototype.html`
