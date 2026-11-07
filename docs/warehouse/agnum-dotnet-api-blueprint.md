# Agnum API на .NET: implementation blueprint

Обновлено: 2026-11-07. Автор: Claude (Opus). Исполнитель: Codex.

Цель: заменить мёртвый `AgnumDBApi-1.39.jar` своим сервисом на .NET с тем же HTTP-контрактом, поднять контейнер на test,
перевести на него синхронизацию Cikada и включить этап S7b из `pricing-implementation-blueprint.md`.

Это единственное задание. Что здесь не указано, делать нельзя. Решения не пересматривать.

Отчёт вести в разделе «Состояние» в конце этого файла.

## 0. Проверенные факты

Не перепроверять, если код не противоречит.

**Что заменяем**
- Jar: Spring Boot, Jaybird 5, HikariCP, 98 классов приложения `lt.elit.agnumdbapi`. Часть логики лежит в
  `BOOT-INF/lib/elit-common-spring-boot-5.jar` и `elit-db-7.jar`. Их тоже разобрать.
- Jar отвечает 500 на всё, включая `/doc/health` и `/v3/api-docs`. Живого эталона нет.
- В jar есть `BOOT-INF/classes/application.properties` с несекретными значениями по умолчанию (`cfg.price-range.*`,
  `cfg.prk.export.kn0`) и примеры `examples/*.json`.
- Реальный SQL есть в логах: `/opt/agnum/logs/api_debug.log.0` на `root@10.11.12.15`, строк 4633. Логи читать только на
  хосте, в репозиторий не копировать, ключи не выводить.

**Firebird**
- Хост `192.168.0.13:3050`, файл `C:/AGNUMW/AgnumDb/LAURESTA-EUR-TESTAPI.GDB`. TCP из docker-сети test проверен.
- Параметры соединения `encoding=ISO8859_13;lc_ctype=ISO8859_13;sqlDialect=1`.
- **Dialect 1 обязателен.** В SQL jar двойные кавычки означают строки (`LIST(TRIM(S.KOD) || ":" || P.KIEKIS, ",")`),
  а `1/1000000` даёт дробное число для округления. В dialect 3 первое упадёт, второе тихо изменит результат.
- Похоже, это замороженная копия: `result count` 7470 и 2682 совпали 18 мая и 1 августа, а самая свежая правка среди
  10130 товаров в `agnum_product_links` датирована 2026-05-05. Новый API вернёт эти же данные, это норма.

**Настройка и пользователи**
- Рабочий файл настроек собирает `deploy-test.yml` из variable `AGNUM_APPLICATION_PROPERTIES` и секретов
  `CFG_APIUSERS_SANDELYS_APIKEY`, `CFG_APIUSERS_PARDAVIMAI_APIKEY` (environment `test`, repo `lauresta/agnum-api-deploy`).
- Пользователи `sandelys` (`sndid=493`) и `pardavimai` (`sndid=496`). Значения `${...}` в файле надо разворачивать.
- Те же ключи Cikada хранит в `AGNUM__WAREHOUSES__SANDELYS__APIKEY` и `AGNUM__WAREHOUSES__PARDAVIMAI__APIKEY`
  (repo `lauresta/lkvitai-mes`) и видит их в контейнере `lkvitai-mes-warehouse-api`.

**Cikada**
- `AgnumApiClient` вызывает ровно два маршрута:
  `GET /api/products/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id` и
  `GET /api/clients/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id`. Первый вызов `/api/products/search` идёт
  без параметров и должен дать 400, тогда клиент уходит на запрос выше.
- Эталонный SQL из лога для товаров:
  `SELECT KOD, POGRUPIS, F10, …, ROUND(KIEKIS + 1/1000000, 3) AS KIEKIS, ID_PRK, …, (SELECT LIST(B.BKOD, ',') FROM BK B WHERE B.ID_PRK=P.ID_PRK) AS BKOD FROM PRK P WHERE P.ID_SND=? AND LOWER(P.KOD) <> ? ORDER BY P.ID_PRK`.
- Адрес приходит из `Agnum__Api__BaseUrl`. **Такой variable сейчас нет**, работает значение по умолчанию из
  `docker-compose.test.yml`. Существующая `AGNUM_API_BASE_URL` никем не читается, не использовать её.
- `Deploy to Test` в `lkvitai-mes` запускается только после `Build and Push Docker Images`, ручного запуска нет.

**Деплой**
- Старый `deploy-test.yml` в `agnum-api-deploy` упадёт на шаге ожидания health: jar отвечает 500. Запускается он только
  после сборки по изменениям `Dockerfile` и `vendor/**`. Эти пути не трогать.
- `AgnumExport` через `ApiEndpoint` из `agnum_export_configs` к этой задаче не относится.

## 1. Правила

**Разбор jar**
- Разбирать вне репозитория, через `docker run eclipse-temurin:21-jdk` (`javap -p -c -constants`) или декомпилятор из
  Maven Central. На Mac нет Java.
- **Декомпилированный код и классы поставщика в репозиторий не класть и не копировать.** В репозиторий идёт только
  спецификация поведения своими словами (`docs/DOTNET_API_SPEC.md`) и свой код.

**Срок действия**
- `AppConfigFilter` не переносить, проверок срока и лицензии в новом сервисе нет.
- Сам jar не патчить. Не запускать старый jar с подменой даты или часов, в том числе ради эталонных ответов.

**Firebird**
- Все GET-маршруты работают только в read-only транзакции (`FbTransactionBehavior.Read`).
- Значения только через параметры. Имена полей и `order` только из белого списка, как в `AgDBField` и `SqlBuilder`.
- Никакого DDL. Никаких ручных записей в Firebird и в legacy SQL, ни для проверки, ни для отладки.
- Запись в Firebird разрешена только кодом этапа A4 и только после включения владельцем. Codex её не включает.

**Секреты**
- Не печатать ключи и пароли, не писать их в логи, в том числе значение `X-API-KEY`.
- Настройки только из файла на хосте или секретов GitHub.

**Границы**
- Не трогать: `vendor/`, `Dockerfile`, `docker-compose.test.yml`, `build-and-push.yml`, `deploy-test.yml`
  в `agnum-api-deploy` и контейнер `agnum-api`.
- В `lkvitai-mes` менять только то, что указано в A2.
- Портов на хост не публиковать, сервис доступен только в сети `lkvitai-mes_default`.
- Прод не трогать.

**Намеренные отличия от jar.** Других отличий не вводить. Найденный баг jar записать в «Отклонения», чинить только если
без этого маршрут не работает.

| # | Что | Почему |
| --- | --- | --- |
| 1 | Нет `AppConfigFilter` | Цель замены |
| 2 | `limit`/`offset` работают | В jar нет пробела: `ORDER BY P.ID_PRKROWS 5` |
| 3 | Запрос черновика заказа исправлен | В jar нет запятой: `-1 AS ID_DOK K.KOD` |
| 4 | `GET /api/orders/pdf/{docNo}` отвечает 501 в формате ошибки API | Jar запускает Windows-программу `Evsk1ToPdf.exe`, её нет ни в jar, ни на Linux |
| 5 | Логи в stdout, без `logs/api.log` | Стандарт контейнера |

## 2. Маршруты

Всего 19 GET-маршрутов, swagger и 7 записывающих. `GskController` (`/supplier-returns`) не содержит обработчиков, переносить нечего.

| Этап | Метод и путь | Кому нужен |
| --- | --- | --- |
| A1 | `GET /doc/health` | healthcheck |
| A1 | `GET /api/products/search` | Cikada |
| A1 | `GET /api/products/{id}` | |
| A1 | `GET /api/products/qty/search` | |
| A1 | `GET /api/products/price/search` | S7b |
| A1 | `GET /api/clients/search` | Cikada |
| A1 | `GET /api/clients/{id}` | |
| A3 | `GET /api/clients/{id}/doc/search`, `GET /api/clients/doc/search` | |
| A3 | `GET /api/orders/{docNo}`, `/api/orders/search` | |
| A3 | `GET /api/orders/tracking/{docNo}`, `/api/orders/tracking/search` | |
| A3 | `GET /api/orders/pdf/{docNo}` | отличие 4 |
| A3 | `GET /api/receipts/{docNo}` | |
| A3 | `GET /api/customer-returns/{docNo}`, `/api/customer-returns/search` | |
| A3 | `GET /api/employee/{id}` | |
| A3 | `GET /api/test/search` | |
| A3 | `GET /v3/api-docs`, `/swagger-ui/index.html` | |
| A4 | `POST /api/products`, `PUT /api/products` | |
| A4 | `POST /api/clients` | |
| A4 | `POST /api/orders` | |
| A4 | `POST /api/receipts` | |
| A4 | `POST /api/customer-returns` | |
| A4 | `POST /api/employee/timesheet` | |

## 3. Стек и файлы

Код живёт в `lauresta/agnum-api-deploy` (локально `../agnum-api-deploy`, сначала обновить `main`).

- `net10.0`: поддержка .NET 8 заканчивается 2026-11-10, а общего кода с Cikada нет.
- ASP.NET Core, `FirebirdSql.Data.FirebirdClient` с точной версией в csproj, ADO.NET без ORM, xUnit.
- `TreatWarningsAsErrors` включён.

```text
Agnum.Api.sln
src/Agnum.Api/
tests/Agnum.Api.Tests/
Dockerfile.net
docker-compose.net.test.yml
.github/workflows/build-and-push-net.yml
.github/workflows/deploy-net-test.yml
docs/DOTNET_API_SPEC.md
```

Контейнер:

| Параметр | Значение |
| --- | --- |
| Образ | `ghcr.io/lauresta/agnum-api-net:<sha>` |
| Контейнер | `agnum-api-net` |
| Compose project | `agnum-net` |
| Порт | `8181` внутри контейнера, без `ports:` |
| Сеть | `lkvitai-mes_default` (external) |
| Настройки | `/opt/agnum-net/application.properties:ro` |
| Runner | `[self-hosted, lkvitai-test]`, environment `test` |

Образ на `mcr.microsoft.com/dotnet/aspnet:10.0` с `curl` для healthcheck, сборка `dotnet test` перед `docker build`.

`deploy-net-test.yml` собирает `/opt/agnum-net/application.properties` той же логикой, что `deploy-test.yml`, и добавляет
`cfg.apiusers.sandelys.prices=KN1,KN2` и `cfg.apiusers.pardavimai.prices=KN1,KN2`.
Отдельный compose project нужен, чтобы `--remove-orphans` не снёс старый контейнер.

## 4. Этапы

Один этап = одна ветка = один PR. В `agnum-api-deploy` ветки `codex/agnum-net-<этап>`. Merge:
`gh pr merge --merge --admin --delete-branch`. После этапа запись в «Состояние» и сразу следующий этап.

### A0. Спецификация

Разобрать jar и написать `docs/DOTNET_API_SPEC.md`: для каждого маршрута параметры, SQL своими словами, поля JSON с
точными именами, статусы, тексты ошибок.

Обязательно зафиксировать:
- авторизацию: `X-API-KEY`, анонимные `/doc/*` и swagger, `cfg.apiusers.<n>.ip`, `X-Forwarded-For`, `maxqueryperday`,
  `rights` (`READ_ALL`, `SAVE_ALL`), `QueryCountHeader`;
- ключи настроек, включая `prices`, `prkpricesnd`, `dbalias`, `prk.*`, `n18.pozindex`, `nd.pozindex`;
- формат ошибки `timestamp, status, error, message` и таблицу `AppErrors`;
- `filter_type`: `eq, ne, co, sw, gt, ge, lt, le`, разбор дат в `FuncService`;
- различие `barcode` в `/api/products/{id}` и `barcodes` в поиске;
- формат дат JSON, отступы (`indent_output=true`), перекодировки в `AppConst` (`WIN_1252`, `WIN_1257`).

**Готово:** файл влит, разделы есть по всем маршрутам раздела 2, кода поставщика в нём нет.

### A1. Сервис с маршрутами для синхронизации и контейнер на test

Сделать маршруты A1 раздела 2, разбор `application.properties`, авторизацию, формат ошибок, пул на 5 соединений,
контейнер и оба workflow. Тесты: разбор настроек, авторизация, построение SQL и фильтров, JSON.

Подключение: `Dialect=1`, `Charset=ISO8859_13`, база и хост из `jdbc:firebirdsql://…`. Перекодировку зарегистрировать через
`CodePagesEncodingProvider`, если клиент этого требует.

**Проверка** (из контейнера Cikada, ключ не печатается):

```bash
docker exec lkvitai-mes-warehouse-api sh -c 'curl -s -H "X-API-KEY: $Agnum__Warehouses__sandelys__ApiKey" "http://agnum-api-net:8181/api/products/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id" | grep -c "\"code\""'
```

**Готово:**
- `sandelys` даёт 7470 товаров, `pardavimai` даёт 2682;
- `GET /api/products/search` без параметров даёт 400, как ждёт `AgnumApiClient`;
- без ключа 401 или 403, как в jar;
- `price/search?code=2103170012` даёт 200 и ключи `KOD`, `KN1`, `KN2`;
- `code` и `balance` совпадают с `agnum_product_links` и `agnum_virtual_warehouse_balances` импорта 2026-08-01 на test, SELECT
  через `docker exec lkvitai-mes-db-test psql`. Литовские буквы без искажений (`Apsauginės`, `Roletų audiniai`);
- `/api/clients/search` даёт 200 и разбирается в `AgnumClientDto`.

**Стоп:** Firebird не подключается или числа не совпадают и причина не найдена за разумное время. Запиши, что проверено,
и остановись. Подбирать другие настройки сервера и трогать базу нельзя. Менять версию клиента, `WireCrypt` и `Compression`
в строке подключения можно.

### A2. Перевод синхронизации и S7b

Работа в `lauresta/lkvitai-mes`, ветка `codex/warehouse-prices-s7b` от свежего `main`.

1. Создать variable: `gh variable set Agnum__Api__BaseUrl --env test --repo lauresta/lkvitai-mes --body http://agnum-api-net:8181`.
   Откат: `gh variable delete` этой же переменной.
2. Выполнить S7b из `pricing-implementation-blueprint.md`.
3. Дополнительно к S7b провести `Agnum__PriceSync__Enabled` через `docker-compose.test.yml` и `deploy-test.yml` из
   `vars.Agnum__PriceSync__Enabled`. Значение на test не задавать: включает владелец.

Merge запускает обычный `Build and Push` и `Deploy to Test`, контейнер получает новый адрес.

**Готово:**
- в контейнере `lkvitai-mes-warehouse-api` переменная `Agnum__Api__BaseUrl` указывает на `agnum-api-net`;
- CI и Deploy to Test зелёные;
- следующий импорт `0 6 * * *` пишет `Completed` с ненулевым числом товаров. Если сессия не доживёт до 06:00, записать это как
  непроверенное и идти дальше, этап не блокировать.

### A3. Остальные GET

Маршруты A3 раздела 2 и отличие 4. Тесты на SQL и JSON.

**Готово:** каждый маршрут отвечает с тестовым ключом кодом из спецификации, 500 нет. Проверять только чтение.

### A4. Запись 1:1, выключена

Маршруты A4 раздела 2: те же таблицы, генераторы (`ID_PRK_GEN`, `ID_PRK_REC_GEN`, `ID_KL_GEN`, `ID_DOK_GEN`, `ID_APV_GEN`,
`ID_TBLREC_GEN`), проверки дублей и длин, транзакция с откатом, право `SAVE_ALL`.

Настройка `cfg.write.enabled`, по умолчанию `false`. Выключено: 403 в формате ошибки API, соединение с Firebird не открывается.

На test не включать, запросов записи к Firebird не отправлять. Тесты только без базы. В «Состояние» написать прямо, что запись
на реальной базе не проверялась.

**Готово:** при выключенной настройке все 7 маршрутов отвечают 403, старые тесты зелёные.

## 5. Состояние

Заполняет Codex.

### Сделано

- A0 → `lauresta/agnum-api-deploy` PR #1, merged to `main`: `docs/DOTNET_API_SPEC.md` documents all routes, parameters, SQL behavior, JSON names, auth, configuration, and errors in original prose. GitGuardian passed. This documentation-only stage has no test deployment trigger.
- A1 → `lauresta/agnum-api-deploy` PR #2, merged to `main` (`a41accc`). Build run 42410418864 and test deploy 42410601175 passed. From the Cikada warehouse container, authenticated product search returned 7470 for `sandelys` and 2682 for `pardavimai`; empty search returned 400, missing key 401, price search returned 200 with KN1/KN2, and client search returned 200. Read-only Firebird checks also confirmed Lithuanian text and sample `AUDR` code/balance against the 2026-08-01 warehouse import. The old `agnum-api` container and deployment files were untouched.
- A2 → `lauresta/lkvitai-mes` PR #296 merged to `main` (`a5c0f93b`). Build run 42411687350 and Deploy to Test run 42411951311 passed. The Cikada warehouse container has `Agnum__Api__BaseUrl=http://agnum-api-net:8181` and `Agnum__PriceSync__Enabled=false`; the owner has not enabled price writes. The live price endpoint returned 200 from that container, its response is a test fixture, and eight focused client/sync tests passed. The next scheduled 06:00 import has not yet occurred; its Completed count remains unverified and does not block A3.
- A3 → `lauresta/agnum-api-deploy` PR #3 merged to `main` (`cdec6a7`). Build run 42413242120 and test deploy 42413339822 passed. Seven contract tests passed. From the Cikada warehouse container, all A3 GET routes returned the specified 200/404/501 statuses with the test key; no route returned 500. The sample order and both order/return searches returned 200. Only read requests were sent.
- A4 → `lauresta/agnum-api-deploy` PR #4 merged (`77c6149`), followed by A4 parity correction PR #5 merged (`f7d50ea`). Final build run 42415230544 and test deploy 42415313426 passed; eight .NET tests passed. From the Cikada warehouse container, all seven write routes returned 403 and a price GET returned 200. `cfg.write.enabled` remains false, the new container has no published host port, and the old `agnum-api` container is untouched. The enabled write path was not tested on the real Firebird database; no Firebird write request was sent.

### Отклонения от blueprint

- A0: inspection of the jar service showed `/api/products/price/search` serializes an array of `{ "<code>": { "KN1": ..., "KN2": ... } }`, whereas the earlier pricing blueprint described flat rows with `KOD`. The replacement will preserve the inspected wire shape; A2 must parse it. `/api/products/qty/search` uses the same nested-by-code pattern.
- A1: `FirebirdSql.Data.FirebirdClient` 10.3.4 does not accept Jaybird's `ISO8859_13` client encoding. `UTF8` was required for product/client reads; `WIN1257` failed to transliterate some client rows. The warehouse name in the quantity route is explicitly cast from legacy bytes as `WIN1257` so `Sandėlys` renders correctly. SQL dialect remains 1 and all GET transactions are read-only.
- A2: the live `sandelys` price response has 7470 rows but 7463 distinct codes; seven repeated codes have identical KN1/KN2 values. The parser accepts identical repetitions and rejects conflicting values.
- A3: the decompiled jar's customer-return search applies `DOKTIP=4` (order type), which returns orders instead of returns. The replacement uses return type 12 so `/api/customer-returns/search` functions as its named route.
- A4: after PR #4 was merged, comparison with legacy write behavior exposed mismatches in document amount/line price columns, delivery lookup, client duplicate checks, and product barcode handling. PR #5 corrects them while keeping writes disabled. Contrary to A0's prose description, legacy product create/update does not write `BK` barcodes; the replacement leaves `BK` unchanged. The extra A4 correction PR was necessary because the initial A4 PR was merged before this comparison.

### Осталось

- The next scheduled warehouse import at 06:00 has not been observed; its `Completed` status and nonzero product count remain unverified, as explicitly nonblocking in A2.
- `Agnum__PriceSync__Enabled` and `cfg.write.enabled` remain false on test. Their activation belongs to the owner under the blueprint; no live Firebird write path has been exercised.
