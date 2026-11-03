using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Pages;

public partial class Gamyba : ComponentBase
{
    // The designer's fixture switches are opt-in; the operator page stays clear.
    [Parameter, SupplyParameterFromQuery(Name = "prototypeControls")]
    public bool PrototypeControls { get; set; }

    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private readonly List<GamybaItem> _items = GamybaFixtures.Items();
    private readonly List<GamybaTask> _tasks = GamybaFixtures.Tasks();
    private readonly List<GamybaDocument> _documents = GamybaFixtures.Documents();
    private readonly List<GamybaRepair> _repairs = GamybaFixtures.Repairs();
    private readonly List<GamybaLabelJob> _jobs =
    [
        new() { Number="ZB-261102-021", Task="GU-2611-0147", PhysicalLabels=8, Sent=5, State="Uncertain", Mode="GU", Note="No ack after #5" },
        new() { Number="ZB-261102-020", Task="GU-2611-0146", PhysicalLabels=5, Sent=5, State="Sent", Mode="GU", Note="P1 + separator" }
    ];
    private readonly List<(string Number, string Order, string Reason, List<int> ItemIds)> _stops =
    [
        ("GS-261030-002", "KVT-261020-011", "Audinio defektas", [8,9])
    ];
    private readonly HashSet<int> _selected = [];
    private readonly HashSet<int> _stopSelected = [];
    private readonly List<(string Barcode, string Reason)> _scanErrors = [("250611000777", "Unknown barcode")];
    private readonly List<GamybaTemplateColumn> _columns = GamybaFixtures.Columns();
    private readonly List<GamybaTemplateColumn> _publishedColumns = GamybaFixtures.Columns();

    private string _screen = "queue";
    private string _lang = "en";
    private string _viewport = "fit";
    private string _role = "operator";
    private string _branch = "All";
    private string _type = "All";
    private string _priority = "All";
    private string _targetGu = "new";
    private int _firstN = 5;
    private int _cursor;
    private string _globalSearch = "";
    private string _taskSearch = "";
    private string? _fromSales;
    private string _currentTask = "GU-2611-0147";
    private string _currentDocument = "GT-261102-003";
    private string _newGtOp = "pagaminta";
    private bool _mineLastFour = true;
    private string _scanValue = "";
    private string _lastScanBarcode = "";
    private string _scanMode = "desktop";
    private string _scanOutcome = "";
    private string _scanReason = "";
    private string _manualScan = "";
    private string _importText = "";
    private string _stopOrder = "";
    private string _stopSeries = "KVT";
    private string _stopGroup = "2611";
    private string _stopTab = "stops";
    private string _stopReason = "Kliento prašymas";
    private string _stopBarcode = "";
    private string? _stopPrintNumber;
    private string _repairNumber = "R-0233";
    private string _repairFilter = "all";
    private string _historyBarcode = "";
    private string _techType = "Rolėtai";
    private string _techTab = "formulas";
    private string _techParameter = "PPP12";
    private string _techFormula = "W - 32";
    private int _testWidth = 1200;
    private int _testHeight = 1600;
    private string _testResult = "";
    private bool _formulaTested;
    private string _report = "Rolėtai";
    private string _reportMode = "preview";
    private string _templateVersion = "v2";
    private bool _templateDraftSaved = true;
    private bool _compareTemplates;
    private string? _selectedColumn;
    private string _labelMode = "task";
    private string _labelText = "JUOSTOS";
    private int _labelCopies = 1;
    private string _labelJob = "ZB-261102-021";
    private readonly HashSet<int> _reprintSelected = [];
    private string _comOwner = "agent";
    private string? _outputPreview;
    private string _outputPrintKind = "Išdavimas klientui";
    private string _finishedSelected = "GP-261102-001";
    private static readonly (string No, string Date, string Operation, string Recipient, int Count, string Vehicle, string Author)[] FinishedDocs =
    [
        ("GP-261102-001", "2026-11-02", "atiduota", "UAB Audenis Projektai", 1, "—", "Rūta M."),
        ("GF-261101-004", "2026-11-01", "filialui", "Filialas Kaunas", 6, "KDL 412 · T. Petraitis", "Jonas K.")
    ];
    private (string No, string Date, string Operation, string Recipient, int Count, string Vehicle, string Author) CurrentFinished => FinishedDocs.First(x => x.No == _finishedSelected);
    private string? _archiveReport;
    private string? _modal;
    private string _issuePhase = "idle";
    private string _issueProblem = "";
    private int? _issueConflictId;
    private string? _pendingCommand;
    private string? _appliedCommand;
    private string _notice = "";
    private string _noticeKind = "info";
    private bool _simConflict;
    private bool _simNoMaterial;
    private bool _simTimeout;
    private bool _simEmpty;
    private bool _simNetwork;
    private bool _simSlow;
    private bool _simZebraNoAck;

    private static readonly (string Key, string En, string Lt)[] Tabs =
    [
        ("queue", "Ready for production", "Paruošta gamybai"),
        ("tasks", "Production tasks", "Gamybos užduotys"),
        ("scan", "Scanning", "Skenavimas"),
        ("gt", "GT documents", "GT dokumentai"),
        ("stops", "Stops · Repairs", "Stabdymai · Remontas"),
        ("finished", "Finished goods", "Gatava produkcija"),
        ("history", "Product history", "Gaminio istorija"),
        ("technology", "Technology", "Technologija"),
        ("reports", "Documents", "Dokumentai"),
        ("labels", "Label queue", "Etikečių eilė")
    ];

    private static readonly (string Code, string Screen, string Flow, string Target)[] MapRows =
    [
        ("G01", "Sales → Ready for production", "Confirm in Sales → queue filtered by order, items pre-selected", "queue"),
        ("G02–G03", "Ready for production", "select rows / all / first N → target GU → Issue dialog → task card", "queue"),
        ("G03–G04", "Production tasks → GU", "composition by portion · history · PDFs · labels · warehouse · add portion", "task"),
        ("G07", "Scanning (desktop · mobile)", "scan + Enter → result → list; manual pick; import; error log; undo", "scan"),
        ("G08", "GT documents", "new GT → scan; own documents ≤ 4 days; details and last-row correction", "gt"),
        ("G09", "Stops", "pick order/items → stop document → view/print; Sales cancellation is separate", "stops"),
        ("G10", "Repairs", "unfinished / missing actual failure; sFactFailure by role; save & close", "repairs"),
        ("G11", "Product history", "barcode → item, stage, documents and allowed actions", "history"),
        ("G12", "GU → Warehouse", "existing write-off action; repeat blocked", "task"),
        ("G13", "Finished goods", "handover / dispatch → four output prints, invoice gated", "finished"),
        ("G14", "Technology", "types → parameters → formula → test → save; other settings by role", "technology"),
        ("G05", "Documents", "13 reports → NoData / preview / print / archive / version; template editor", "reports"),
        ("G06", "Label queue", "job modes → physical labels → pause / resume / cancel / reprint; COM owner", "labels")
    ];
    private static readonly string[] MatrixStates = ["empty", "no rights", "wrong state", "Access conflict", "no material", "network", "long op", "2nd click", "recovery"];
    private static readonly (string Operation, string States)[] MatrixRows =
    [
        ("Issue to GU", "yyyyyyyyy"), ("Add portion", "yyyqyyyyy"), ("Scan", "yyyynyyyy"),
        ("Undo last scan", "yypqnynyq"), ("New GT", "nyynnynyn"), ("Stop", "yyyqnpnyq"),
        ("Repair close", "nyqnnpnyq"), ("Write-off", "nyyqnpyyq"), ("Print PDF", "yynnnpyyy"),
        ("New PDF version", "yynynnyyy"), ("Zebra job", "yynynyyyy"), ("Reprint range", "yynnnyyyy"),
        ("Publish template", "nyynnnyyy"), ("Formula save", "nyqnnnnyq")
    ];
    private static readonly (string Id, string Area, string Question, string Design)[] MapQuestions =
    [
        ("P01", "Queue", "V_WaitingProductionBlinds and SP_fsubProduction differ by date and exclusions: which defines queue and Due?", "Shows order due date"),
        ("P02", "Issue to GU", "Concurrent Access and web GU numbering strategy is not decided.", "Number only after success"),
        ("P03", "GU card", "Limits for adding portions to current or historical GU and empty GU/GT?", "Allows open GU and empty GU"),
        ("P04", "All", "Menu, button and field rights matrix is not closed.", "Three fixture roles"),
        ("P05", "Documents", "Original layouts, expressions and subreports of 13 gam and 4 output reports unknown.", "Generic preview, columns only"),
        ("P06", "Documents", "Are UNC archives reachable, and is silent printing to a preset printer needed?", "Archive preview; browser print"),
        ("P07", "Labels", "Zebra model, DPI, label size, encoding and COM port per workstation unknown.", "CECH-02 · COM3 placeholder"),
        ("P08", "Scan correction", "Legacy DELETE audit is commented out; actor receipt and deletion rights unconfirmed.", "Only last row, later state blocks"),
        ("P09", "Stops · Repairs · Finished", "SQL side effects, payroll, notifications and stock contracts not examined.", "Fixture documents only"),
        ("P10", "Warehouse", "Where Access calls SP_WriteOffBlindComponentsNew and repeat policy.", "Once per GU"),
        ("P11", "Technology", "Trusted test-before-save calculator API is required.", "Fixture evaluator"),
        ("P12", "Reports", "Closed list of production-only reports and Excel exports undefined.", "G15 omitted"),
        ("D01", "Queue", "Which order defines first N: due date, order date or Access list order?", "Due, order, item"),
        ("D02", "Mixed GU", "Can a partial-rights user see GU with hidden rows?", "Rows hidden"),
        ("D03", "Scanning", "Audible signal required? Mobile and desktop sharing one GT?", "Visual only; same GT")
    ];

    private void OpenMapRow(string target)
    {
        if (target == "repairs") { OpenRepairs(); return; }
        if (target == "stops") _stopTab = "stops";
        Go(target);
    }

    private string L(string en, string lt) => _lang == "lt" ? lt : en;
    private bool Restricted => _role == "restricted";
    private bool Supervisor => _role == "supervisor";
    private string RoleLabel => _role switch
    {
        "supervisor" => L("Supervisor + editor", "Vadovas + redaktorius"),
        "restricted" => L("Restricted · Kaunas", "Ribota · Kaunas"),
        _ => L("Operator", "Operatorius")
    };
    private GamybaTask CurrentTask => _tasks.First(x => x.Number == _currentTask);
    private GamybaDocument CurrentDocument => _documents.First(x => x.Number == _currentDocument);
    private static bool CanContinueDocument(GamybaDocument doc) => doc.Author == "Rūta M." && DateOnly.Parse(doc.Created) >= new DateOnly(2026, 10, 29);
    private IEnumerable<GamybaDocument> ListedDocuments => _documents.Where(x => !_mineLastFour || CanContinueDocument(x));
    private GamybaRepair CurrentRepair => _repairs.First(x => x.Number == _repairNumber);
    private GamybaLabelJob CurrentJob => _jobs.First(x => x.Number == _labelJob);
    private int PlannedLabelCount => _labelMode switch
    {
        "one" => 1,
        "manual" => Math.Max(1, _labelCopies),
        _ => Math.Max(1, TaskItems(CurrentTask).Count()) + 1 + Math.Max(0, _labelCopies - 1)
    };
    private string PlannedLabelDetail => _labelMode switch
    {
        "one" => "1 item",
        "manual" => $"{PlannedLabelCount} manual",
        _ => $"{TaskItems(CurrentTask).Count()} item · 1 sep · {Math.Max(0, _labelCopies - 1)} extra"
    };
    private GamybaTemplateColumn? CurrentColumn => _columns.FirstOrDefault(x => x.Field == _selectedColumn);
    private static readonly (string Field, string Type)[] DatasetFields =
    [
        ("OrderNo", "text"), ("Barcode", "code"), ("Customer", "text"), ("Branch", "text"),
        ("Type", "text"), ("Fabric", "text"), ("Width", "mm"), ("Height", "mm"),
        ("Qty", "int"), ("Note", "text"), ("ControlSide", "enum"),
        ("Mechanism", "text"), ("Color", "text"), ("DueDate", "date")
    ];
    private void AddTemplateField(string field)
    {
        if (_columns.Any(x => x.Field == field)) return;
        _columns.Add(new GamybaTemplateColumn { Field=field, Label=field, Width=12 });
        _selectedColumn = field;
        _templateDraftSaved = false;
    }
    private void MoveTemplateField(int direction)
    {
        var index = _columns.FindIndex(x => x.Field == _selectedColumn);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= _columns.Count) return;
        (_columns[index], _columns[target]) = (_columns[target], _columns[index]);
        _templateDraftSaved = false;
    }
    private void RemoveTemplateField()
    {
        if (CurrentColumn is null) return;
        _columns.Remove(CurrentColumn);
        _selectedColumn = null;
        _templateDraftSaved = false;
    }
    private static string TemplateCell(GamybaItem item, string field) => field switch
    {
        "OrderNo" => item.Order,
        "Barcode" => item.Barcode,
        "Branch" => item.Branch,
        "Type" => item.Type,
        "Fabric" or "Color" => item.Fabric,
        "Width" => item.Width.ToString(),
        "Height" => item.Height.ToString(),
        "Qty" => item.Quantity.ToString(),
        "Note" => item.Note,
        "DueDate" => item.Due,
        _ => "—"
    };
    private string LabelKind(GamybaLabelJob job, int n)
    {
        if (job.ReprintOf is not null)
        {
            var original = _jobs.First(x => x.Number == job.ReprintOf);
            return LabelKind(original, job.ReprintNumbers[n - 1]);
        }
        if (job.Mode == "ONE") return "P1";
        if (job.Mode == "MANUAL") return "EXTRA";
        var rowCount = _tasks.FirstOrDefault(x => x.Number == job.Task) is { } task ? TaskItemCount(task) : 1;
        return n <= rowCount ? "P1" : n == rowCount + 1 ? "SEP" : "EXTRA";
    }
    private string LabelBarcode(GamybaLabelJob job, int n)
    {
        if (job.ReprintOf is not null)
        {
            var original = _jobs.First(x => x.Number == job.ReprintOf);
            return LabelBarcode(original, job.ReprintNumbers[n - 1]);
        }
        if (job.Mode == "ONE") return job.SourceBarcode ?? "—";
        if (job.Mode == "MANUAL") return "—";
        var task = _tasks.FirstOrDefault(x => x.Number == job.Task);
        return task is null ? "—" : TaskItems(task).ElementAtOrDefault(n - 1)?.Barcode ?? "—";
    }
    private string LabelState(GamybaLabelJob job, int n) => n <= job.Sent ? "Sent" : job.State == "Cancelled" ? "Cancelled" : "Pending";
    private GamybaItem? HistoryItem => _items.FirstOrDefault(x => x.Barcode == _historyBarcode);
    private IEnumerable<GamybaItem> VisibleItems => _items.Where(x => !Restricted || x.Branch == "Kaunas");
    private IEnumerable<GamybaItem> QueueItems => _simEmpty ? [] :
        VisibleItems.Where(x => x.Stage == "patvirtintas"
            && (_fromSales is null || x.Order == _fromSales)
            && (_branch == "All" || x.Branch == _branch)
            && (_type == "All" || x.Type == _type)
            && (_priority == "All" || x.Priority == _priority))
            .OrderBy(x => x.Due).ThenBy(x => x.Order).ThenBy(x => x.Id);
    private IEnumerable<GamybaItem> TaskItems(GamybaTask task) =>
        VisibleItems.Where(x => task.Portions.Any(p => p.ItemIds.Contains(x.Id)));
    private int TaskItemCount(GamybaTask task) => task.Portions.SelectMany(x => x.ItemIds).Distinct().Count();
    private string TaskOrders(GamybaTask task) => string.Join(", ", TaskItems(task).Select(x => x.Order).Distinct());
    private bool TaskMixed(GamybaTask task) => TaskItems(task).Select(x => x.Branch).Distinct().Count() > 1;
    private IEnumerable<GamybaItem> StopCandidates => VisibleItems.Where(x =>
        x.Stage is "gaminamas" or "patvirtintas" &&
        (string.IsNullOrWhiteSpace(_stopOrder) || x.Order.EndsWith(_stopOrder, StringComparison.OrdinalIgnoreCase)))
        .Take(6);
    private IEnumerable<GamybaRepair> FilteredRepairs => _repairs.Where(x =>
        _repairFilter == "all" || (_repairFilter == "unfinished" && !x.Done) || (_repairFilter == "unknown" && string.IsNullOrWhiteSpace(x.Fact)));
    private readonly Dictionary<string, List<(string Code, string Name, string Formula, string Unit)>> _techCatalog = new()
    {
        ["Rolėtai"] = [("PPP12", "Audinio plotis", "W - 32", "mm"), ("PPP13", "Audinio aukštis", "H + 250", "mm"), ("PPP10", "Juostos", "If(W > 1500, 2, 1)", "vnt."), ("PPP21", "Veleno ilgis", "W - 20", "mm")],
        ["Vertikalios"] = [("PPP31", "Lamelių kiekis", "Round((W - 20) / 89 + 1)", "vnt."), ("PPP32", "Lamelės ilgis", "H - 25", "mm")],
        ["Plise"] = [("PPP41", "Audinio ilgis", "H * 1.05 + 40", "mm"), ("PPP42", "Virvelių kiekis", "Max(2, Round(W / 300))", "vnt.")],
        ["Horizontalios"] = [("PPP51", "Lamelių kiekis", "Round(H / 22)", "vnt.")],
        ["Romanetės"] = [("PPP61", "Audinys", "(W + 60) * (H + 180) / 1000000", "m²")],
        ["Tinkleliai"] = [("PPP71", "Tinklo plotas", "W * H / 1000000 * Qty", "m²")]
    };
    private List<(string Code, string Name, string Formula, string Unit)> CurrentTechParameters => _techCatalog[_techType];
    private (string Code, string Name, string Formula, string Unit) CurrentTechParameter => CurrentTechParameters.First(x => x.Code == _techParameter);
    private IEnumerable<(string Name, string Value, string Source, string Rights)> TechOtherRows => _techTab switch
    {
        "colors" => [("Rolėtai", "Balta · Pilka · Juoda", "frmCategoriesColors", Supervisor ? L("Editable", "Redaguojama") : L("Locked", "Užrakinta")), ("Vertikalios", "Balta · Smėlio", "frmCategoriesColors", Supervisor ? L("Editable", "Redaguojama") : L("Locked", "Užrakinta"))],
        "acc" => [("Rolėtai", "Grandinėlė · Spyruoklė · Variklis", "frmAccessorisByBlindCategory", Supervisor ? L("Editable", "Redaguojama") : L("Locked", "Užrakinta")), ("Plise", "Rankena · Virvelė", "frmAccessorisByBlindCategory", Supervisor ? L("Editable", "Redaguojama") : L("Locked", "Užrakinta"))],
        _ => [("Patvirtintas → Gaminamas", "GU issue", "frmRulesStates · fn_CheckStateChange", L("Locked", "Užrakinta")), ("Gaminamas → Pagamintas", "GT pagaminta", "frmRulesStates · fn_CheckStateChange", L("Locked", "Užrakinta")), ("Gaminamas → Sustabdytas", "Stop document", "frmRulesStates", L("Locked", "Užrakinta"))]
    };
    private static bool MatchesReport(GamybaItem item, string report) => report switch
    {
        "Pleated Blinds" or "XL Plise" => item.Type == "Plise",
        _ => item.Type == report
    };
    private IEnumerable<GamybaItem> ReportItems => TaskItems(CurrentTask).Where(x => MatchesReport(x, _report));
    private int ReportCount(string report) => TaskItems(CurrentTask).Count(x => MatchesReport(x, report));
    private string StageClass(string stage) => stage switch
    {
        "patvirtintas" => "chip--ok",
        "gaminamas" => "chip--warn",
        "pagamintas" => "chip--ok",
        "atiduotas" or "filialui" => "chip--done",
        "sustabdytas" => "chip--danger",
        _ => "chip--info"
    };
    private string PriorityClass(string priority) => priority switch
    {
        "urgent" => "chip--danger",
        "vip" => "chip--info",
        "repair" => "chip--warn",
        _ => "chip--slate"
    };
    private string JobClass(string state) => state switch
    {
        "Sent" => "chip--done",
        "Sending" => "chip--info",
        "Failed" or "Uncertain" or "Cancelled" => "chip--danger",
        "Paused" => "chip--warn",
        _ => "chip--slate"
    };

    protected override void OnInitialized()
    {
        var uri = new Uri(Navigation.Uri);
        var order = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query)
            .GetValueOrDefault("order").ToString();
        if (!string.IsNullOrWhiteSpace(order))
            _fromSales = order;
    }

    private void Go(string screen)
    {
        _screen = screen;
        _modal = null;
        _notice = "";
    }

    private void ClearQueueFilters()
    {
        _branch = "All";
        _type = "All";
        _priority = "All";
        _fromSales = null;
        _simEmpty = false;
    }

    private void OpenSalesFixture(string order)
    {
        _fromSales = order;
        Notice("info", $"Sales order {order} is represented by fixtures in stage 1.", $"Sales užsakymas {order} 1 etape pateiktas pavyzdžiu.");
    }

    private void OpenStop(GamybaItem item)
    {
        _stopTab = "stops";
        var parts = item.Order.Split('-');
        _stopSeries = parts[0];
        _stopOrder = parts[^1];
        _stopSelected.Clear();
        _stopSelected.Add(item.Id);
        Go("stops");
    }

    private void IssueHistoryItem(GamybaItem item)
    {
        _selected.Clear();
        _selected.Add(item.Id);
        Go("queue");
    }

    private void OpenRepairs()
    {
        _stopTab = "repairs";
        Go("stops");
    }

    private void PrintHistoryLabel(GamybaItem item)
    {
        if (item.Gu is null) return;
        _currentTask = item.Gu;
        _labelMode = "one";
        _labelText = item.Barcode;
        _modal = "new-label-job";
    }

    private void ToggleStop(int id)
    {
        if (!_items.Any(x => x.Id == id && x.Stage == "gaminamas")) return;
        if (!_stopSelected.Add(id)) _stopSelected.Remove(id);
    }

    private void OpenArchivedPdf(string name)
    {
        _archiveReport = name;
        _modal = "archive";
    }

    private void WriteOffTask()
    {
        if (CurrentTask.WriteOff is not null) return;
        CurrentTask.WriteOff = $"SM-261102-{_tasks.IndexOf(CurrentTask) + 8:00}";
        Notice("ok", $"Fixture material movement {CurrentTask.WriteOff} recorded once.", $"Pavyzdinis medžiagų judėjimas {CurrentTask.WriteOff} įrašytas vieną kartą.");
    }

    private void PreviewOutput(string name)
    {
        _outputPreview = _finishedSelected;
        _outputPrintKind = name;
        _modal = "output";
    }

    private void SaveRepair()
    {
        if (!Supervisor || CurrentRepair.Done || string.IsNullOrWhiteSpace(CurrentRepair.Fact)) return;
        CurrentRepair.Done = true;
        Notice("ok", "Actual failure saved only in this fixture.", "Faktinis gedimas išsaugotas tik šiame pavyzdyje.");
    }

    private void ResetStopPicker()
    {
        _stopSelected.Clear();
        _stopOrder = "";
    }

    private static string StopDate(string number) => number.Contains("261030", StringComparison.Ordinal) ? "2026-10-30" : "2026-11-02";

    private void PreviewStop(string number)
    {
        _stopPrintNumber = number;
        _modal = "stop-print";
    }

    private async Task UseManualScan()
    {
        _scanValue = _manualScan;
        _manualScan = "";
        _modal = null;
        await Scan();
    }

    private void NewReportVersion()
    {
        if (!ReportItems.Any())
        {
            Notice("warn", "NoData: no PDF version was generated.", "NoData: PDF versija nesukurta.");
            return;
        }
        CurrentTask.Pdfs.Add($"{_report} · fixture-v2 · {DateTime.Now:HH:mm}");
        Notice("ok", "A fixture version was generated from current composition.", "Pavyzdinė versija sukurta pagal dabartinę sudėtį.");
    }

    private async Task PrintReport()
    {
        if (!ReportItems.Any())
        {
            Notice("warn", "NoData: printing cancelled.", "NoData: spausdinimas atšauktas.");
            return;
        }
        await Js.InvokeVoidAsync("gamybaUi.print", "report");
    }

    private async Task PrintOutput()
    {
        await Js.InvokeVoidAsync("gamybaUi.print", "output");
    }

    private void RollbackTemplate()
    {
        if (!Supervisor) return;
        _columns.Clear();
        _columns.AddRange(_publishedColumns.Select(x => new GamybaTemplateColumn { Field=x.Field, Label=x.Label, Width=x.Width }));
        _selectedColumn = null;
        _templateDraftSaved = true;
        Notice("info", "Draft reverted to the published version in this fixture.", "Juodraštis grąžintas į paskelbtą pavyzdžio versiją.");
    }

    private async Task SelectFocusedInput(FocusEventArgs _)
    {
        await Js.InvokeVoidAsync("gamybaUi.selectActive");
    }

    private void GlobalSearchKey(KeyboardEventArgs args)
    {
        if (args.Key != "Enter") return;
        var term = _globalSearch.Trim();
        if (term.Length == 12 && term.All(char.IsDigit))
        {
            _historyBarcode = term;
            Go("history");
        }
        else if (term.StartsWith("GU-", StringComparison.OrdinalIgnoreCase))
        {
            var task = _tasks.FirstOrDefault(x => x.Number.Contains(term, StringComparison.OrdinalIgnoreCase));
            if (task is not null) OpenTask(task.Number);
        }
        else if (term.StartsWith("GT-", StringComparison.OrdinalIgnoreCase))
        {
            var doc = _documents.FirstOrDefault(x => x.Number.Contains(term, StringComparison.OrdinalIgnoreCase));
            if (doc is not null) OpenDocument(doc.Number);
        }
        else
        {
            _fromSales = term;
            Go("queue");
        }
    }

    private void Notice(string kind, string en, string lt)
    {
        _noticeKind = kind;
        _notice = L(en, lt);
    }

    private void ToggleSelected(int id)
    {
        if (!_selected.Add(id)) _selected.Remove(id);
    }

    private void SelectAll()
    {
        var ids = QueueItems.Select(x => x.Id).ToArray();
        if (ids.Length > 0 && ids.All(_selected.Contains))
            foreach (var id in ids) _selected.Remove(id);
        else
            foreach (var id in ids) _selected.Add(id);
    }

    private void PickFirstN()
    {
        _selected.Clear();
        foreach (var id in QueueItems.Take(Math.Clamp(_firstN, 1, 999)).Select(x => x.Id))
            _selected.Add(id);
    }

    private void OpenIssue()
    {
        if (_selected.Count == 0) return;
        if (Restricted)
        {
            Notice("danger", "No rights to issue this portion.", "Neturite teisės išduoti šią porciją.");
            return;
        }
        _issuePhase = "idle";
        _issueProblem = "";
        _issueConflictId = null;
        _modal = "issue";
    }

    private async Task ConfirmIssue()
    {
        if (_issuePhase == "busy") return;
        _issuePhase = "busy";
        _pendingCommand = Guid.NewGuid().ToString("N")[..10];
        await Task.Delay(_simSlow ? 1800 : 350);
        var first = _items.FirstOrDefault(x => _selected.Contains(x.Id));
        if (_simConflict && first is not null)
        {
            _issuePhase = "error";
            _issueConflictId = first.Id;
            _issueProblem = $"{first.Order} · {first.Type} {first.Width}×{first.Height}: " +
                L("already issued by Access. Remove the conflicting row.", "jau išduota per Access. Pašalinkite konfliktuojančią eilutę.");
        }
        else if (_simNoMaterial && _items.FirstOrDefault(x => _selected.Contains(x.Id) && x.NoMaterial) is { } noMaterial)
        {
            _issuePhase = "error";
            _issueConflictId = noMaterial.Id;
            _issueProblem = $"{noMaterial.Order} · {noMaterial.Type}: " +
                L("insufficient material.", "nepakanka medžiagos.");
        }
        else if (_simTimeout || _simNetwork)
        {
            _issuePhase = "timeout";
        }
        else
        {
            ApplyIssue();
        }
    }

    private void CheckIssueResult()
    {
        if (_pendingCommand == _appliedCommand) return;
        ApplyIssue();
        Notice("info", "Original command receipt found. No duplicate issue.", "Rastas pirminės komandos kvitas. Pakartotinio išdavimo nėra.");
    }

    private void RemoveIssueConflict()
    {
        if (_issueConflictId is int id) _selected.Remove(id);
        _issueConflictId = null;
        _issuePhase = "idle";
        _issueProblem = "";
        _simConflict = false;
        _simNoMaterial = false;
        if (_selected.Count == 0) _modal = null;
    }

    private void ApplyIssue()
    {
        if (_pendingCommand == _appliedCommand) return;
        var gu = _targetGu == "new" ? null : _tasks.FirstOrDefault(x => x.Number == _targetGu);
        if (gu is null)
        {
            gu = new GamybaTask { Number=$"GU-2611-{147 + _tasks.Count - 1:0000}", Created="2026-11-02 " + DateTime.Now.ToString("HH:mm") };
            _tasks.Insert(0, gu);
        }
        var ids = _selected.ToList();
        gu.Portions.Add(new GamybaPortion { Number=gu.Portions.Count + 1, At=DateTime.Now.ToString("HH:mm"), ItemIds=ids });
        foreach (var item in _items.Where(x => ids.Contains(x.Id)))
        {
            item.Stage = "gaminamas";
            item.Gu = gu.Number;
        }
        _appliedCommand = _pendingCommand;
        _selected.Clear();
        _currentTask = gu.Number;
        _targetGu = "new";
        _modal = null;
        _issuePhase = "idle";
        _simTimeout = false;
        _simConflict = false;
        Go("task");
        Notice("ok", $"{gu.Number}: {ids.Count} rows issued in this fixture.", $"{gu.Number}: šiame pavyzdyje išduota {ids.Count} eil.");
    }

    private void NewEmptyTask()
    {
        var task = new GamybaTask { Number=$"GU-2611-{147 + _tasks.Count - 1:0000}", Created="2026-11-02 " + DateTime.Now.ToString("HH:mm") };
        _tasks.Insert(0, task);
        _currentTask = task.Number;
        Go("task");
    }

    private void OpenTask(string number) { _currentTask = number; Go("task"); }
    private void AddPortion() { _targetGu = _currentTask; Go("queue"); }
    private void OpenHistory(GamybaItem item) { _historyBarcode = item.Barcode; Go("history"); }
    private void OpenDocument(string number) { _currentDocument = number; Go("gt-detail"); }
    private void ContinueDocument(string number)
    {
        var document = _documents.FirstOrDefault(x => x.Number == number);
        if (document is null || !CanContinueDocument(document)) return;
        _currentDocument = number;
        Go("scan");
    }

    private void NewDocument()
    {
        _newGtOp = _screen == "finished" && Supervisor ? "atiduota" : "pagaminta";
        _modal = "new-gt";
    }

    private static (string Before, string After) OperationTransition(string operation) => operation switch
    {
        "filialui" => ("pagamintas", "filialui"),
        "atiduota" => ("pagamintas", "atiduotas"),
        "sandelis" => ("pagamintas", "pagamintas"),
        _ => ("gaminamas", "pagamintas")
    };

    private void CreateDocument()
    {
        if ((_newGtOp == "atiduota" && !Supervisor) || (_newGtOp == "sandelis" && Restricted)) return;
        var number = $"GT-261102-{_documents.Count + 2:000}";
        _documents.Insert(0, new GamybaDocument { Number=number, Operation=_newGtOp, Created="2026-11-02" });
        _currentDocument = number;
        _modal = null;
        Go("scan");
        Notice("ok", $"{number} · {_newGtOp} created in this fixture.", $"{number} · {_newGtOp} sukurta pavyzdyje.");
    }

    private async Task Scan()
    {
        var value = _scanValue.Trim();
        _scanValue = "";
        if (string.IsNullOrWhiteSpace(value)) return;
        _lastScanBarcode = value;
        if (!CanContinueDocument(CurrentDocument))
        {
            Reject(value, L("This GT document is not editable after four days or by another author.", "Šio GT negalima redaguoti po keturių dienų arba jei jį sukūrė kitas naudotojas."));
            return;
        }
        if (_simNetwork)
        {
            Reject(value, L("Network unavailable. Scan was not saved.", "Tinklas nepasiekiamas. Skenas neišsaugotas."));
            return;
        }
        var item = _items.FirstOrDefault(x => x.Barcode == value);
        if (value.Length != 12 || !value.All(char.IsDigit))
            Reject(value, L("Barcode must contain 12 digits.", "Kodą turi sudaryti 12 skaitmenų."));
        else if (item is null)
            Reject(value, L("Unknown barcode.", "Nežinomas kodas."));
        else if (Restricted && item.Branch != "Kaunas")
            Reject(value, L("Item belongs to another branch.", "Gaminys priklauso kitam filialui."));
        else if (CurrentDocument.Scans.Any(x => x.ItemId == item.Id))
        {
            _scanOutcome = "repeat";
            _scanReason = L("Already scanned in this GT document.", "Jau nuskenuota šiame GT dokumente.");
        }
        else if (item.Stage != OperationTransition(CurrentDocument.Operation).Before)
            Reject(value, L($"Wrong state: {item.Stage}. Expected {OperationTransition(CurrentDocument.Operation).Before}.", $"Netinkama būsena: {item.Stage}. Reikia {OperationTransition(CurrentDocument.Operation).Before}."));
        else
        {
            var after = OperationTransition(CurrentDocument.Operation).After;
            CurrentDocument.Scans.Add(new GamybaScan { ItemId=item.Id, At=DateTime.Now.ToString("HH:mm:ss"), Before=item.Stage, After=after });
            item.Stage = after;
            _scanOutcome = "accepted";
            _scanReason = $"{item.Order} · {item.Type} · {item.Width}×{item.Height}";
        }
        await Js.InvokeVoidAsync("gamybaUi.focusSelect", "gamyba-scan-input");
    }

    private void Reject(string barcode, string reason)
    {
        _scanOutcome = "rejected";
        _scanReason = reason;
        _scanErrors.Insert(0, (barcode, reason));
    }

    private async Task ScanKey(KeyboardEventArgs args)
    {
        if (args.Key == "Enter") await Scan();
    }

    private void UndoLastScan()
    {
        var last = CurrentDocument.Scans.LastOrDefault();
        if (last is null) return;
        if (!CanContinueDocument(CurrentDocument) || Restricted)
        {
            Notice("danger", "Only your recent GT document can be corrected.", "Taisyti galima tik savo naujausią GT dokumentą.");
            return;
        }
        if (last.Locked)
        {
            Notice("danger", "A later document already used this item. Correction blocked.", "Vėlesnis dokumentas jau panaudojo gaminį. Taisymas negalimas.");
            return;
        }
        var item = _items.First(x => x.Id == last.ItemId);
        item.Stage = last.Before;
        CurrentDocument.Scans.Remove(last);
        Notice("info", $"Last accepted scan {item.Barcode} was corrected.", $"Paskutinis priimtas skenas {item.Barcode} pataisytas.");
    }

    private async Task ImportScans()
    {
        foreach (var barcode in _importText.Split(['\r','\n',',',';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _scanValue = barcode;
            await Scan();
        }
        _importText = "";
        _modal = null;
    }

    private void StopItems()
    {
        if (_stopSelected.Count == 0 || Restricted || _items.Any(x => _stopSelected.Contains(x.Id) && x.Stage != "gaminamas")) return;
        var first = _items.First(x => _stopSelected.Contains(x.Id));
        var number = $"GS-261102-{_stops.Count + 1:000}";
        _stops.Insert(0, (number, first.Order, _stopReason, _stopSelected.ToList()));
        foreach (var item in _items.Where(x => _stopSelected.Contains(x.Id)))
            item.Stage = "sustabdytas";
        _stopSelected.Clear();
        Notice("warn", $"{number} created in this fixture. Sales cancellation is separate.", $"{number} sukurtas pavyzdyje. Sales anuliavimas yra atskiras.");
    }

    private void SelectStopBarcode()
    {
        var item = StopCandidates.FirstOrDefault(x => x.Barcode == _stopBarcode.Trim());
        if (item is null)
            Notice("danger", "Barcode not found among stoppable items.", "Kodas nerastas tarp stabdomų gaminių.");
        else
            _stopSelected.Add(item.Id);
    }

    private void TestFormula()
    {
        try
        {
            // The fixture evaluator intentionally accepts only the expressions shown in the handoff.
            // Live legacy formulas need the trusted server calculator in a later stage.
            var formula = _techFormula.Replace(" ", "", StringComparison.OrdinalIgnoreCase);
            double value = formula switch
            {
                "W-32" => _testWidth - 32,
                "H+250" => _testHeight + 250,
                "If(W>1500,2,1)" => _testWidth > 1500 ? 2 : 1,
                "W-20" => _testWidth - 20,
                "Round((W-20)/89+1)" => Math.Round((_testWidth - 20d) / 89d + 1d),
                "H-25" => _testHeight - 25,
                "H*1.05+40" => _testHeight * 1.05d + 40d,
                "Max(2,Round(W/300))" => Math.Max(2d, Math.Round(_testWidth / 300d)),
                "Round(H/22)" => Math.Round(_testHeight / 22d),
                "(W+60)*(H+180)/1000000" => (_testWidth + 60d) * (_testHeight + 180d) / 1_000_000d,
                "W*H/1000000*Qty" => _testWidth * _testHeight / 1_000_000d,
                _ => throw new InvalidOperationException("This formula is outside the fixture calculator.")
            };
            _testResult = $"{Math.Round(value, 2)} {CurrentTechParameter.Unit}";
            _formulaTested = true;
        }
        catch (InvalidOperationException)
        {
            _testResult = L("Cannot evaluate this formula in the fixture. Save is blocked.", "Šios formulės pavyzdyje apskaičiuoti negalima. Išsaugojimas blokuojamas.");
            _formulaTested = false;
        }
    }

    private void ChangeParameter(string code)
    {
        if (!CurrentTechParameters.Any(x => x.Code == code)) return;
        _techParameter = code;
        _techFormula = CurrentTechParameter.Formula;
        _formulaTested = false;
        _testResult = "";
    }

    private void ChangeTechType(string type)
    {
        if (!_techCatalog.ContainsKey(type)) return;
        _techType = type;
        ChangeParameter(CurrentTechParameters[0].Code);
    }

    private void SaveFormula()
    {
        if (!_formulaTested)
        {
            Notice("warn", "Test this formula before saving.", "Prieš išsaugodami patikrinkite formulę.");
            return;
        }
        var index = CurrentTechParameters.FindIndex(x => x.Code == _techParameter);
        var old = CurrentTechParameters[index];
        CurrentTechParameters[index] = (old.Code, old.Name, _techFormula, old.Unit);
        _formulaTested = false;
        Notice("ok", "Formula saved only in this fixture.", "Formulė išsaugota tik šiame pavyzdyje.");
    }

    private void SaveTemplate()
    {
        _templateDraftSaved = true;
        Notice("ok", "Template draft saved in this fixture.", "Šablono juodraštis išsaugotas pavyzdyje.");
    }

    private void PublishTemplate()
    {
        if (!Supervisor) return;
        _publishedColumns.Clear();
        _publishedColumns.AddRange(_columns.Select(x => new GamybaTemplateColumn { Field=x.Field, Label=x.Label, Width=x.Width }));
        _templateVersion = "v3";
        _templateDraftSaved = true;
        Notice("ok", "Template v3 published in this fixture; v2 retained.", "Šablono v3 versija paskelbta pavyzdyje; v2 išsaugota.");
    }

    private void QueueLabels()
    {
        var task = CurrentTask;
        var single = _labelMode == "one" ? TaskItems(task).FirstOrDefault(x => x.Barcode == _labelText.Trim()) : null;
        if (_labelMode == "one" && single is null)
        {
            Notice("danger", "Select a barcode from this GU for one-item printing.", "Vieno gaminio etiketei pasirinkite šios GU kodą.");
            return;
        }
        if (_labelMode == "manual" && !Supervisor) return;
        if (_comOwner == "access")
        {
            Notice("warn", "Access owns the COM port. Job remains pending.", "COM prievadą naudoja Access. Užduotis laukia.");
        }
        var count = PlannedLabelCount;
        var job = new GamybaLabelJob { Number=$"ZB-261102-{_jobs.Count + 21:000}", Task=task.Number, PhysicalLabels=count, Mode=_labelMode.ToUpperInvariant(), SourceBarcode=single?.Barcode, Note=_labelMode == "task" ? "P1 + separator + extra" : _labelMode == "one" ? single?.Barcode ?? "" : _labelText };
        _jobs.Insert(0, job);
        _labelJob = job.Number;
        _modal = null;
        Go("labels");
    }

    private void ChangeJobState(string state)
    {
        if (CurrentJob.State is "Sent" or "Cancelled") return;
        CurrentJob.State = state;
    }

    private void ToggleReprint(int number)
    {
        if (!_reprintSelected.Add(number)) _reprintSelected.Remove(number);
    }

    private void QueueReprint()
    {
        if (CurrentJob.State != "Uncertain" || _reprintSelected.Count == 0 || _reprintSelected.Any(x => x <= CurrentJob.Sent || x > CurrentJob.PhysicalLabels))
        {
            Notice("warn", "Select the exact labels to repeat.", "Pasirinkite tikslias etiketes kartojimui.");
            return;
        }
        var original = CurrentJob;
        var numbers = _reprintSelected.Order().ToList();
        var job = new GamybaLabelJob { Number=$"ZB-261102-{_jobs.Count + 21:000}", Task=original.Task, PhysicalLabels=numbers.Count, Mode="REPRINT", ReprintOf=original.Number, ReprintNumbers=numbers, Note=$"{original.Number} · #{string.Join(", #", numbers)}" };
        _jobs.Insert(0, job);
        _labelJob = job.Number;
        Notice("info", $"Fixture reprint queued for labels {string.Join(", ", numbers)}.", $"Pavyzdyje perspausdinamos etiketės {string.Join(", ", numbers)}.");
        _reprintSelected.Clear();
        _modal = null;
    }

    private async Task PageKey(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && _modal is not null)
            _modal = null;
        if (args.Key == "F2")
            await Js.InvokeVoidAsync("gamybaUi.focusSelect", "gamyba-global-search");
    }

    private void AdvanceJob()
    {
        if (_simZebraNoAck && CurrentJob.State is not "Cancelled" and not "Sent")
        {
            CurrentJob.State = "Uncertain";
            Notice("warn", "Zebra acknowledgement lost. Check exact labels before retry.", "Zebra patvirtinimas prarastas. Prieš kartodami patikrinkite konkrečias etiketes.");
            return;
        }
        if (_comOwner == "access")
        {
            CurrentJob.State = "Pending";
            Notice("warn", "COM busy: Access owns this workstation.", "COM užimtas: darbo vietą valdo Access.");
            return;
        }
        if (CurrentJob.State is "Paused" or "Uncertain" or "Failed" or "Cancelled" or "Sent") return;
        CurrentJob.State = "Sending";
        CurrentJob.Sent = Math.Min(CurrentJob.PhysicalLabels, CurrentJob.Sent + 1);
        if (CurrentJob.Sent == CurrentJob.PhysicalLabels) CurrentJob.State = "Sent";
    }

    private void QueueKey(KeyboardEventArgs args)
    {
        var rows = QueueItems.ToArray();
        if (args.Key == "ArrowDown") _cursor = Math.Min(rows.Length - 1, _cursor + 1);
        else if (args.Key == "ArrowUp") _cursor = Math.Max(0, _cursor - 1);
        else if (args.Key == " " && rows.Length > 0) ToggleSelected(rows[Math.Clamp(_cursor, 0, rows.Length - 1)].Id);
        else if (args.Key.Equals("a", StringComparison.OrdinalIgnoreCase)) SelectAll();
        else if (args.Key.Equals("g", StringComparison.OrdinalIgnoreCase)) OpenIssue();
        else if (args.Key == "Enter" && rows.Length > 0) OpenHistory(rows[Math.Clamp(_cursor, 0, rows.Length - 1)]);
    }
}
