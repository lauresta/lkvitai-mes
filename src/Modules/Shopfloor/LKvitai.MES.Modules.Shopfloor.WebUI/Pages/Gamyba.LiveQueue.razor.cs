using LKvitai.MES.Modules.Shopfloor.WebUI.Services;
using Microsoft.AspNetCore.Components;

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
}
