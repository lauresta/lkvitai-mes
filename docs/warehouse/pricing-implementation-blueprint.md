# Warehouse: цены на всех экранах — implementation blueprint

Версия: 2026-11-07. База: `main` после PR #280/#281. Автор blueprint: Claude; исполнитель: Codex.
Этот документ — **единственное задание на код**. Всё, чего здесь нет, делать нельзя.
Статус и решения: [pricing-visibility-plan.md](pricing-visibility-plan.md). Порядок работы и отчёт:
[pricing-codex-handoff.md](pricing-codex-handoff.md).

## 0. Проверенные факты (2026-11-07)

Не перепроверять и не пересматривать, если код не противоречит.

- **Agnum API мёртв.** `agnum-api` (`AgnumDBApi-1.39.jar`) на test отвечает 500 на всё:
  `invalid exp config`, `AppConfigFilter`. Срок работы прошит в jar поставщика константами
  `2026-08-01`. Последний импорт с данными — 2026-08-01. Лечится только новым jar от поставщика.
  **Не патчить jar, не обходить фильтр, не ходить напрямую в Firebird.**
- С 2026-08-01 ежедневный импорт пишет `Completed` с 0 товаров: `AgnumApiClient.FetchProductsAsync`
  возвращает `null` на любой не-2xx, `GetProductsAsync` превращает это в пустой список.
- `GET /api/products/price/search` по байткоду: `List<JSONObject>` с ключами `KOD` + `KN1…KN5`
  (набор из `cfg.apiusers.<user>.prices`) либо `KOD` + `PRICE` (`KN2`), если список пуст.
  Склад — `prkpricesnd`, иначе склад пользователя. Уровни: KN1 `didm` (опт), KN2 `mazm` (розница),
  KN3–5 `pap1–3`. Это **только продажные цены**. Закупочной цены в API нет (`PRK.KNB` в DTO не выходит).
- Test-база Warehouse: 9505 товаров, цена у двух (`1422073200`: 99.00 / 12.00; `0000001`: 15.00 / 10.00),
  `on_hand_value` пусто, `mt_doc_availablestockview` пусто, 7 событий `StockMoved`.
  Живой объём только в `agnum_virtual_warehouse_balances` (срез 2026-08-01: 7452 + 2678 товаров).
  Экран с этими данными — `/agnum/balances`.
- Для товаров Agnum `Item.InternalSKU` равен коду Agnum (`2103170012`).
- WebUI — лист зависимостей: Contracts не видит, DTO копирует в `WebUI/Models/*.cs`.
- `POST` под `/api/warehouse/v1/items/*` требует `ITEM/UPDATE` (`PermissionPolicyMiddleware`), поэтому
  новый маршрут лежит **вне** `/items`.
- Frontline **анонимен** на test: страница и `/api/frontline/fabric/{code}` отвечают 200 без входа
  (issue #102 открыт). Закупочная цена туда не идёт никогда.
- Legacy-база общая с production (PR #267). Только чтение. Никаких DDL, процедур, записи.
- Цена ткани в legacy плоская: у типа изделия одна строка `DIC_PriceList` (потолки 7000×7000), единица
  `Zinynas_tipai.KainosMatas` → `Zinynas_matai.Matas` (`1m pločio`, `kvm`, `1m aukščio`…). Запрос из S6
  проверен на коде `R403`: `R19 · 28.96 · 1m pločio`, `R25 LP · 30.70`.

## 1. Общие правила

**Единое значение цены:**

| Поле | Правило |
|---|---|
| `EffectiveCost` | `OnHandValue.UnitCost`, если строка есть и `> 0`; иначе `Item.PurchasePrice`; иначе `null` |
| `CostSource` | `"WAC"`, `"Purchase"` или `null` |
| `BasePrice` | `Item.BasePrice` |
| `Value` | `decimal.Round(qty * EffectiveCost, 2, MidpointRounding.AwayFromZero)`; `null`, если нет cost |

Если у товара несколько строк `OnHandValue`, брать с максимальным `LastUpdated`.

- Подписи колонок: `Cost`, `Sale`, `Value` (склад пока не переведён, новых ключей i18n нет).
- На экранах движений подпись `Value (current cost)`: цена текущая, не историческая.
- Пустая цена: `–`, никогда `0.00`. Сумма по строкам считает только строки с ценой.
- Все роли видят всё: никаких проверок ролей вокруг цен.
- Сбой запроса цен **не ломает экран**: остатки показываются, цены `–`.
- UI: `AGENTS.md`, `docs/ux/lkvitai-mes-ux-handoff.html`; деньги моноширинно, вправо (`is-num`).
- **Миграций нет во всех этапах.** Если кажется, что нужна: стоп, запись в handoff.

**Запрещено:** новые страницы и пункты меню; рефакторинг вне перечисленных файлов; правки Sales,
Shopfloor, Finance, Portal, MCP; смена дизайна экранов; мультивалюта, FIFO, экспорт цен в Agnum, перевод
на LT; новые NuGet-пакеты; запись в legacy; ручной SQL на test; ввод паролей, токенов, обход SSO.

## S1. Основа: снимок цен

Ветка `codex/warehouse-prices-s1`.

**Contracts** `Warehouse.Contracts/ReadModels/ItemPriceSnapshot.cs`:

```csharp
public sealed record ItemPriceSnapshot(int ItemId, string Sku, decimal? EffectiveCost, string? CostSource,
    decimal? PurchasePrice, decimal? BasePrice);
```

**Application:**
- `Ports/IItemPriceSnapshotService.cs`:
  `Task<IReadOnlyList<ItemPriceSnapshot>> GetAsync(IReadOnlyCollection<int> itemIds, IReadOnlyCollection<string> skus, CancellationToken ct);`
- `Services/ItemPriceResolver.cs`: статическая чистая логика `Resolve(wacUnitCost, purchasePrice)` и `Value(qty, cost)`.

**Infrastructure:** `Pricing/ItemPriceSnapshotService.cs` на `WarehouseDbContext` (`Items` + `OnHandValues`,
`AsNoTracking`, SKU без учёта регистра). Регистрация в `Infrastructure/DependencyInjection.cs`.

**Api:** новый `Api/Controllers/ItemPriceSnapshotsController.cs`:
- `POST /api/warehouse/v1/item-price-snapshots`, тело `{ "itemIds": [..], "skus": [..] }`, ответ `ItemPriceSnapshot[]`.
- `[Authorize]` без policy уровня Manager: видят все роли. Больше 2000 ключей суммарно: 400.

**Заменить три копии join'а** на сервис, поведение то же: `StockController.GetAvailableAsync`,
`DashboardController` (строки 314 и 434), `CycleCountsController` (строка 227).

**StockController:**
- `AvailableStockRow` + `BasePrice`, колонки `UnitCost,TotalValue,BasePrice` в `BuildAvailableCsv`.
- `GetLocationBalanceAsync`: `TotalValue` на локацию (сумма `qty × EffectiveCost`), в ответ и CSV.

**WebUI:**
- `Models/ItemPriceSnapshotDto.cs`.
- `Services/ItemPricesClient.cs` по образцу `PutawayClient`, `AddScoped` в `Program.cs`.
- `Services/PriceMap.cs`: `PriceMap.Empty`, `Get(int? itemId, string? sku)`, `Value(decimal qty, int? itemId, string? sku)`.
  `ItemPricesClient.GetMapAsync(rows ключей)` ловит ошибки, логирует и возвращает `PriceMap.Empty`.
- `Infrastructure/Money.cs`: `Money.Format(decimal?)` → `12.50 €` / `–`. Заменить `FormatMoney` на
  AdminItems, AvailableStock, InboundShipmentCreate, AdminItemDetail, Dashboard.

**Тесты:** unit `ItemPriceResolverTests` (WAC, WAC=0, только Purchase, ничего, округление); unit на сервис по
образцу `AgnumBalanceImportServiceTests`; расширить тесты StockController, если они есть.

**Готово:** `dotnet build src/LKvitai.MES.sln -c Release`, unit и architecture зелёные; на test анонимный
`POST /api/warehouse/v1/item-price-snapshots` даёт 401, не 404.

## S2. Импорт цен из Excel

Ветка `codex/warehouse-prices-s2`. Идёт вторым: это единственный источник цен, пока нет Agnum.

- `ExcelTemplateService`: `PriceHeaders = ["InternalSKU", "PurchasePrice", "BasePrice"]`, ветка `"prices"`.
- `MasterDataImportService`: ветка `"prices"` → `ImportPricesAsync(rows, dryRun, errors, startedAt, ct)`.
  - Неизвестный SKU, дубль в файле, отрицательное или нечисловое значение: `ImportErrorReport`, файл не применяется.
  - Пустая ячейка: поле не меняется. Обновляются только отличающиеся значения.
  - На каждое изменение строка `ItemPriceHistory` (`ItemPriceTypes.Purchase` / `Base`, `Reason = "excel-import"`,
    `ChangedBy` из `ICurrentUserService`).
  - `dryRun=true` ничего не пишет и считает те же итоги. Файл на 10 000 строк проходит одним сохранением.
- `AdminImport.razor`: `new("prices", "Item Prices")`.
- `ImportController` не меняется. Цены **не** добавлять в импорт `items`.

**Тесты:** `ImportIntegrationTests`: применение, пустая ячейка, неизвестный SKU, история, dry-run.

**Готово:** шаблон `GET /api/warehouse/v1/admin/import/prices/template` скачивается под админом. Реальные цены
на test **не загружать**: файл даёт владелец.

## S3. Экраны остатков

Ветка `codex/warehouse-prices-s3`. Файлы в `Warehouse.WebUI/Pages/`.

| Экран | Файл | Источник ключа и qty | Что добавить |
|---|---|---|---|
| `/agnum/balances` | `Agnum/Balances.razor` | `AgnumBalanceRowDto.ItemId/Sku/Quantity` | `Cost`, `Sale`, `Value`; итог `Value` над таблицей |
| `/available-stock` таблица | `AvailableStock.razor` | API | `Sale` |
| `/available-stock` список, галерея | `AvailableStock.razor` | API | `Cost`, `Sale`, `Value` |
| `/stock/dashboard` локации | `StockDashboard.razor` | `StockLevelRowDto` | `Value` (сумма по строкам локации) |
| `/stock/dashboard` low stock, expiring | `StockDashboard.razor` | `StockLevelRowDto` | `Cost`, `Sale`, `Value` |
| `/reports/stock-level` + CSV | `ReportsStockLevel.razor` | `StockLevelRowDto` | `Cost`, `Sale`, `Value` |
| `/stock/location-balance` | `StockLocationBalance.razor` | `LocationBalanceItemDto.TotalValue` | `Value` |
| `/admin/items` | `AdminItems.razor` | `AdminItemDto` | `Purchase` рядом с `Base Price` |

`StockLevelRowDto` получает `UnitCost`, `TotalValue`, `BasePrice` (они уже в ответе `/stock/available`).
`/agnum/balances`: `GetMapAsync` по странице строк.

**Готово:** на test в `/agnum/balances` у `1422073200` и `0000001` видны цены, у остальных `–`.

## S4. Операционные экраны

Ветка `codex/warehouse-prices-s4`. Везде `PriceMap`, кроме двух строк с изменением API.

| Экран | Файл | Ключ, qty | Что добавить |
|---|---|---|---|
| `/reports/stock-movements` + CSV | `ReportsStockMovements.razor`, `ReportsController` | `ItemId`, `Qty` | сервер: `UnitCost`, `Value`; колонки `Cost`, `Value (current cost)` |
| `/inbound/shipments` | `InboundShipments.razor`, `ReceivingController.GetShipmentsAsync` | по `InboundShipmentLines` страницы | сервер: `TotalExpectedValue` = сумма `ExpectedQty × UnitPrice`; колонка `Value` |
| `/stock/adjustments` | `StockAdjustments.razor` | `ItemId`, `QtyDelta` | `Cost`, `Value (current cost)` |
| `/reservations` строки | `Reservations.razor` | `ReservationLineDto.SKU`, `RequestedQty` | `Cost`, `Sale`, `Value` |
| `/picking/tasks` | `PickingTasks.razor` | выбранный товар | строка `Cost · Sale` над подсказками локаций |
| `/putaway` | `Putaway.razor` | `ItemId`, `Qty` | `Cost`, `Sale`, `Value` |
| `/outbound/pack/{id}` | `PackingStation.razor` | `OutboundOrderLineDto` | `Sale`, `Value` |
| `/outbound/orders/{id}` | `OutboundOrderDetail.razor` | `OutboundOrderLineDto` | `Cost`, `Sale`, `Value` |
| `/inbound/qc` | `ReceivingQc.razor` | `QcPendingRowDto` | `Cost`, `Sale` |
| `/cross-dock` | `CrossDock.razor` | `CrossDockDto.ItemId`, `Qty` | `Cost`, `Value` |
| `/rmas` | `Rmas.razor` | `RmaLineDto.ItemId`, `Qty` | `Cost`, `Value` |
| `/transfers/{id}/execute`, `/transfers/create` | `Transfers/*.razor` | `TransferLineDto.ItemId`, `Qty` | `Cost`, `Value` |
| `/cycle-counts/{id}/execute`, `/discrepancies` | `CycleCounts/*.razor` | `CycleCountLineDetailDto.ItemId` | `Cost`, `Value` расхождения |
| `/agnum/reconcile` | `Agnum/Reconciliation.razor` | `AgnumReconciliationRowDto.Sku`, `Delta` | `Cost`, `Value` расхождения |
| `/reports/pick-history`, `/reports/receiving-history`, `/admin/lots` | соответствующие файлы | поля строки | `Cost`, `Value`, если в строке есть `ItemId` или SKU |

Если в DTO экрана нет `ItemId` и SKU: экран пропустить и записать в handoff. **Новый API ради ключа не делать.**
Не трогать: `/visualization/3d`, `/analytics/*`, `/labels`, `/dashboard`, `/valuation/*`, `/admin/serial-numbers`.

**Готово:** `dotnet build`, unit, architecture; экраны открываются без ошибок в консоли на пустых данных.

## S5. Подсказка в выборе товара

Ветка `codex/warehouse-prices-s5`.

Новый компонент `Components/Stock/ItemPriceHint.razor`, параметр `ItemId` (`int?`): строка
`Available 12.00 · Cost 10.00 € · Sale 15.00 €`. Снимок через `ItemPricesClient`, остаток через существующий
`StockClient.SearchAvailableStockAsync`. Перезагрузка при смене `ItemId`, ошибка → `–`.

Поставить под выбором товара: `StockAdjustments.razor`, `PickingTasks.razor`, `SalesOrderCreate.razor` (в строке
заказа), `InboundShipmentCreate.razor` (в строке поставки), `Transfers/Create.razor`.

В `<select>` и `ToStringFunc` цены **не** дописывать. Логику `SalesOrderCreate`, подставляющую цену группы, не менять.

## S6. Frontline: ориентировочная продажная цена

Ветка `codex/warehouse-prices-s6`. **Только продажная.** Закупочной нет: Frontline анонимен. Таблица соответствия
ткань ↔ товар Agnum отменена.

- `Frontline.Contracts/Fabric/FabricReferencePriceDto.cs`: `record FabricReferencePriceDto(string TypeName, string? Unit, decimal Price, bool IsFrom)`.
- `FabricCardDto` + `IReadOnlyList<FabricReferencePriceDto> ReferencePrices`, в конце записи.
- `Frontline.Infrastructure/Sql/Queries/FabricReferencePrices.sql`, в csproj `<EmbeddedResource Include="Sql/Queries/*.sql" />`
  (как в Sales.Infrastructure), отдельная команда в
  `SqlFabricQueryService.GetMobileCardAsync` после чтения процедуры. `mes_Fabric_GetMobileCard` не менять.
  Ошибка этого запроса: лог и пустой список, карточка работает.

```sql
SELECT TOP (5) t.TipoPavadinimas AS TypeName, m.Matas AS Unit,
       MIN(l.mPrice) AS Price, CASE WHEN MIN(l.mPrice) <> MAX(l.mPrice) THEN 1 ELSE 0 END AS IsFrom
FROM dbo.TBD_Components AS c
JOIN dbo.TBD_ComponentsPriceGroupRows AS cgr ON cgr.iComponentID = c.iComponentID
JOIN dbo.TCR_TypeColourPriceGroup AS g ON g.iComponentsPriceGroupID = cgr.iComponentsPriceGroupID
JOIN dbo.DIC_PriceList AS l ON l.iPriceGroupID = g.sPriceGroupID
JOIN dbo.TCR_PriceGroupPeriods AS p ON p.iPeriodID = l.iPeriodID
JOIN dbo.Zinynas_tipai AS t ON t.TipoID = g.iTypeID
LEFT JOIN dbo.Zinynas_matai AS m ON m.MatoID = t.KainosMatas
WHERE c.sCodeInternal = @Code AND p.dtValidFrom <= @Today AND p.dtValidUntil >= @Today
GROUP BY t.TipoPavadinimas, m.Matas
ORDER BY Price;
```

- `StubFabricQueryService`: две строки-примера.
- `FabricLookup.razor`: блок `Kainos` после виджета ширин: `R19 · 28.96 € / 1m pločio`, при `IsFrom` префикс `nuo`.
  Пустой список: блок не показывать. Подпись «Orientacinė kaina».
- Low-stock не менять.

**Тесты:** unit в `LKvitai.MES.Tests.Frontline.Unit` на маппинг. **Готово:** на test
`GET /api/frontline/fabric/R403` содержит `referencePrices`, карточка показывает блок.

## S7. Agnum: честный статус и цены

Ветка `codex/warehouse-prices-s7a` выполняется всегда, `-s7b` только по условию ниже.

**S7a.** `AgnumApiClient.FetchProductsAsync`: ответ `>= 500` или сетевая ошибка → `AgnumApiUnavailableException`
(Integration/Agnum), логика `400 → fallback` остаётся. `AgnumBalanceImportService` уже ставит `Failed` по исключению.
`AgnumImportRecurringJob` и `AgnumNomenclatureImportService` проверить: задача не падает бесконечными ретраями, данные
не стираются. То же для `FetchClientsAsync`. Тест в `AgnumBalanceImportServiceTests`.
**Готово:** на test следующий запуск (`0 6 * * *`) или ручной пишет `Failed` с причиной.

**S7b. Условие:** из контейнера `lkvitai-mes-warehouse-api` на test
`GET http://agnum-api:8181/api/products/price/search?code=2103170012` с `X-API-KEY` даёт 200. Иначе **S7b не начинать**,
ничего не заготавливать, записать в handoff. Ключ не печатать.

Если 200: сохранить реальный ответ в тест-фикстуру без секретов; `IAgnumApiClient.GetPricesAsync`, разбор ключей без
учёта регистра; `AgnumPriceSyncService` в Infrastructure/Agnum; ветка в `AgnumImportRecurringJob` после импорта товаров.
Маппинг: `KN2` → `Item.BasePrice`, `KN1` → `ItemPriceOverride` группы `Didmeninė` (создать, если нет). Остальные уровни
не трогать. Запись истории с `Reason = "agnum-sync"`. `PurchasePrice` не менять. Настройка `Agnum:PriceSync:Enabled`,
по умолчанию `false`.
