# Codex: Warehouse prices — handoff

Задание: [pricing-implementation-blueprint.md](pricing-implementation-blueprint.md). Blueprint готов, не переписывать.
Решения владельца: [pricing-visibility-plan.md](pricing-visibility-plan.md), не пересматривать.

## Порядок

S1 → S2 → S3 → S4 → S5 → S6 → S7a → S7b (только при выполненном условии).

## Как работать

- Один этап = одна ветка от свежего `main` = один PR.
- До merge зелёные: `warehouse-ci`, `architecture`, `frontline-ci` где применимо, SonarCloud quality gate.
- Merge: `gh pr merge --merge --admin --delete-branch`. Дождаться Build and Push и Deploy to Test.
- Проверить критерий «Готово» этапа, короткий отчёт в этот файл, следующий этап. Подтверждений не ждать.
- Без авторизованной сессии UI на test глазами не проверяется: не обходить вход, не брать пароли и токены. Проверить что
  доступно (API-коды, образ, unit-тесты, локальный запуск) и честно написать, что не смотрел.
- Факт в коде не совпал с blueprint: минимальная правка в рамках цели этапа и строка в разделе «Отклонения». Если
  правка выходит за перечисленные файлы или требует миграции: остановиться и записать.
- Вопросы владельцу только при блокировке. S7b, ждущий нового jar, блокировкой не считается: пропустить и закончить.

## Состояние

- S1 → PR #283 → Release solution build, 768 Warehouse unit tests, 29 architecture tests, CI/quality gate, Build and Push, Deploy to Test прошли; анонимный POST `/api/warehouse/v1/item-price-snapshots` вернул 401. UI глазами без авторизованной сессии не проверял.
- S2 → PR #284 → Release build, три новых PostgreSQL integration tests, CI/quality gate, Build and Push, Deploy to Test прошли. Анонимный GET шаблона `/api/warehouse/v1/admin/import/prices/template` вернул 401; скачивание под админом без авторизованной сессии не проверял. Реальные цены не загружал.
- S3 → PR #285 → Warehouse WebUI Release build, 768 Warehouse unit tests, 29 architecture tests, CI/quality gate, Build and Push, Deploy to Test прошли. `/warehouse/agnum/balances` отвечает 200. Цены двух товаров глазами не проверил: доступного авторизованного браузера не было, Mac заблокирован.
- S4 → PR #286 → Warehouse WebUI Release build, 768 Warehouse unit tests, 29 architecture tests, CI/quality gate, Build and Push, Deploy to Test прошли. `/warehouse/agnum/balances` отвечает 200, анонимный API отчёта — 401. Экраны и консоль глазами не проверил без авторизованной сессии.

## Отклонения от blueprint

- S2: `ImportIntegrationTests` запускали контейнер с базой `postgres`; `EnsureDeleted` не мог удалить текущую БД, а повторное создание теряло расширение `vector`. В тестовом fixture задана отдельная БД и удалён лишний `EnsureDeleted`; три новых price-теста проходят. Полный opt-in прогон существующего класса: 7/8, старый `ImportItems_Commit_ThenReimport` падает на конфликте EF tracking — вне scope цен, не исправлял.
- S4: `/reports/receiving-history` пропущен: `ReceivingHistoryRowDto` не содержит `ItemId` или SKU. Новый API ради ключа запрещён blueprint.

## Осталось и готовый промт

_(заполняет Codex в конце: что не сделано и почему, промт на продолжение)_
