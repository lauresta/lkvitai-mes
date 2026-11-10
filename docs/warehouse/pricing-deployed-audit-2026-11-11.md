# Warehouse price audit on test — 2026-11-11

The deployed audit ran against `cikada-test.lauresta.com` in EN and LT. Expected
values were generated at run time by the SELECT-only query in
`tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/Ui/PriceAuditExpected.sql`
through `docker exec lkvitai-mes-db-test psql`. No database writes or new test
records were made. Screenshots, rendered page text, the DB snapshot and logs are
local artifacts under `/tmp/warehouse-price-audit`; they are not committed.

## Result

- Unit tests: **783 green**. New cases cover Dashboard overview/category/warehouse
  aggregation, incomplete prices, negative rows, case-insensitive SKU, null CSV
  money cells, and negative/large amount formatting.
- Deployed route sweep: **red**. 37 routes configured, 35 visited in each culture.
  Cycle-count discrepancy and execution routes were skipped because the test DB
  has no cycle-count ID; no record was created to fill this gap.
- The final throttled sweep had no 429 responses or browser console errors.
  WebUI server logs contained three API request errors; API logs contained no
  ERROR/exception lines. All three WebUI errors came from Sales allocations.
- Sales order amount checks passed on the existing order, outbound order and
  packing screens: 1 × 25.00 = 25.00 on each. The unsaved create form picked up
  BasePrice 15.00 and the matching ItemPriceHint for SKU `0000001`; changing to
  another customer kept that price. An item with no BasePrice left the input
  empty. The test did not submit a new order.
- Agnum latest-import rows passed for SND 493 (`1422073200`, qty 274140,
  cost value 3289680.00, sale value 27139860.00) and SND 496
  (`1422073200`, qty 480, 5760.00 / 47520.00; `0000001`, qty 4,
  40.00 / 60.00). The test checks one row per SND/SKU and waits for the price map.
- Physical Available Stock and Stock Level prices matched the DB: SKU
  `1422073200` qty 30 and 21 at cost 12.00, sale 99.00, values 360.00 and
  252.00; SKU `000-000-101` qty 3 at WAC 10.00, no sale price, value 30.00.
  Missing prices displayed `–`, including `000-000-102` and `1514251720`.
  Movement screens label their value as current cost in both languages.

## Red defects

1. **Virtual stock filter.** Available Stock and Stock Level show negative
   `AGNUM` rows even with “Include virtual” off. Physical screens therefore do
   not use a consistent virtual/negative-row policy. Open PR #313 addresses
   Available Stock only and is not sufficient for the whole cross-screen rule.
2. **Valuation quantity.** `000-000-101` shows qty 1 and value 10.00 in
   Valuation, while the stock projection and DB show qty 3 and value 30.00.
   The assertion deliberately expects the DB value; it was not adjusted to
   make the test green.
3. **Sales allocations.** WebUI requests `status=PENDING_APPROVAL`; the API
   parses the enum name `PendingApproval` and returns HTTP 400. The error is
   visible in EN/LT and appears three times in the WebUI log.

## Coverage limits and owner decisions

- Dashboard has 4 SKUs and positive-row quantity 483. Its cost and sale totals
  display `–` because some positive rows have no prices. Thus numerical
  Dashboard/category/warehouse total and 100% percentage reconciliation cannot
  be completed on this data set. **Owner decision:** keep the current
  all-or-nothing total rule or use the blueprint's sum of priced rows.
- **Owner decision:** LT money formatting (current UI uses invariant decimal
  point in most money cells). No formatting policy was changed here.
- **Owner decision:** access policy for Valuation. No permission change was made.
- **Owner decision:** server validation of sales-order prices. The API currently
  accepts zero and negative values. No validation policy was changed.
- The test DB has no customer price groups or item overrides, so switching to a
  group price could not be exercised without creating records. The existing QA
  inbound shipment `ISH-5` is complete; its receipt already yielded WAC 10.00.
  A new receipt was not made, so the before/after WAC transition was not
  repeated in this audit. Empty invoice price renders `–` on the create screen.
- Routes with no priced rows were checked for loading, errors and visible
  columns, but no price arithmetic can be asserted on absent rows. The two
  cycle-count routes were not visited.

## Test mechanics

The runner `run_deployed_price_audit.py` refreshes expected JSON immediately
before Playwright, checks 35 deployed routes per culture, waits for the data
grid or loading indicator, detects visible Blazor errors, error alerts/toasts,
console errors and `/portal` redirects, then checks `docker logs --since` for
both Warehouse containers. It respects the API's 100-request UTC-minute window
between batches. Any browser or server ERROR/exception makes the run red.
