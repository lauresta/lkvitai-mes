# S2-012: completion-date calculation proposal

**NOT APPLIED. Review and validation only, 2026-11-01.**

The proposed replacement is [s2-012-completion-dates-proposal.sql](s2-012-completion-dates-proposal.sql). It replaces only the identical `#R` statement in `dbo.sproc_GenerateSalaryReportData` and `dbo.sproc_GenerateTurnOverData`. No stored procedure, job, schedule, index, server setting, or business row was changed for this review. The original procedures were not executed to benchmark them.

## Problem and causal limit

The existing statement repeats an order-wide cumulative production-event sum for each product/event timestamp. Its cached plan contains an inner production-document/detail scan beneath correlated nested loops and a lazy index spool, with about 360,781 estimated inner re-executions. It is not a missing-join Cartesian product.

The cached last execution of the turnover `#R` statement took 987.252 seconds, used 10,982.952 CPU seconds and 2,632,926,182 logical reads, and returned 24,729 rows. The preceding salary procedure has the same normalized `#R` SQL and similar resource use. Together they account for almost all of the 32m54s midnight report job. These are existing cached execution statistics, not new executions during investigation.

Historical `system_health` evidence places that job's turnover statement on SQL Agent SPID 54 during the Sales timeout window, with SQL CPU at 99%. This establishes a substantial overlapping workload. The precise Sales SPID and its wait were not captured; this evidence does not prove that CPU was the sole cause of every Sales timeout or exclude locking.

## Preserved behavior

The proposal first groups relevant event deltas by **order and exact stored timestamp**, then calculates the running balance, compares it with the count of current manufactured product rows, and chooses the earliest equal timestamp.

- Status `2` and `1173102376` add one; `1173102377` subtracts one.
- Every event at the same timestamp contributes before equality is tested. A window over individual events could incorrectly find equality between timestamp peers.
- The calculation stays order-wide. The original `O2.BlindID = O2.BlindID` is a tautology for the non-null product key. Changing it to a correlation with the outer product would change the report's business result.
- The required count is `COUNT(*)` of currently manufactured rows, including rows with null barcodes or no matching events. It is neither the sum of quantity nor the count of released products.
- Null timestamps cannot contribute to the original `<=` comparison and cannot provide a minimum completion date. They are excluded explicitly. Orders with no relevant event remain absent.
- The first equality must occur at a relevant status event: the required count is positive, and irrelevant events do not change the balance. Excluding irrelevant events from the candidate timeline therefore preserves the earliest date.
- Order/series joins, order-number formatting, the existing `#P` payment-date calculation, and the final maximum of payment/release dates are preserved. Payroll duty and employee projections remain untouched.

This intentionally preserves historical behavior based on the **current** manufactured product set. It does not redesign payroll rules or repair the tautology.

## Plan gate and validation

Before executing the candidate, its estimated plan was obtained with `SHOWPLAN_XML`; it showed bounded scans of approximately 113,059 product rows, 370,748 document-detail rows and 264,920 document rows. There was one document scan and one detail scan, without the old repeated correlated scans. All operators were serial under `MAXDOP 1`.

| Check | Result |
|---|---:|
| Existing cached `#R` estimated cost | 794,565 |
| Candidate estimated cost | 37.8139 |
| Candidate serial desired memory, estimated | 25,800 KB |
| Candidate operators | 35 |
| Candidate maximum estimated rebinds | 0 |
| Candidate `#R` rows / distinct orders | 24,729 / 24,729 |
| Candidate single-run client elapsed time | 964.363 ms |

After the plan review, the candidate was executed once into a connection-local `#R`, using a 10-second command timeout, `LOCK_TIMEOUT 3000`, `DEADLOCK_PRIORITY LOW`, and `MAXDOP 1`. The connection was closed afterward. No persistent report table was recreated.

The original inexpensive `#P` SELECT was separately estimated (cost 3.92506) before temporary-table execution. The candidate finishing dates were then compared with `Ataskaitos.dbo.SalaryReportData`; that comparison was also separately estimated (cost 4.53154) before execution. Only aggregate validation results were returned.

| Date comparison | Orders |
|---|---:|
| Candidate finishing dates | 23,862 |
| Persisted salary report, distinct orders | 23,858 |
| Shared orders with exactly equal dates | 23,858 |
| Shared orders with different dates | 0 |
| Persisted orders absent from candidate | 0 |
| Persisted orders with mixed finishing dates | 0 |
| Candidate orders absent from persisted salary projection | 4 |

The candidate `#R` and final finishing-date row counts also match the existing cached original counts: 24,729 and 23,862 respectively. The salary report has downstream duty, employee and cost joins; comparing its dates provides full agreement for the 23,858 shared orders, not a claim that its entire employee/cost projection has been reimplemented. The four candidate-only orders were not classified as errors or modified.

A separate deterministic, in-memory test compared the original order/timestamp arithmetic with grouped-window arithmetic on 10,000 generated histories. All matched, including timestamp ties, null times, irrelevant statuses, repeated events, non-manufactured products and missing events. Counterexamples verify why the per-product predicate change and an ungrouped event window are unsafe.

The single warm-cache candidate run and historical parallel job measurements are different conditions. The observed timings are not a controlled speedup benchmark, an SLA, or proof of future concurrency behavior. The estimated final output cardinality is also poor (about one row versus 24,729 actual), despite the bounded base scans; deployment review should retain this fact.

## Review before applying

The owner/DBA should review the exact SQL and payroll date equivalence before any production change. A future approved change should preserve both callers' result contracts, compare output on a stable representative database copy, and measure runtime, logical reads, grants, tempdb use and concurrent order-entry latency. Changing the schedule, removing a seemingly unused turnover result, sharing materialized results between callers, adding indexes, or changing server parallelism are separate decisions. None is included or applied here.

Private evidence is retained with restricted file permissions under `/tmp/sales-m2-evidence/s2012/`: `proposal-estimated-plan.xml`, `proposal-estimated-plan-summary.json`, `proposal-validation.json`, the two comparison estimated plans, cached original plan/statistics, current source definitions, and `event-algorithm-equivalence.json`. Raw payroll names or amounts are not included in this repository document.
