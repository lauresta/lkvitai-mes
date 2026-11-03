using LKvitai.MES.Modules.Shopfloor.WebUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Pages;

public partial class Gamyba
{
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [Inject] private ShopfloorApiClient ShopfloorApi { get; set; } = default!;

    private bool LiveQueueEnabled => Configuration.GetValue<bool>("Shopfloor:ProductionQueue:Enabled");
    private ProductionQueuePageDto? _liveQueue;
    private List<ProductionQueueFacetDto> _liveFacets = [];
    private string? _liveFacetError;
    private string? _liveQueueError;
    private bool _liveQueueLoading;
    private int _liveQueuePage = 1;
    private readonly HashSet<long> _liveSelected = [];
    private int _liveFirstN = 5;
    private IReadOnlyList<ProductionQueueItemDto> LiveSelectedItems =>
        _liveQueue?.Items.Where(item => _liveSelected.Contains(item.ProductId)).ToArray() ?? [];
    private long? _liveDepartmentId;
    private long? _liveSeriesId;
    private long? _liveCategoryId;
    private long? _liveTypeId;
    private long? _livePriorityId;
    private ProductionTaskPageDto? _liveTasks;
    private ProductionTaskDetailDto? _liveTask;
    private string? _liveTaskError;
    private bool _liveTaskLoading;
    private int _liveTaskPage = 1;
    private ProductionDocumentPageDto? _liveDocuments;
    private string? _liveDocumentsError;
    private bool _liveDocumentsLoading;
    private int _liveDocumentsPage = 1;
    private ProductionProductHistoryDto? _liveHistory;
    private string? _liveHistoryError;
    private bool _liveHistoryLoading;
    private ProductionRepairPageDto? _liveRepairs;
    private int? _liveRepairId;
    private string? _liveRepairError;
    private bool _liveRepairLoading;
    private int _liveRepairPage = 1;
    private ProductionRepairDto? LiveRepair =>
        _liveRepairs?.Items.FirstOrDefault(x => x.Id == _liveRepairId);
    private sealed record LiveHistoryEvent(DateTime? Date, string Label, string Detail, int? TaskId);
    private IEnumerable<LiveHistoryEvent> LiveHistoryEvents
    {
        get
        {
            if (_liveHistory is null) return [];
            var documents = _liveHistory.Documents.Select(d => new LiveHistoryEvent(
                d.Date, d.Number,
                string.Join(" · ", new[] { d.Operation,
                    d.Before is not null || d.After is not null ? $"{d.Before} → {d.After}" : null }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                d.Number.StartsWith("GU", StringComparison.OrdinalIgnoreCase) ? d.Id : null));
            var repairs = _liveHistory.Repairs.Select(r => new LiveHistoryEvent(
                r.Registered, $"{L("Repair", "Remontas")} {r.Id}",
                string.Join(" · ", new[] { r.Reason, r.Claimed, r.Actual }
                    .Where(x => !string.IsNullOrWhiteSpace(x))), null));
            return documents.Concat(repairs).OrderBy(x => x.Date).ToArray();
        }
    }
    private List<ProductionReportCatalogDto> _liveReportCatalog = [];
    private ProductionReportDataDto? _liveReportData;
    private int? _liveReportId;
    private string? _liveReportError;
    private bool _liveReportLoading;

    protected override async Task OnInitializedAsync()
    {
        if (LiveQueueEnabled)
        {
            await LoadLiveFacetsAsync();
            await LoadLiveQueueAsync(1);
        }
    }

    private async Task LoadLiveFacetsAsync()
    {
        try
        {
            var result = await ShopfloorApi.GetProductionQueueFacetsAsync(CancellationToken.None);
            if (result.Success && result.Value is not null) _liveFacets = result.Value;
            else _liveFacetError = result.Error ?? L("Could not load queue filters.", "Nepavyko įkelti eilės filtrų.");
        }
        catch (Exception)
        {
            _liveFacetError = L("Could not load queue filters.", "Nepavyko įkelti eilės filtrų.");
        }
    }

    private async Task LoadLiveQueueAsync(int page)
    {
        if (page < 1 || _liveQueueLoading) return;
        _liveQueueLoading = true;
        _liveSelected.Clear();
        _liveQueue = null;
        _liveQueueError = null;
        try
        {
            var result = await ShopfloorApi.GetProductionQueueAsync(page, _fromSales,
                _liveDepartmentId, _liveSeriesId, _liveCategoryId, _liveTypeId,
                _livePriorityId, CancellationToken.None);
            if (result.Success && result.Value is not null)
            {
                _liveQueue = result.Value;
                _liveQueuePage = result.Value.Page;
            }
            else _liveQueueError = result.Error ?? L("Could not load the production queue.", "Nepavyko įkelti gamybos eilės.");
        }
        catch (Exception)
        {
            _liveQueueError = L("Could not load the production queue.", "Nepavyko įkelti gamybos eilės.");
        }
        finally { _liveQueueLoading = false; }
    }

    private void ToggleLiveSelected(long productId)
    {
        if (_liveQueue?.Items.Any(item => item.ProductId == productId) != true) return;
        if (!_liveSelected.Add(productId)) _liveSelected.Remove(productId);
    }

    private void SelectAllLiveOnPage()
    {
        if (_liveQueue is null) return;
        if (_liveQueue.Items.Count > 0 && _liveQueue.Items.All(item => _liveSelected.Contains(item.ProductId)))
            _liveSelected.Clear();
        else
            foreach (var item in _liveQueue.Items) _liveSelected.Add(item.ProductId);
    }

    private void SelectFirstLiveOnPage()
    {
        _liveSelected.Clear();
        foreach (var item in (_liveQueue?.Items ?? []).Take(Math.Clamp(_liveFirstN, 1, 100)))
            _liveSelected.Add(item.ProductId);
    }

    private async Task ClearLiveOrderFilterAsync()
    {
        _fromSales = null;
        await LoadLiveQueueAsync(1);
    }

    private async Task SetLiveQueueFilterAsync(string kind, long? id)
    {
        switch (kind)
        {
            case "branch": _liveDepartmentId = id; _liveSeriesId = null; break;
            case "series": _liveSeriesId = id; break;
            case "category": _liveCategoryId = id; _liveTypeId = null; break;
            case "type": _liveTypeId = id; break;
            case "priority": _livePriorityId = id; break;
        }
        await LoadLiveQueueAsync(1);
    }

    private Task SetLiveSelectAsync(string kind, ChangeEventArgs args)
        => SetLiveQueueFilterAsync(kind,
            long.TryParse(args.Value?.ToString(), out var id) ? id : null);

    private async Task GoLiveAwareAsync(string screen)
    {
        Go(screen);
        if (LiveQueueEnabled && screen == "tasks" && _liveTasks is null)
            await LoadLiveTasksAsync(1);
        if (LiveQueueEnabled && screen == "gt" && _liveDocuments is null)
            await LoadLiveDocumentsAsync(1);
        if (LiveQueueEnabled && screen == "stops" && _stopTab == "repairs" && _liveRepairs is null)
            await LoadLiveRepairsAsync(1);
        if (LiveQueueEnabled && screen == "reports"
            && (_liveReportCatalog.Count == 0 || _liveReportError is not null))
            await LoadLiveReportCatalogAsync();
    }

    private async Task LoadLiveTasksAsync(int page)
    {
        if (page < 1 || _liveTaskLoading) return;
        _liveTaskLoading = true;
        _liveTasks = null;
        _liveTaskError = null;
        try
        {
            var result = await ShopfloorApi.GetProductionTasksAsync(page, CancellationToken.None);
            if (result.Success && result.Value is not null)
            {
                _liveTasks = result.Value;
                _liveTaskPage = result.Value.Page;
            }
            else _liveTaskError = result.Error ?? L("Could not load production tasks.", "Nepavyko įkelti gamybos užduočių.");
        }
        catch (Exception)
        {
            _liveTaskError = L("Could not load production tasks.", "Nepavyko įkelti gamybos užduočių.");
        }
        finally { _liveTaskLoading = false; }
    }

    private async Task LoadLiveDocumentsAsync(int page)
    {
        if (page < 1 || _liveDocumentsLoading) return;
        _liveDocumentsLoading = true;
        _liveDocuments = null;
        _liveDocumentsError = null;
        try
        {
            var result = await ShopfloorApi.GetProductionDocumentsAsync(page, CancellationToken.None);
            if (result.Success && result.Value is not null)
            {
                _liveDocuments = result.Value;
                _liveDocumentsPage = result.Value.Page;
            }
            else _liveDocumentsError = result.Error ?? L("Could not load GT documents.", "Nepavyko įkelti GT dokumentų.");
        }
        catch (Exception)
        {
            _liveDocumentsError = L("Could not load GT documents.", "Nepavyko įkelti GT dokumentų.");
        }
        finally { _liveDocumentsLoading = false; }
    }

    private async Task OpenLiveTaskAsync(int id)
    {
        _liveTask = null;
        _liveTaskError = null;
        Go("task");
        _liveTaskLoading = true;
        try
        {
            var result = await ShopfloorApi.GetProductionTaskAsync(id, CancellationToken.None);
            if (result.Success && result.Value is not null) _liveTask = result.Value;
            else _liveTaskError = result.Error ?? L("Task unavailable or access denied.", "Užduotis neprieinama arba neturite teisių.");
        }
        catch (Exception)
        {
            _liveTaskError = L("Could not load the task.", "Nepavyko įkelti užduoties.");
        }
        finally { _liveTaskLoading = false; }
    }

    private async Task HistorySearchKeyAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter" && LiveQueueEnabled) await LoadLiveHistoryAsync();
    }

    private async Task LoadLiveHistoryAsync()
    {
        var barcode = _historyBarcode.Trim();
        _liveHistory = null;
        _liveHistoryError = null;
        if (barcode.Length != 12 || barcode.Any(c => c is < '0' or > '9'))
        {
            _liveHistoryError = L("Enter a 12-digit barcode.", "Įveskite 12 skaitmenų kodą.");
            return;
        }
        if (_liveHistoryLoading) return;
        _liveHistoryLoading = true;
        try
        {
            var result = await ShopfloorApi.GetProductionHistoryAsync(barcode, CancellationToken.None);
            if (result.Success && result.Value is not null) _liveHistory = result.Value;
            else _liveHistoryError = result.Error ?? L("Product not found or access denied.", "Gaminys nerastas arba neturite teisių.");
        }
        catch (Exception)
        {
            _liveHistoryError = L("Could not load product history.", "Nepavyko įkelti gaminio istorijos.");
        }
        finally { _liveHistoryLoading = false; }
    }

    private async Task OpenLiveQueueHistoryAsync(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        _historyBarcode = barcode;
        Go("history");
        await LoadLiveHistoryAsync();
    }

    private async Task LoadLiveRepairsAsync(int page)
    {
        if (page < 1 || _liveRepairLoading) return;
        _liveRepairLoading = true;
        _liveRepairs = null;
        _liveRepairError = null;
        try
        {
            var result = await ShopfloorApi.GetProductionRepairsAsync(page, _repairFilter, CancellationToken.None);
            if (result.Success && result.Value is not null)
            {
                _liveRepairs = result.Value;
                _liveRepairPage = result.Value.Page;
                if (!result.Value.Items.Any(x => x.Id == _liveRepairId))
                    _liveRepairId = result.Value.Items.FirstOrDefault()?.Id;
            }
            else _liveRepairError = result.Error ?? L("Could not load repairs.", "Nepavyko įkelti remontų.");
        }
        catch (Exception)
        {
            _liveRepairError = L("Could not load repairs.", "Nepavyko įkelti remontų.");
        }
        finally { _liveRepairLoading = false; }
    }

    private async Task SetLiveRepairFilterAsync(string filter)
    {
        _repairFilter = filter;
        await LoadLiveRepairsAsync(1);
    }

    private async Task OpenLiveRepairsAsync()
    {
        OpenRepairs();
        if (LiveQueueEnabled && _liveRepairs is null) await LoadLiveRepairsAsync(1);
    }

    private async Task SetRepairTabAsync()
    {
        _stopTab = "repairs";
        if (LiveQueueEnabled && _liveRepairs is null) await LoadLiveRepairsAsync(1);
    }

    private async Task OpenLiveReportsAsync()
    {
        Go("reports");
        if (_liveReportCatalog.Count == 0) await LoadLiveReportCatalogAsync();
    }

    private async Task LoadLiveReportCatalogAsync()
    {
        _liveReportError = null;
        try
        {
            var result = await ShopfloorApi.GetProductionReportCatalogAsync(CancellationToken.None);
            if (result.Success && result.Value is not null)
                _liveReportCatalog = result.Value;
            else _liveReportError = result.Error ?? L("Could not load report catalog.", "Nepavyko įkelti ataskaitų sąrašo.");
        }
        catch (Exception)
        {
            _liveReportError = L("Could not load report catalog.", "Nepavyko įkelti ataskaitų sąrašo.");
        }
    }

    private async Task SelectLiveReportAsync(int reportId)
    {
        if (_liveReportLoading || (_liveReportId == reportId && _liveReportData is not null)) return;
        _liveReportId = reportId;
        _liveReportData = null;
        _liveReportError = null;
        if (_liveTask is null) return;
        _liveReportLoading = true;
        try
        {
            var result = await ShopfloorApi.GetProductionReportDataAsync(
                _liveTask.Task.Id, reportId, CancellationToken.None);
            if (result.Success && result.Value is not null) _liveReportData = result.Value;
            else _liveReportError = result.Error ?? L("Report unavailable or access denied.", "Ataskaita neprieinama arba neturite teisių.");
        }
        catch (Exception)
        {
            _liveReportError = L("Could not load report data.", "Nepavyko įkelti ataskaitos duomenų.");
        }
        finally { _liveReportLoading = false; }
    }
}
