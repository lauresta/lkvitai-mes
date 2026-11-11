# Warehouse C8 — промежуточный отчёт, 2026-11-11

## Возобновление после решения владельца

Решение 2026-11-11, дословно: «1 - da». Исключение только для #329: все проверки кроме E2E
проверены как SUCCESS; E2E отменён из-за DNS на [первой попытке](https://github.com/lauresta/lkvitai-mes/actions/runs/43036476178/attempts/1)
и [повторе с установкой около 15:39 UTC](https://github.com/lauresta/lkvitai-mes/actions/runs/43036476178/attempts/2).

#329 влит (`253c8089`); [Build and Push #43055040823](https://github.com/lauresta/lkvitai-mes/actions/runs/43055040823)
и [Deploy to Test #43055762166](https://github.com/lauresta/lkvitai-mes/actions/runs/43055762166) — SUCCESS.
Read-only docker inspect: API и WebUI работают на `253c8089e8f8eddec2b8db92343c768953b8d188`.

[PR #331](https://github.com/lauresta/lkvitai-mes/pull/331) влит после всех SUCCESS: установка Chromium без apt,
без изменения набора тестов и гейта. [E2E #43056665649](https://github.com/lauresta/lkvitai-mes/actions/runs/43056665649)
реально выполнил **249 тестов, все 249 прошли**. Исключение #329 здесь не применялось.

#330 перебазирован на main, история #329 и #330 сохранена при разрешении конфликта.
Следующий шаг после merge #330 — приёмка C8a с новым header-only QA-сеансом до четырёх часов;
затем C8b → C8c → C8d. Во всех дальнейших PR каждый check обязан иметь SUCCESS.

## Сделано до остановки в 14:29 UTC

- До изменений воспроизведены обе ошибки C8a: MassTransit harness не достигал Processing;
  PostgreSQL с конфигурацией Marten приложения воспроизвёл ошибку ItemId при повторной оценке.
- Начальное состояние трёх саг теперь null; inline snapshot ItemValuation удалён без удаления таблицы.
- Исправлена база средневзвешенной стоимости повторной приёмки: физический остаток и ещё не размещённый
  товар RECEIVING/QC_HOLD учитываются, текущая приёмка вычитается из исходного количества один раз.
- После исправлений: 792 unit-теста, 19 PostgreSQL-тестов приёмки успешны.
  Последовательность Initialize → AdjustCost → AdjustCost → LandedCost → WriteDown сохраняется;
  две приёмки 2 × 10 + 3 × 20 успешны, WAC = 16.
- SELECT на test: в двух таблицах Agnum-саг нет сохранённых документов с пустым CurrentState.
- [PR #328](https://github.com/lauresta/lkvitai-mes/pull/328) влит, main `23785872`.
- [Корректирующий PR #329](https://github.com/lauresta/lkvitai-mes/pull/329) добавляет отсутствующий
  linked source в Docker-контекст API. Локальный Docker build stage, включая Release publish, успешен.
- Read-only docker inspect подтвердил: API и WebUI на test остаются на образах `14259fd5` (C7).
  C8a на test ещё не развёрнут; приёмки C8a не создавались.
- Отчёт — [docs-only PR #330](https://github.com/lauresta/lkvitai-mes/pull/330).
- Подготовленные `WAREHOUSE_QA_ENABLED`, `WAREHOUSE_QA_EXPIRES_AT_UTC` и секрет `WAREHOUSE_QA_KEY`
  удалены из GitHub environment test; локальные временные credentials удалены. QA-вход на test не активирован.

## Отклонения до остановки в 14:29 UTC

1. PR #328 влит преждевременно: exit code 0 от `gh pr checks --watch` ошибочно принят за полный успех
   после отменённого E2E. Проверка ожидания исправлена: каждый обязательный check должен иметь SUCCESS.
   Ошибка исполнителя; C8a не объявлен завершённым.
2. [Build and Push #43034940929](https://github.com/lauresta/lkvitai-mes/actions/runs/43034940929)
   обнаружил отсутствие `WarehouseQaProof.cs` в Docker COPY. Исправление потребовало дополнительного PR C8a.
   [Deploy to Test #43035530013](https://github.com/lauresta/lkvitai-mes/actions/runs/43035530013) пропущен.
3. Обязательный [E2E #43032509821](https://github.com/lauresta/lkvitai-mes/actions/runs/43032509821)
   повторно отменён по таймауту на установке Playwright. Та же ошибка в
   [main E2E #43034940802](https://github.com/lauresta/lkvitai-mes/actions/runs/43034940802):
   `Temporary failure resolving 'azure.archive.ubuntu.com'`. Тесты не запускались.
   Смена CI-зависимостей и обход гейта не выполнялись.
   Последний [E2E корректирующего PR #329, #43036476178](https://github.com/lauresta/lkvitai-mes/actions/runs/43036476178)
   также отменён около 14:29 UTC по таймауту с тем же DNS-сбоем. Работа остановлена на CI-гейте.
4. PostgreSQL-регрессия обнаружила дополнительную ошибку WAC при повторной приёмке до putaway.
   Исправление ограничено целью C8a; правило физического количества OnHandValue не изменено.

## Осталось на момент остановки в 14:29 UTC

| Этап | Состояние |
| --- | --- |
| C8a | Код и локальные проверки готовы; CI/deploy/приёмка на test не завершены |
| C8b | Не начат: требуется успешная приёмка C8a |
| C8c | Не начат: склады и остатки на test не объединялись |
| C8d | Не начат: UI-приёмка на реальном SKU и финальный ценовой аудит не проводились |

Следующий разрешённый шаг: успешный E2E PR #329, остальные проверки SUCCESS → merge → Deploy to Test →
проверка C8a, затем C8b. Нельзя считать локальные PostgreSQL-тесты заменой приёмки на test.
PR #329 и #330 оставлены открытыми. Для приёмки понадобится новый header-only QA-сеанс до четырёх часов;
временный QA-код в main неактивен, его удаление остаётся пунктом C8d.

QA-данные не удалялись. Прямых SQL-записей на test не было. Другие модули, prod, legacy SQL,
Agnum write и синхронизация цен не изменялись.
