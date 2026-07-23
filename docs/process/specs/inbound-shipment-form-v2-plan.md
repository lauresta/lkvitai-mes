# Inbound Shipment form v2 — MudBlazor rebuild + service/cost line classification

**Status:** PLANNING COMPLETE — IMPLEMENTATION NOT STARTED.
**Owner:** whichever AI agent is currently assigned this repo/task.

## Read this first (handoff contract)

This document is a **self-contained execution plan**, written so that any AI
agent — Claude, a different model, a fresh session with zero prior context —
can pick it up cold and continue without asking clarifying questions. All the
design decisions below were already discussed and approved by the repo owner
in a prior conversation. Do not re-litigate them; if something genuinely
blocks progress that isn't resolved here, say so explicitly and stop, but
don't second-guess decisions already made.

**If you are a human handing this to a fresh AI agent**, paste this as your
instruction:

> Read `docs/process/specs/inbound-shipment-form-v2-plan.md` in full. Pick up
> implementation at the first unchecked step in the "Execution steps"
> checklist and continue in order, checking off each step in this file as you
> complete it (edit the checkbox directly, commit the doc update alongside
> your code changes). Don't ask me clarifying questions about scope or
> design — everything decided is written down. If you hit something
> genuinely ambiguous that isn't covered, stop and ask; otherwise keep going
> autonomously through the whole checklist, including opening the PR at the
> end.

**Progress tracking convention:** every time a step below is completed,
change its `[ ]` to `[x]` in this file and commit that change together with
the corresponding code change (same commit or the next one). If you stop
mid-task (rate limit, cooldown, context exhaustion), the checklist state IS
the handoff state — the next agent reads it and resumes at the first
unchecked box.

## Background (why this exists)

Two prior PRs on this repo, in order:

- **PR #205** — redesigned `/dashboard` (warehouse module) from a
  technical/ops page into a business dashboard (stock value, low stock,
  expiring lots, Agnum health, etc.).
- **PR #206** — closed the gap where inbound shipment receiving captured no
  price at all, so stock valuation was always 0. Added `UnitPrice`/`Currency`
  to `InboundShipmentLine`, and four fixed header columns on
  `InboundShipment` (`FreightCost`, `DutyCost`, `InsuranceCost`,
  `OtherCost`) for landed-cost allocation at receiving time. Receiving now
  auto-initializes/adjusts `ItemValuation` via weighted average.

This plan is **v2** of the inbound shipment form, replacing/extending what
PR #206 shipped, based on follow-up product feedback:

1. The create-shipment page (`InboundShipmentCreate.razor`) is a plain
   Bootstrap form with `<select>` dropdowns — needs to become a proper
   MudBlazor form (autocomplete pickers, data grids), matching where the
   rest of the app is heading (see
   `docs/process/specs/mudblazor-migration-plan.md` and the discovery notes
   referenced below).
2. The four fixed cost columns (`FreightCost`/`DutyCost`/`InsuranceCost`/
   `OtherCost`) don't support arbitrary custom cost types (e.g. VAT, or a
   type the business invents later) — needs to become a proper table.
3. Real invoices sometimes list freight/handling/delivery **as their own
   invoice line** (e.g. "Transportas — 1 pcs — 100 EUR") alongside real
   goods, rather than as a separate document. If a user enters that line
   into the shipment's item Lines the naive way, it's double-wrong: it
   pollutes physical stock quantity/valuation with a fake "1 unit of
   transport" SKU, AND the 100 EUR never gets distributed across the real
   goods' actual cost. This needs first-class handling, not a training note.

### Prior art worth knowing about (do not merge, reference only)

`origin/freeze/mudblazor-migration-discovery` is a stale, heavily diverged
branch (many unrelated file deletions — do not diff/merge it wholesale). The
one genuinely useful thing on it:
`docs/process/notes/mudblazor-migration-discovery.md` (750 lines) — a survey
of the app's MudBlazor adoption state as of 2026-03. Useful facts pulled from
it, already verified true on `main` as of this writing:

- MudBlazor is already installed and wired (`Program.cs`, `App.razor`
  providers, `_Layout.cshtml` assets). No new package/setup work needed.
- Two pages already use real Mud grid patterns as reference:
  `/warehouse/admin/lots` (`Pages/Admin/Lots.razor`) and `/available-stock`
  (`Pages/AvailableStock.razor`).
- `Pages/AdminItemEditorDialog.razor` already has a working
  `MudAutocomplete<string>` (used for tags) — good style reference for
  `Variant.Outlined` / `Margin.Dense` conventions used elsewhere in this app.
- Shared wrappers (`ToastService`, `ErrorBanner`, `LoadingSpinner`,
  `ConfirmDialog`, `Pagination`, `DataTable`) are used across 70+ pages each.
  **Do not touch/replace these globally** — that's a separate, much bigger
  app-wide migration effort tracked elsewhere. This plan only touches the
  inbound shipment pages; keep using `ToastService`/`ErrorBanner` as-is for
  error/success feedback, just build the form layout itself in Mud
  components.

## Decisions made (final — implement as specified, don't redesign)

### 1. Additional costs become a real child table, not fixed columns

Replace `InboundShipment.FreightCost/DutyCost/InsuranceCost/OtherCost`
(added in PR #206) with a new entity:

```csharp
public sealed class InboundShipmentAdditionalCost
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public string CostType { get; set; } = string.Empty; // "Freight" | "Duty" | "Insurance" | "VAT" | "Other" | user-defined
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    public InboundShipment? Shipment { get; set; }
}
```

`CostType` is a free-text string (not a hard enum) so users can add their own
categories beyond the four seeded defaults (Freight, Duty, Insurance, VAT).
Seed those four rows (Amount = 0) when the create form loads, matching what
the earlier UI mock already showed; users can add more rows or leave the
seeded ones at 0 (0-amount rows should not be persisted, or should be
filtered out silently — don't send noise to the API).

### 2. Item master data gets a Stock/Service classification

Add to `Item` (`Domain/Entities/MasterDataEntities.cs`):

```csharp
public enum ItemType { Stock = 0, Service = 1 }

// on Item:
public ItemType ItemType { get; set; } = ItemType.Stock;
public string? CostType { get; set; } // only meaningful when ItemType == Service; same value space as InboundShipmentAdditionalCost.CostType (Freight/Duty/Insurance/VAT/Other/custom)
```

This is deliberately **not** an enum for `CostType` on either entity — keep
it a plain string both places so the two line up without a shared enum
dependency headache across Domain/Contracts/WebUI, and so users can invent
new categories without a code change/migration.

Rationale (already agreed with repo owner): item names for freight/delivery
services vary by language and supplier ("Transportas", "Pristatymas",
"Delivery", "Shipping fee" ...). The system must not try to guess from the
item name. `CostType` is an explicit field set once when the service item is
created in the catalog (Admin → Items), and every future invoice that uses
that item automatically maps to the right cost bucket regardless of what the
line is literally called.

### 3. How a Service-type line behaves inside a shipment

When a shipment line's `Item.ItemType == Service`:

- It still appears as a normal line in the Lines grid and contributes to
  "Total Invoice Value" — so the on-screen total matches the real supplier
  invoice total (accounting parity).
- It is **excluded** from "Total Qty" (physical) and from the physical
  receiving flow entirely — there is nothing to put in a location or a lot.
  Recommended implementation: such lines are auto-marked
  `ReceivedQty = ExpectedQty` at shipment **creation** time (not via
  `ReceiveGoodsAsync`), since there's no physical receipt to perform. Do not
  route them through the lot-tracking/QC/location logic in
  `ReceivingController.ReceiveGoodsAsync` at all.
- Its line total (`Qty × UnitPrice`) automatically folds into the Additional
  Costs allocation pool, bucketed by the item's `CostType` (default to
  `"Other"` if the service item has no `CostType` set, so the money is never
  silently dropped from the total).
- The manual Additional Costs table (decision #1) still exists independently
  for costs that are **not** a line on this specific supplier invoice (e.g.
  a separate freight-forwarder invoice, customs broker fee billed
  separately). Both sources — service lines + manual rows — feed the same
  allocation pool. There is no hard duplicate-prevention; add a **soft**
  warning in the UI if a manual row's `CostType` matches a Service line's
  `CostType` on the same shipment (likely-but-not-certainly a double entry),
  but do not block submission on it.

### 4. Invoice price vs. actual (landed) price, computed live

Rename the existing per-line "Unit Price" field/label to **"Invoice unit
price"**. Add a second, read-only column **"Actual unit price"**:

```
actualUnitPrice = invoiceUnitPrice + (line's proportional share of total additional-cost pool) / expectedQty
```

Allocation basis: proportional by line value (`ExpectedQty × InvoiceUnitPrice`)
relative to the sum of all Stock-type lines' values — this is the same
formula already implemented server-side in `ReceivingController.
UpdateItemValuationForReceiptAsync` (PR #206). Only **Stock**-type lines
participate in this denominator and receive an allocated share — Service
lines don't get an "actual unit price" of their own (they have none — they
are the cost, not a cost-bearing item).

This must be computed **live in the browser** as the user edits the
Additional Costs table or the invoice unit prices, so the create form shows
a correct preview before submission — don't wait for a server round-trip.
Duplicate the allocation math client-side in the Blazor component (small
enough to not be worth a shared library right now).

The server-side calculation in `ReceivingController` (PR #206) computes the
authoritative landed cost at **receiving** time, not creation time — that
logic needs updating too (see Execution step 4) to read from the new
`InboundShipmentAdditionalCost` table plus Service-type sibling lines,
instead of the four fixed columns it reads today.

### 5. Totals footer

Add a totals row/bar under the Lines grid showing at least: Total Qty
(Stock lines only), Total Invoice Value (all lines), Total Additional Costs
(manual rows + Service line totals), Total Actual (landed) Value, and the
delta between average invoice unit price and average actual unit price. Exact
layout is an implementation detail — follow existing dashboard/panel visual
conventions (`docs/ux/lkvitai-mes-ux-handoff.html`, `AGENTS.md`) rather than
inventing new styling.

### 6. Whole form → MudBlazor

Not just the additional-costs table — the **entire** create form, including
the Lines grid, moves to MudBlazor components:

- Item picker and Supplier picker → `MudAutocomplete<T>`, backed by the
  existing `MasterDataAdminClient.GetItemsAsync(search, ...)` /
  `GetSuppliersAsync(search, ...)` (both already support server-side text
  search — do not add new endpoints for this).
- Lines grid → `MudDataGrid` or `MudTable` (match whichever pattern
  `Pages/Admin/Lots.razor` or `Pages/AvailableStock.razor` already uses, for
  consistency — check both before picking).
- Additional Costs grid → `MudTable` with inline-editable Amount cells (Type
  as free text or a small preset dropdown + "custom" escape hatch).
- Keep `ToastService`/`ErrorBanner` for feedback (do not touch those shared
  components — out of scope, see "Prior art" section above).
- Follow `Variant.Outlined` + `Margin.Dense` conventions already established
  in `AdminItemEditorDialog.razor`.

`InboundShipmentDetail.razor` (the receive-goods page) is **not required**
to be rebuilt in this pass — PR #206 already added price-gating logic there.
If time allows, apply the same MudBlazor treatment for consistency, but
treat it as a stretch goal, not a blocker for calling this plan done. Do
update it functionally wherever the backend shape changes make it necessary
to keep compiling/working correctly (e.g. if `InboundShipmentDetailDto`
fields change).

## Explicit non-goals (do not do these; someone would have to ask first)

- No changes to the Agnum export pipeline. Deferred by the repo owner to a
  separate, API-based task — see PR #206 conversation.
- No allocation-by-weight or other alternate allocation bases (only
  proportional-by-value, as in PR #206). `Item.Weight`/`Volume` exist and
  could support this later, but nobody asked for it yet.
- No explicit "freight already included in invoice price" checkbox/flag —
  handled implicitly by simply not adding a cost row/Service line for it.
- No app-wide Bootstrap → MudBlazor migration. Only the inbound shipment
  create page (and optionally detail page) in this plan.
- No hard duplicate-prevention between manual cost rows and Service lines —
  soft warning only (see decision #3).

## Known gotchas from prior work (avoid repeating these mistakes)

These are real mistakes made and fixed during PR #205/#206 — avoid them
proactively this time:

1. **No .NET SDK is available in the agent sandbox.** You cannot run
   `dotnet build`, `dotnet ef migrations add`, or `dotnet test` locally. All
   compile-correctness has to come from careful manual review (brace/paren
   balance, matching positional-record parameter order, etc.) — verify with
   CI after pushing, expect at least one round of CI-driven fixes, and treat
   that as normal, not a failure.
2. **EF Core migrations must be hand-authored.** Follow the exact pattern
   used in
   `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/Persistence/Migrations/20260721090000_AddInboundShipmentPricingFields.cs`
   (+ its `.Designer.cs`) as a template: copy the current
   `WarehouseDbContextModelSnapshot.cs` verbatim as the new migration's
   `.Designer.cs`, adjust only the wrapper (`using
   Microsoft.EntityFrameworkCore.Migrations;`, `[Migration("...")]` attribute,
   class name, `BuildModel` → `BuildTargetModel`), then apply the *same*
   property-block edits to both the new Designer.cs and the real
   `WarehouseDbContextModelSnapshot.cs` so they stay identical except for
   that wrapper. Diff the two files after editing to confirm they only
   differ in the expected wrapper lines.
3. **Razor `::deep` misuse.** Don't prefix every rule in a `.razor.css` file
   with `::deep` "just in case" — it's only for reaching into *child
   component* markup. Bare `.class { }` rules (no `::deep`) are correctly
   scoped by Blazor automatically for markup rendered directly by that
   component. (This caused a real production bug in PR #205 — the KPI grid
   silently failed to apply `display: grid`.)
4. **C# switch expressions with a leading relational pattern
   (`< 0 => ...`) inside a `.razor` file's `@code` block can break the Razor
   tokenizer** — it misreads the leading `<` as an HTML tag start and
   corrupts parsing of the entire file. This is fine in plain `.cs` files
   (e.g. controllers), just avoid it inside `.razor` `@code` blocks — use an
   if/else chain instead.
5. **MediatR 12.2.0's `ISender` interface has *three* `Send` overloads**,
   not two: `Send<TResponse>(IRequest<TResponse>, ct)`,
   `Send<TRequest>(TRequest, ct) where TRequest : IRequest` (no-response
   commands), and `Send(object, ct)`. Any hand-written `IMediator` test stub
   needs all three (see `NoOpMediator` in
   `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/ReceivingWorkflowIntegrationTests.cs`
   for a working example) plus `CreateStream` (x2) and `Publish` (x2).
6. **Branch protection on `main` requires PRs.** Direct pushes to `main`
   technically succeed (the repo owner's token can bypass the rule) but
   this was flagged as a process mistake earlier in this project. Always
   work on a feature branch and open a PR — don't push straight to `main`
   even if it's technically possible.
7. Positional C# records (used heavily in `ReceivingController` for request/
   response DTOs) break every existing call site when you add a parameter —
   grep the whole repo (`src/` and `tests/`) for every constructor call site
   of anything you change the shape of, not just the obvious ones.

## Execution steps

Work through in order. Check each box when done, commit the checkbox update
with the corresponding code.

- [ ] **Step 0 — Orientation.** Read this file fully. Skim the diffs of PR
      #205 and PR #206 (`git log --oneline main`, then `git show <merge
      commit>`) to see the actual shape of what exists today before editing.
- [ ] **Step 1 — Domain: Item classification.** Add `ItemType` enum
      (`Stock`/`Service`) and `CostType` (nullable string) to `Item` in
      `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/Entities/MasterDataEntities.cs`.
- [ ] **Step 2 — Domain: additional-cost table.** Remove the four fixed cost
      columns from `InboundShipment`; add the `InboundShipmentAdditionalCost`
      entity (same file) with a collection nav property on `InboundShipment`.
- [ ] **Step 3 — Infrastructure/EF.** Update `WarehouseDbContext.cs` fluent
      config for both entity changes (new columns on `Items` table, drop the
      four columns from `inbound_shipments`, new `inbound_shipment_additional_costs`
      table with FK to `inbound_shipments`). Hand-author the migration +
      Designer.cs + ModelSnapshot.cs update per gotcha #2 above.
- [ ] **Step 4 — Api: ReceivingController.** Update
      `CreateInboundShipmentRequest`/`CreateInboundShipmentLineRequest` DTOs:
      drop the four cost fields, add `AdditionalCosts: List<{CostType,
      Amount, Currency}>`. In `CreateShipmentAsync`, persist the cost rows;
      auto-complete (`ReceivedQty = ExpectedQty`) any line whose `Item.ItemType
      == Service` immediately, skipping lot/location logic for those lines.
      In `ReceiveGoodsAsync`/`UpdateItemValuationForReceiptAsync`, change the
      additional-cost pool computation to sum
      `InboundShipmentAdditionalCost` rows **plus** the shipment's Service-type
      lines (grouped by the *item's* `CostType`, defaulting to `"Other"`),
      instead of reading the four old fixed fields.
- [ ] **Step 5 — Api: Item admin endpoints.** Update
      `ItemsController.cs` (create/update item) and its request/response
      DTOs to accept/return `ItemType`/`CostType`.
- [ ] **Step 6 — WebUI models/clients.** Mirror all the above DTO shape
      changes in `WebUI/Models/InboundDtos.cs`, `WebUI/Models/MasterDataDtos.cs`
      (or wherever `AdminItemDto`/`CreateOrUpdateItemRequest` live), and the
      corresponding methods on `ReceivingClient.cs` /
      `MasterDataAdminClient.cs`.
- [ ] **Step 7 — WebUI: item editor.** Add `ItemType` (dropdown: Stock/
      Service) and `CostType` (dropdown, only visible/relevant when
      ItemType = Service; allow free text for custom types) fields to
      `Pages/AdminItemEditorDialog.razor`.
- [ ] **Step 8 — WebUI: rebuild InboundShipmentCreate.razor.** Full
      MudBlazor rebuild per decisions #1–#6 above: autocomplete pickers,
      Mud grid for Lines (Invoice unit price + read-only live-computed
      Actual unit price columns), Mud table for Additional Costs (4 seeded
      rows + add-custom-row), totals footer, soft duplicate-type warning.
- [ ] **Step 9 (stretch, optional) — WebUI: InboundShipmentDetail.razor.**
      Apply the same MudBlazor treatment for consistency. Not required to
      consider this plan complete, but keep the page functionally correct
      against any DTO shape changes from Step 6 regardless.
- [ ] **Step 10 — Tests.** Update
      `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/ReceivingWorkflowIntegrationTests.cs`
      for the new request/response shapes (same kind of fixes as PR #206's
      second commit). Add at least one test covering: a shipment with one
      Stock line + one Service line (e.g. "Transport") correctly excludes
      the Service line from received quantity and folds its amount into the
      Stock line's landed cost.
- [ ] **Step 11 — Manual review pass.** For every file touched: verify
      brace/paren balance (`python3 -c "..."` count trick used in PR #205/
      #206), verify every positional-record call site across `src/` and
      `tests/` was updated, re-read the diff end to end once before
      committing.
- [ ] **Step 12 — Ship it.** Create a new feature branch (do not commit to
      `main` directly — see gotcha #6), commit, push, open a PR against
      `main` (use `mcp__github__create_pull_request`), subscribe to PR
      activity (`mcp__github__subscribe_pr_activity`), and work through CI
      feedback until green — expect 1–2 rounds of fixes based on prior
      history, that's normal.
- [ ] **Step 13 — Close out.** Once merged, update this file's status line
      at the top to `DONE — merged in PR #<number>` and check off any
      remaining boxes.

## Open questions to flag to the repo owner if they come up

These were intentionally left as judgment calls for whoever implements —
raise them if they become blocking, don't just guess silently:

- Whether `CostType` should eventually be a real lookup/reference table
  (like `ReasonCode`) instead of a free string, if the list of categories
  grows large or needs per-tenant configuration. Not needed for this pass.
- Whether Service-type items need their own `RequiresLotTracking`/`RequiresQC`
  guard rails (they shouldn't ever be true for a Service item — consider
  adding a validation rule when Step 5/7 land, but confirm before adding new
  validation the owner didn't ask for).
