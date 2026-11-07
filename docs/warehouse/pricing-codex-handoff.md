# Codex: Warehouse prices — implementation blueprint и выполнение

Дата: 2026-11-07. Источник: [pricing-visibility-plan.md](pricing-visibility-plan.md) (решения
приняты, менять их не нужно). Это задание на **blueprint → код → handoff**, строго в scope.

## Решения, которые уже приняты

- На складских экранах показываем **обе** цены: себестоимость (WAC из `on_hand_value`, иначе
  `Item.PurchasePrice`) и продажную (`Item.BasePrice`, с override группы там, где известен клиент).
  Что оставить после демо — решит клиент, не мы.
- Закупочную цену видят **все роли**. Никакого gating по ролям.
- Frontline: ровно раздел 5 плана (две цены, таблица соответствия ткань ↔ товар Agnum с
  автозаполнением по названию и коду поставщика, ручная правка в admin-экране, ориентир
  1000 × 1000 мм из `DIC_PriceList`).
- Валюта везде неявный EUR. FIFO/партии, мультивалюта, выгрузка цен в Agnum, перевод складских
  экранов на LT — **вне scope** (этап G). Этап F (дашборд) уже сделан в PR #280.

## Шаг 1 — Implementation blueprint (первый коммит, до любого кода)

Файл `docs/warehouse/pricing-implementation-blueprint.md`, на русском, как
`docs/shopfloor/trapezoid/implementation-task.md`. Для каждого этапа A0, A, B, C, D, E:

1. Точный список файлов (создать / изменить), типов, endpoint'ов, EF-миграций, DI-регистраций.
2. Контракты: DTO в `Warehouse.Contracts` (zero deps), порт в `Warehouse.Application`,
   реализация в `Warehouse.Infrastructure` (EF `WarehouseDbContext`), контроллер в `Warehouse.Api`.
   Правила слоёв из `CLAUDE.md` и `tools/DependencyValidator` обязательны: Application без EF/Marten,
   Contracts без ссылок.
3. Тесты: unit (`LKvitai.MES.Tests.Warehouse.Unit`) на порядок fallback цены и на разбор ответа
   Agnum; integration там, где уже есть похожие (`ReceivingWorkflowIntegrationTests`).
4. Критерии готовности: какой URL на https://cikada-test.lauresta.com открыть и что там увидеть.
5. Порядок PR и веток: `codex/warehouse-prices-a0`, `-a`, `-b`, `-c`, `-d`, `-e`.

Blueprint пишется по **фактам из кода**, не по памяти. Перед написанием проверить и зафиксировать
в нём:

- Ответ `GET {Agnum__Api__BaseUrl}/api/products/price/search` на test (контейнер `agnum-api`,
  базовый URL в `docker-compose.test.yml`; доступ через test-хост `root@10.11.12.15`). Какую цену он
  отдаёт (закупочную/учётную/продажную), по какому ключу (`code` = `AgnumProductLink.AgnumCode`),
  в каком формате. Если endpoint недоступен или цены в нём нет — этап A сводится к цене из
  supplier mapping (`SupplierItemMapping.PricePerUnit`) и приёмки, и это фиксируется в blueprint,
  а не додумывается.
- Как сейчас устроен импорт товаров: `AgnumImportController` (`POST /api/warehouse/v1/agnum/import/products`,
  `import/balances`), `IAgnumApiClient.GetProductsAsync` в `Warehouse.Integration/Agnum`,
  `AgnumProductDto` (цены в нём нет).
- Где сейчас три копии join'а себестоимости: `StockController.GetAvailableAsync`
  (`OnHandValues` → fallback `Item.PurchasePrice`), `DashboardController` (TotalStockValue),
  `CycleCountsController` (value impact). Все три переводятся на один сервис в этапе B.
- Сущности: `Item.BasePrice/PurchasePrice`, `OnHandValue(ItemId, ItemSku, Qty, UnitCost, TotalValue)`,
  `ItemPriceOverride(ItemId, PriceGroupId, Amount)`, `ItemPriceHistory`, `SupplierItemMapping.PricePerUnit`
  — всё в `Warehouse.Domain/Entities/MasterDataEntities.cs`.
- Экраны без цены — таблица в разделе 2 плана; DTO экранов в `Warehouse.WebUI/Models/*.cs`,
  клиенты в `Warehouse.WebUI/Services/*.cs`; `FormatMoney` сейчас скопирован в пять страниц
  (AdminItems, AvailableStock, InboundShipmentCreate, AdminItemDetail, Dashboard) — в этапе C
  вынести один раз в общий helper/компонент и использовать везде.
- Frontline: `SqlFabricQueryService` → процедуры `mes_Fabric_GetMobileCard` /
  `mes_Fabric_GetLowStockList` (`docs/sql/frontline-f2-2.sql`), источник `dbo.TBD_Components`
  + `dbo.V_Remains`; legacy-цены: `TCR_TypeColourPriceGroup(iTypeID, iComponentsPriceGroupID,
  sPriceGroupID)` → `DIC_PriceList(iPriceGroupID, iWidth, iHeight, mPrice, iPeriodID)` →
  `TCR_PriceGroupPeriods`. Коды ткани (`RPT-12`) и товара Agnum (`2103170012`, имя
  `PT audinys PT-12 GEORGIA blue`) общего ключа не имеют.

Факты на test (2026-11-07): 9505 товаров, цена у 2; `on_hand_value` пусто; проекция остатков
пуста; 1 price group, 0 overrides; `supplier_item_mappings` 0 строк. Поэтому A0 (импорт
остатков из Agnum на test) идёт первым, иначе демо показывает прочерки.

Blueprint коммитится отдельным первым PR (`codex/warehouse-prices-blueprint`), влить, потом код.

## Шаг 2 — Выполнение строго по blueprint

- Один этап = один PR = одна ветка от `main`. Autopilot: commit → PR → `gh pr merge --merge --admin`
  → дождаться Build and Push + Deploy to Test → проверить критерий готовности на cikada-test →
  короткий отчёт. Проверки PR (warehouse-ci, architecture, SonarCloud quality gate) должны быть
  зелёными до merge.
- Если факт в коде не совпал с blueprint — сначала правка blueprint отдельным коммитом в том же
  PR с пометкой «уточнение», потом код. Расширять scope нельзя: никаких новых экранов, нового
  визуального языка, рефакторинга вне перечисленных файлов, правок в Sales/Shopfloor/Finance.
- UI только по `AGENTS.md` и `docs/ux/lkvitai-mes-ux-handoff.html`: деньги в `mono` (`is-money
  is-num`), dense-таблицы, без новых цветов.
- Пустая цена показывается как `–`, не как `0,00`.
- Миграции EF только через `dotnet ef migrations add`, проверить `dotnet ef database update` на
  dev-базе; не руками.

Порядок: A0 → A → B → C → D → E. Если на этап E не хватает ответа по ценам (нет доступа к
legacy-прайсу), остановиться после D и описать это в handoff, не изобретать источник.

## Шаг 3 — Handoff

В конце (или при остановке) обновить этот файл: раздел «Состояние» — что влито (номера PR),
что проверено на test и как, что не сделано и почему, какие факты blueprint пришлось уточнить,
и готовый промт для следующего Codex-сеанса на оставшееся.

## Состояние

_(заполняет Codex)_
