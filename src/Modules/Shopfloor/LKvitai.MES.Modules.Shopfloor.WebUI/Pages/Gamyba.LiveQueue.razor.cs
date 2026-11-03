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
    private string? _liveQueueError;
    private bool _liveQueueLoading;
    private int _liveQueuePage = 1;
    private ProductionTaskPageDto? _liveTasks;
    private ProductionTaskDetailDto? _liveTask;
    private string? _liveTaskError;
    private bool _liveTaskLoading;
    private int _liveTaskPage = 1;
    private ProductionProductHistoryDto? _liveHistory;
    private string? _liveHistoryError;
    private bool _liveHistoryLoading;

    protected override async Task OnInitializedAsync()
    {
        if (LiveQueueEnabled) await LoadLiveQueueAsync(1);
    }

    private async Task LoadLiveQueueAsync(int page)
    {
        if (page < 1 || _liveQueueLoading) return;
        _liveQueueLoading = true;
        _liveQueueError = null;
        try
        {
            var result = await ShopfloorApi.GetProductionQueueAsync(page, _fromSales, CancellationToken.None);
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

    private async Task ClearLiveOrderFilterAsync()
    {
        _fromSales = null;
        await LoadLiveQueueAsync(1);
    }

    private async Task GoLiveAwareAsync(string screen)
    {
        Go(screen);
        if (LiveQueueEnabled && screen == "tasks" && _liveTasks is null)
            await LoadLiveTasksAsync(1);
    }

    private async Task LoadLiveTasksAsync(int page)
    {
        if (page < 1 || _liveTaskLoading) return;
        _liveTaskLoading = true;
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
}
