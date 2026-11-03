# Gamyba: реестр действий Access → web

Статус: начальный реестр этапа 0, 2026-11-02. Источник — актуальный `/Users/bykovas/Desktop/Uzsakymai.adp` (SHA-256 `84185a33c8390abf327cac0bf469b0240dc135d650acdc5d8d4c60fef8cabaf5`), его извлечённый VBA, меню и прочитанные SQL-определения. `Confirmed` подтверждает наличие legacy-действия, **не** готовность web. `Unknown` означает, что полный контракт ещё не извлечён. Никакая строка ниже пока не имеет статуса `verified`.

Подтверждённое исходное событие указано без паролей, реальных заказов и данных клиентов. Для каждой строки до реализации дописать: вход/выход, SQL-объекты, права на строку/действие/поле, побочные эффекты, тестовый пример и результат сверки Access/web. Список расширяется при обнаружении действующей кнопки/события; архивные и закомментированные заглушки отмечаются отдельно.

| ID | Действие оператора | Источник legacy | Статус | Что проверить до записи/приёмки |
|---|---|---|---|---|
| G01.01 | Подтвердить изделия к производству | `frmOrdersConfirmation`; Sales M2 confirmation | Confirmed | Живая очередь web ведёт в существующий Sales order entry для подтверждения; сверить само подтверждение, права, допустимость, материалы и состояние 1173102375 |
| G02.01 | Просмотреть очередь готовых изделий | `frmBlindsProduction.FillTvw/FillLvw`; `V_WaitingProductionBlinds`; `SP_fsubProduction` | Confirmed | Web проверяет меню 49 и `Loc_leidimas`; дерево Access из view, строки из процедуры: дата изделия против даты заказа и исключённая категория всё ещё требуют сверки счётчиков |
| G02.02 | Отобрать отдельные, все или первые N изделий | `frmBlindsProduction.tvwBlinds_NodeCheck/TVCheckNodes`, toolbar | Confirmed | В web живые строки можно выбрать по одной, все или первые N **на текущей странице**; выбор сбрасывается при обновлении/фильтре/пагинации. Выдача на test включена только для Testavimas; полный отбор по всем страницам и сортировку сверить с Access |
| G03.01 | Создать новый GU, в том числе пустой | `frmBlindsProduction.NewTask` | Confirmed | Web создаёт GU в одной транзакции с первой порцией: формат Access `GUyyMMdd` + счётчик дня, автор mapped SQL. Пустой GU отдельно пока не создаётся; проверить запись и номер на Testavimas |
| G03.02 | Выдать выбранную порцию в текущее GU | `frmBlindsProduction.Gaminti`; `TAB_ProductionOutputDocumentDetails` trigger | Confirmed | Web вставляет один barcode на INSERT под mapped SQL login и повторно проверяет eligibility/Loc_leidimas; транзакция порции атомарна. Проверить живой триггер, статус 1, повторную попытку на Testavimas |
| G03.03 | Добавить следующую порцию в то же GU | повторный `Gaminti` с `txtProdTaskID` | Confirmed | Web на test позволяет добавить порцию только к собственному доступному GU, весь существующий состав которого относится к Testavimas; проверить на живом GU пределы дополнения, опубликованные ранее PDF и историю состава |
| G04.01 | Просмотреть задания и их состав | `frmBlindsProduction`, документы GU | Confirmed | Реальные поля, состояние, история, поиск и доступ к смешанному GU |
| G04.02 | Проверить прежние выдачи изделия | `sprec_findBlindThatWerePrintedByGamTaskID` | Confirmed | Это история выдач, не доказательство физической печати |
| G05.01 | Выбрать производственный отчёт и посмотреть preview | `dlgPrintDocuments.cmdPreview_Click`; `DIC_ProjectReports` category `gam` | Confirmed | Все 13 действующих шаблонов, NoData и права по документу |
| G05.02 | Печатать отчёт на обычном принтере | `dlgPrintDocuments.cmdPrintReport_Click` | Confirmed | Размер, ориентация, масштаб, многостраничность |
| G05.03 | Экспортировать отчёт в PDF | `dlgPrintDocuments.cmdExportToPDF_Click` | Confirmed | Два UNC-архива, без перезаписи оригинала; авторизация чтения |
| G05.04 | Повторно открыть исходный PDF или создать новую версию | legacy архивы/повторная печать | Unknown | Доступность архива, версия до/после изменения GU, пометка регенерации |
| G06.01 | Напечатать этикетки всего GU | `dlgPrintDocuments.cmdPrintLabels_Click`; `SP_GamHorizontaliosLabelsByTask` | Confirmed | Порядок, групповые разделители, bPrintLabels, специальные варианты, EPL |
| G06.02 | Напечатать этикетку одного изделия или вручную | `dlgPrintDocuments.fraLabelPrintingOptions_AfterUpdate`; `sp_BlindDescriptionByBarCode` | Confirmed | Доступные режимы, права, количество физических этикеток |
| G06.03 | Приостановить, продолжить или остановить пачку | `dlgPrintDocuments.fPause` в цикле отправки | Confirmed | Остаток пачки, неопределённый исход, занятой COM |
| G07.01 | Сканировать barcode и получить результат | `frmScanning.CheckNewBarCode`; `fn_CheckStateChange` | Confirmed | Видимый успех/повтор/ошибка, права, состояние, фокус и Enter |
| G07.02 | Выбрать изделие из списка вручную | `frmScanning.FillTreeView`/input mode | Confirmed | Read-only API кандидатов через `lnfn_ListReadayForScanningBlinds`, разрешённый целевой статус, меню 41 и `Loc_leidimas` проверены; live UI/сканирование пока не включены. |
| G07.03 | Импортировать список barcode | `frmScanning.cmdOpenFile/cmdReadFile` | Confirmed | 12-символьные коды, порядок и частичный результат по строкам |
| G07.04 | Просмотреть/выгрузить журнал ошибочных сканов | `modMeniu.OpenScanningErrorLog` | Confirmed | Состав, доступ, период жизни локального журнала |
| G08.01 | Создать документ операции GT | `frmMadeProductionDocuments` → `dlgNewProductionOutputDocument` | Confirmed | Список разрешённых статусов, номер/дата/автор, ACL |
| G08.02 | Открыть существующий документ, продолжить сканирование | `frmMadeProductionDocuments.lstDocs_DblClick` → `frmScanning` | Confirmed | Старое окно последних 4 дней/свой автор, допустимое дополнение |
| G08.03 | Просмотреть детали документа | `modMeniu.OpenProductionOutPutDetailedForm` | Confirmed | Barcode, до/после статуса, права каждой строки |
| G08.04 | Удалить ошибочно принятый последний скан | `modMeniu.DeleteItemFromProductionMovemementDocument`; details DELETE trigger | Confirmed | Последующая операция запрещает удаление; прежний статус, права, audit receipt |
| G09.01 | Выбрать заказ/изделия для остановки | `frmBlindProductionCancelling` cascade/list | Confirmed | Серия/группа/номер, состояние 1, scope |
| G09.02 | Создать документ остановки и добавить barcode | `frmBlindProductionCancelling.cmdNewCancellDocument_Click/txtBarcode_AfterUpdate` | Confirmed | Полный SQL-контракт, номер, причины, статус и побочные эффекты |
| G09.03 | Смотреть/печатать документ остановки | `frmBlindProductionCancelling.cmdView_Click`; report | Confirmed | Dataset, NoData, шаблон, архив |
| G10.01 | Открыть ремонт из изделия/заказа | `frmNewBlind`; `TAB_BlindRepairments` | Confirmed | Единый Sales/Shopfloor контракт, причина, описание, история |
| G10.02 | Указать фактическую неисправность | `frmInWarrantyFailures.cmd_SaveAndClose_Click` | Confirmed | `project.TCR_UserFormControls`, право на `sFactFailure` |
| G10.03 | Найти незавершённые/незаполненные гарантии | `frmNotFinishedWarranties`, `frmInWarrantyUnknown` | Confirmed | Фильтры, статусы, разрешённое завершение |
| G10.04 | Повторно выдать/закрыть ремонт | связанные формы/`rptGamDokGarantiniai` | Unknown | Полная цепочка документов и переходов, фактическое меню |
| G11.01 | Найти изделие по barcode и открыть историю | `dlgFindBlindByBarCode` | Confirmed | Живая очередь web открывает историю по barcode под mapped legacy login; сверить Parent ACL, разные документы и прямой URL |
| G12.01 | Создать складское движение по GU | `SP_WriteOffBlindComponentsNew` | Confirmed | Место вызова, расчёт PPP12, `bConfirmed`, запрет повтора |
| G12.02 | Просмотреть связанный складской документ/остатки | legacy movement references | Unknown | Фактические переходы из Gamyba, без нового StockLedger |
| G13.01 | Зарегистрировать выдачу клиенту/отправку филиалу | `frmMadeProductionDocuments`/allowed operation | Unknown | Статусы, recipient, warehouse/finance effects |
| G13.02 | Печатать документы выдачи/отправки/перевозки | `dlgPrintDocuments` category `prodoutput` | Confirmed | Четыре позиции, реальные параметры и финансовая граница PVM |
| G14.01 | Править формулы/PPP и пробно вычислить до сохранения | `frmTypesAdditionalParameters_fsubTypes` | Confirmed | Те же функции/allowlist, без временной записи в production |
| G14.02 | Править цвета/аксессуары/правила состояний | `frmCategoriesColors`, `frmAccessorisByBlindCategory`, `frmRulesStates` | Confirmed | Точные поля/права/сохранение и влияние на Sales |
| G15.01 | Смотреть действующие производственные отчёты/журналы | `frmReports`/`frmNewReports` | Unknown | Закрытый перечень только производственных действий |
| G15.02 | Экспортировать производственную выработку | `frmReports`, `sproc_DisplayLastMonthWork` | Confirmed | Поля/группировки и Excel pivot, без коммерческого/зарплатного модуля |

Проверенные старые заглушки: закомментированная Excel-реализация `dlgPrintDocuments.cmdExportToEXL_Click` — `legacy-placeholder`, не G05-обязательство. Остальные формы/подформы и SQL-цепочки ещё сверяются; таблица не объявлена закрытой.

## Реализовано на этапе 2, ожидает полную приёмку

На test включены read-only API/UI для очереди с филиалом/серией/категорией/типом/приоритетом, GU с составом, истории изделия и ремонтов. Сервер определяет Cikada account из проверенной Portal cookie или SSO JWT, получает `LegacySqlLogin` по защищённому внутреннему запросу и выполняет SQL на отдельном непулируемом соединении через `EXECUTE AS LOGIN`. Все прямые запросы к изделиям/документам фильтруются `Loc_leidimas().UKS`; смешанный или пустой GU скрыт. Запись, печать и складские действия не подключены.

Для 13 производственных отчётов подготовлен read-only dataset pilot: [источники и ограничение доступа](legacy-production-report-datasets.md). Полные макеты, PDF/печать, другие источники истории, образец ремонта и сверка под разными mapped users остаются открытыми в [issues](legacy-production-issues.md); этот блок не означает приёмку этапа 2.
