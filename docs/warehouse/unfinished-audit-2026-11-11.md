# Warehouse: unfinished functionality audit (2026-11-11, main 24923cce)

Source: 2 read-only Sonnet audits (backend, WebUI) + spot checks by Opus.
Paths relative to src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api unless noted.

## A. Looks done, stock does not change (P1)
1. Cycle-count approve/apply adjustment — CycleCountsController.cs:263-347 (publish :323), CycleCountServices.cs:395. StockAdjustedEvent only to MassTransit, no consumer, not appended to Marten → projections unchanged. Working pattern: AdjustmentsController.cs:125-152 (append "stock-adjustment:{id}"). [verified]
2. Dispatch — ShipmentCommandHandlers.cs:60-165: EF status + ShipmentDispatchedEvent on bus; nothing drains SHIPPING. Pattern: TransferServices.AppendStockMovementAsync (TransferServices.cs:580-640). [verified]
3. Pack — OutboundOrderCommandHandlers.cs:215-250: fake StockMoved PICKING_STAGING→SHIPPING on bus only (pick already moved to SHIPPING); packing HU created without lines (:183).
4. Putaway — PutawayController.cs:126-157: raw StockMovedEvent append, no expected-version, no IBalanceGuardLockFactory → concurrent overdraw of RECEIVING.
5. Pick complete (PickingController.cs:152-345), Receiving (ReceivingController.cs:560-600), QC (QCController.cs:212): Marten append + EF save in separate commits; stock via operational events projected directly (not StockLedger). Receiving valuation swallowed exceptions :586-598. → tech debt, keep pattern, fix only atomicity where cheap.

## B. In-memory only (singleton AdvancedWarehouseStore, Program.cs:273) — lost on restart, differs per replica
6. Waves /waves — AdvancedWarehouseStore.cs:57-173; pick list FABRICATED from orderId.GetHashCode() (:528-551) [verified]; UI WebUI/Pages/WavePicking.razor:146-178. Needs table + lines from OutboundOrderLine + AvailableStock; completion via PickTask path (PickingController.cs:152).
7. RMA /rmas — AWS.cs:300-370; receive/inspect no stock. Needs RMA tables; stock: receive→RETURNS/QC_HOLD, restock→pickable, scrap→SCRAP. UI Rmas.razor:161-181.
8. Cross-dock /cross-dock — AWS.cs:175-226; no link/reservation. Needs table; reserve via AllocateReservationCommand.
9. Serials /admin/serial-numbers — AWS.cs:456-518; durable SerialNumbers DbSet exists (WarehouseDbContext.cs:102, :1606) unused; needs history/location columns.
10. QC checklist templates + defects + attachments — AWS.cs:228-298; attachments to local disk uploads/qc (controller :218-248). Needs tables; blobs via IObjectStorageService (Minio, Program.cs:107-125). No UI creates defects.
11. HU split/merge/hierarchy — AWS.cs:373-455; GUIDs only; HandlingUnitSplit/Merged events TODO (Contracts/Events/HandlingUnitEvents.cs:55-79); Domain/Aggregates/HandlingUnit.cs:11 placeholder. No UI page found.
12. Label print queue /labels — InMemoryLabelPrintQueueStore (Program.cs:265, LabelPrintQueueServices.cs:34-72); printer default 127.0.0.1:9100.
13. Agnum reconciliation report store (CSV upload, POST/GET agnum/reconcile) — InMemoryAgnumReconciliationReportStore; no page uses it (page uses agnum/reconciliation, DB-backed).
14. Analytics /analytics/quality — defect counts from in-memory store (always 0); rootCause guessed from status (AdvancedWarehouseController.cs:510-560).
OK/ignore: InMemoryLotTraceStore (cache of DB-computed report, but ComplianceReportService.cs:89,368 downcasts + TryGetAny — check), DatabaseAdminUserStore registered (in-memory one is test double), rate limiter, caches, ONNX, telemetry.
Check: MfaSessionTokenService, OAuthLoginStateStore in-process singletons vs 3 replicas.

## C. Broken in UI
15. Status filters PENDING_APPROVAL/PENDING_STOCK → 400 (SalesOrdersController.cs:235 Enum.TryParse without underscore strip) — /sales/orders, /sales/allocations. TransfersController.cs:44 strips underscores = pattern. [verified]
16. Agnum config save: BY_WAREHOUSE/BY_CATEGORY/BY_LOGICAL_WH/TOTAL_ONLY/JSON_API vs enums ByWarehouse… → 400 (AgnumController.cs:89-97; WebUI Pages/Agnum/Configuration.razor:199-200,299-304). [verified]
17. /outbound/orders bulk cancel sends outbound-order ids to sales-order cancel route (WebUI Pages/OutboundOrders.razor:309-313).
18. /outbound/dispatch: only FedEx adapter; UPS/DHL/USPS/OTHER always fail, dialog has no manual tracking field (OutboundDispatch.razor:82-86,174-179; ShipmentCommandHandlers.cs:67-105). FedEx without BaseUrl returns fake "local" tracking number (FedExApiService.cs:55-66).
19. /transfers row "Cancel" button: only error toast, no API route (Transfers/List.razor:82,236-240).
20. /projections runbook link 404 (Projections.razor:153).

## D. Simulations presented as real
21. Backups /admin/backups: gzip of "-- Simulated pg_dump backup", restore = text log; DB row Completed (BackupServices.cs:146-180).
22. DR drills /admin/dr-drills: canned steps/results (DisasterRecoveryService.cs:116-172).
23. Capacity alert simulation (CapacityPlanningService.cs:14,92-109).

## Placeholders not user-reachable
Sagas/ReceiveGoodsSaga.cs:11 empty but registered (MassTransitConfiguration.cs:27); TransferExecuted double publish (TransferServices.cs:451-513).

## Owner decisions (2026-11-11)
1. Backups / DR drills / capacity alert simulation: HIDE from menu (keep code). Not "remove".
2. In-memory features (waves, RMA, cross-dock, serials, QC templates/defects, label queue, HU, quality analytics) stay VISIBLE while being rebuilt; each such screen gets a bright red banner: data entered here is not saved and is lost on restart (EN + LT). Banner removed when the real version ships.
3. Dispatch: manual carrier + tracking number for any carrier; no fake tracking numbers; carrier APIs later.
Architecture (Claude governance): no StockLedger migration now; fixes use the existing working event pattern (AdjustmentsController append / TransferServices.AppendStockMovementAsync). Implementation by Codex via prompt only when the user says.
