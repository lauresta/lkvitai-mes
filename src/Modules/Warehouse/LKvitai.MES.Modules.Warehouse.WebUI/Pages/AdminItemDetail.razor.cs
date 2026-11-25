using LKvitai.MES.Modules.Warehouse.WebUI.Components.ItemCard;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Pages;

/// <summary>
/// Item card (variant 1b): header with stock bar and price / min edits, tabs (?tab=), and a rail with recent
/// movements, incoming and reserved quantities. Every block loads on its own and shows its own error.
/// </summary>
public partial class AdminItemDetail
{
    private const string BarcodeField = "barcode";
    private const int MovementsPageSize = 500;
    private const int StockPageSize = 200;
    private const int SupplierPageSize = 100;
    private static readonly TimeSpan MovementHistory = TimeSpan.FromDays(730);

    private static readonly DialogOptions EditDialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
        CloseButton = true
    };

    private static readonly DialogOptions StockDialogOptions = new()
    {
        MaxWidth = MaxWidth.ExtraSmall,
        FullWidth = true,
        CloseButton = false
    };

    private readonly PanelState<ItemDetailsDto> _item = new();
    private readonly PanelState<IReadOnlyList<AvailableStockItemDto>> _stock = new();
    private readonly PanelState<StockMovementsResponseDto> _movements = new();
    private readonly PanelState<IReadOnlyList<ItemReservationRowDto>> _reservations = new();
    private readonly PanelState<IReadOnlyList<ItemIncomingRowDto>> _incoming = new();
    private readonly PanelState<IReadOnlyList<OnHandValueRowDto>> _onHand = new();
    private readonly PanelState<IReadOnlyList<ItemPriceOverrideDto>> _overrides = new();
    private readonly PanelState<IReadOnlyList<AdminSupplierMappingDto>> _suppliers = new();
    private readonly PanelState<IReadOnlyList<ItemPriceHistoryDto>> _priceHistory = new();
    private readonly PanelState<IReadOnlyList<CostHistoryRowDto>> _purchaseHistory = new();

    private int? _loadedId;
    private IReadOnlyList<AvailableStockItemDto> _stockRows = Array.Empty<AvailableStockItemDto>();
    private ItemStockSummary? _stockSummary;
    private ItemIncomingSummary? _incomingSummary;
    private IReadOnlyList<ItemMovement> _timeline = Array.Empty<ItemMovement>();

    [Inject] private MasterDataAdminClient AdminClient { get; set; } = default!;
    [Inject] private ValuationClient ValuationClient { get; set; } = default!;
    [Inject] private UomClient UomClient { get; set; } = default!;
    [Inject] private StockClient StockClient { get; set; } = default!;
    [Inject] private ReportsClient ReportsClient { get; set; } = default!;
    [Inject] private ReservationsClient ReservationsClient { get; set; } = default!;
    [Inject] private ReceivingClient ReceivingClient { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter] public int Id { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? TabQuery { get; set; }

    private ItemDetailsDto? Item => _item.Value;

    private string Tab => ItemCardTabs.Parse(TabQuery);

    private OnHandValueRowDto? OnHandRow => _onHand.Value?.FirstOrDefault();

    private int MovementTotal => _movements.Value?.TotalCount ?? _timeline.Count;

    private bool CanMove => _stockRows.Any(x => x.LocationId.HasValue && x.AvailableQty > 0m);

    private bool CanAdjust => _stockRows.Any(x => x.LocationId.HasValue);

    private string Show3DHref => ItemStockMath.PrimaryStorageLocation(_stockRows) is { } location
        ? $"visualization/3d?location={Uri.EscapeDataString(location)}"
        : "visualization/3d";

    private IReadOnlyDictionary<string, int?> TabCounts => new Dictionary<string, int?>
    {
        [ItemCardTabs.Locations] = _stock.HasValue ? _stockSummary?.LocationCount : null,
        [ItemCardTabs.Movements] = _movements.HasValue ? MovementTotal : null,
        [ItemCardTabs.Photos] = Item?.Photos.Count
    };

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedId == Id)
        {
            return;
        }

        if (_loadedId is not null)
        {
            ResetPanels();
        }

        _loadedId = Id;
        await ReloadAllAsync();
    }

    /// <summary>Another item was opened in the same page instance: drop every block of the previous one.</summary>
    private void ResetPanels()
    {
        IPanelState[] panels =
        [
            _item, _stock, _movements, _reservations, _incoming, _onHand, _overrides, _suppliers, _priceHistory, _purchaseHistory
        ];

        foreach (var panel in panels)
        {
            panel.Reset();
        }

        UpdateDerived();
    }

    private async Task ReloadAllAsync()
    {
        await _item.LoadAsync(() => AdminClient.GetItemByIdAsync(Id));
        if (Item is null)
        {
            return;
        }

        StateHasChanged();
        await Task.WhenAll(
            LoadStockAsync(),
            LoadMovementsAsync(),
            LoadReservationsAsync(),
            LoadIncomingAsync(),
            LoadOnHandAsync(),
            LoadOverridesAsync(),
            LoadSuppliersAsync(),
            LoadPriceHistoryAsync(),
            LoadPurchaseHistoryAsync());
    }

    private async Task ReloadItemAsync()
    {
        await LoadPanelAsync(_item, () => AdminClient.GetItemByIdAsync(Id));
    }

    private Task LoadStockAsync()
        => LoadPanelAsync(_stock, async () => (await StockClient.GetItemStockAsync(Id, StockPageSize)).Items);

    private Task LoadMovementsAsync()
        => LoadPanelAsync(_movements, () => ReportsClient.GetStockMovementsAsync(
            DateTimeOffset.UtcNow - MovementHistory, null, Id, null, null, null, 1, MovementsPageSize));

    private Task LoadReservationsAsync()
        => LoadPanelAsync(_reservations, () => ReservationsClient.GetByItemAsync(Id));

    private Task LoadIncomingAsync()
        => LoadPanelAsync(_incoming, () => ReceivingClient.GetIncomingByItemAsync(Id));

    private Task LoadOnHandAsync()
        => LoadPanelAsync(_onHand, () => ValuationClient.GetOnHandValueAsync(itemId: Id));

    private Task LoadOverridesAsync()
        => LoadPanelAsync(_overrides, () => AdminClient.GetItemPriceOverridesAsync(Id));

    private Task LoadSuppliersAsync()
        => LoadPanelAsync<IReadOnlyList<AdminSupplierMappingDto>>(_suppliers, async () =>
            (await AdminClient.GetSupplierMappingsAsync(null, null, Id, 1, SupplierPageSize)).Items
            .OrderBy(x => x.SupplierName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SupplierSKU, StringComparer.OrdinalIgnoreCase)
            .ToList());

    private Task LoadPriceHistoryAsync()
        => LoadPanelAsync(_priceHistory, () => AdminClient.GetItemPriceHistoryAsync(Id));

    private Task LoadPurchaseHistoryAsync()
        => LoadPanelAsync<IReadOnlyList<CostHistoryRowDto>>(_purchaseHistory, async () =>
            (await ValuationClient.GetCostHistoryAsync(itemId: Id)).OrderByDescending(x => x.ChangedAt).ToList());

    private async Task LoadPanelAsync<T>(PanelState<T> state, Func<Task<T>> loader)
    {
        var load = state.LoadAsync(loader);
        StateHasChanged();
        await load;
        UpdateDerived();
        StateHasChanged();
    }

    /// <summary>Recomputes the values shared by the header, the tabs and the rail after any block reloads.</summary>
    private void UpdateDerived()
    {
        _stockRows = _stock.Value is null ? Array.Empty<AvailableStockItemDto>() : ItemStockMath.StockRows(_stock.Value);
        _stockSummary = _stock.HasValue ? ItemStockSummary.From(_stockRows) : null;
        _incomingSummary = _incoming.Value is null ? null : ItemIncomingSummary.From(_incoming.Value.ToList());
        _timeline = _movements.Value is null
            ? Array.Empty<ItemMovement>()
            : ItemMovementTimeline.Build(_movements.Value.Movements, _stockSummary?.Physical);
    }

    private void SelectTab(string tab)
    {
        var uri = NavigationManager.GetUriWithQueryParameter("tab", ItemCardTabs.ToQuery(tab));
        NavigationManager.NavigateTo(uri, replace: true);
    }

    private Task<bool> SaveBasePriceAsync(decimal? value)
        => Item is null ? Task.FromResult(false) : SaveItemAsync(ItemCardRequests.WithBasePrice(Item, value));

    private Task<bool> SaveMinQtyAsync(decimal? value)
        => Item is null ? Task.FromResult(false) : SaveItemAsync(ItemCardRequests.WithMinQty(Item, value));

    private async Task<bool> SaveItemAsync(CreateOrUpdateItemRequest request)
    {
        try
        {
            await AdminClient.UpdateItemAsync(Id, request);
            ToastService.ShowSuccess(L["AdminItems.Toast.ItemUpdated"].Value);
            await ReloadItemAsync();
            return true;
        }
        catch (ApiException ex)
        {
            ToastService.ShowError(ItemCardErrors.Describe(L, ex));
            return false;
        }
    }

    private Task OpenMoveDialogAsync(int? locationId)
        => OpenStockDialogAsync<ItemMoveDialog>(L["AdminItemDetail.MoveDialog.Title"].Value, locationId);

    private Task OpenAdjustDialogAsync(int? locationId)
        => OpenStockDialogAsync<ItemAdjustDialog>(L["AdminItemDetail.AdjustDialog.Title"].Value, locationId);

    /// <summary>Opens a stock dialog with the item's rows; a successful move / adjustment reloads stock and movements.</summary>
    private async Task OpenStockDialogAsync<TDialog>(string title, int? locationId)
        where TDialog : ComponentBase
    {
        if (Item is null)
        {
            return;
        }

        var parameters = new DialogParameters
        {
            ["Item"] = Item,
            ["Rows"] = _stockRows,
            ["PreselectedLocationId"] = locationId
        };

        var dialog = await DialogService.ShowAsync<TDialog>(title, parameters, StockDialogOptions);
        var result = await dialog.Result;
        if (result.Canceled)
        {
            return;
        }

        await Task.WhenAll(LoadStockAsync(), LoadMovementsAsync());
    }

    /// <summary>Opens the full item editor; <paramref name="focusField"/> "barcode" focuses the barcode field.</summary>
    private async Task OpenEditDialogAsync(string? focusField)
    {
        if (Item is null)
        {
            return;
        }

        try
        {
            var categoriesTask = AdminClient.GetCategoriesAsync();
            var unitsTask = UomClient.GetUnitsAsync(null, null);
            var tagsTask = AdminClient.GetItemTagsAsync(limit: 100);
            await Task.WhenAll(categoriesTask, unitsTask, tagsTask);

            var parameters = new DialogParameters
            {
                ["IsEdit"] = true,
                ["Item"] = Item,
                ["FocusField"] = focusField,
                ["Categories"] = categoriesTask.Result.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToList(),
                ["Units"] = unitsTask.Result.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToList(),
                ["ExistingTags"] = tagsTask.Result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
            };

            var dialog = await DialogService.ShowAsync<AdminItemEditorDialog>(L["AdminItems.EditItem"].Value, parameters, EditDialogOptions);
            var result = await dialog.Result;
            if (result.Canceled || result.Data is not CreateOrUpdateItemRequest request)
            {
                return;
            }

            await SaveItemAsync(request);
        }
        catch (ApiException ex)
        {
            ToastService.ShowError(ItemCardErrors.Describe(L, ex));
        }
    }
}
