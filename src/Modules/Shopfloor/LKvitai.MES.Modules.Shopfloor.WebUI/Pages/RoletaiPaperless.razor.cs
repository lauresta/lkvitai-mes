using System.Globalization;
using System.Text.Json;
using LKvitai.MES.Modules.Shopfloor.WebUI.Resources;
using LKvitai.MES.Modules.Shopfloor.WebUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Pages;

public sealed record RoletaiGroup(string Key, List<RoletaiPaperlessRowDto> Rows);

public class RoletaiPaperlessBase : ComponentBase, IDisposable
{
    [Inject] protected ShopfloorApiClient Api { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected AuthenticationStateProvider Authentication { get; set; } = default!;
    [Inject] protected IStringLocalizer<ShopfloorResources> Localizer { get; set; } = default!;
    [Inject] protected RoletaiSignOffNotifier Notifier { get; set; } = default!;

    protected static readonly string[] ViewKeys = ["Paper", "Fabric", "Type", "Guides"];
    protected static readonly string[] FilterModes = ["All", "Marked", "Unmarked"];
    protected RoletaiPaperlessDataDto? _data;
    protected string? _error;
    protected bool _loading = true;
    protected bool _showFilter;
    protected string _view = "Paper";
    protected string _filterMode = "All";
    protected HashSet<int> _selectedSteps = [];
    protected bool _filterSigned;
    protected bool _hideMine;
    protected string _searchInput = "";
    protected string _searchedOrder = "";
    protected string _fromText = "";
    protected string _toText = "";
    protected string _workerName = "";
    protected string _initials = "LK";
    protected string _saveState = "";
    protected RoletaiPaperlessRowDto? _dialogRow;
    protected bool _withdraw;
    protected bool _signing;
    protected string? _dialogError;
    private readonly Dictionary<(long Id, int Step), bool> _pending = [];
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _started;
    private string _storageKey = "lkvitai.shopfloor.roletai.paperless";
    private Guid _notificationKey;

    protected bool IsSearching => !string.IsNullOrWhiteSpace(_searchedOrder);
    protected bool HasPendingMarks => _pending.Count > 0;
    protected bool HasMarkFilter => IsSearching || ActiveFilterCount > 0;
    protected int ActiveFilterCount => (_filterMode == "All" ? 0 : _selectedSteps.Count + (_filterSigned ? 1 : 0)) + (_hideMine ? 1 : 0) + (IsDefaultDate ? 0 : 1);
    private bool IsDefaultDate => _fromText == VilniusToday().AddDays(-14).ToString("yyyy-MM-dd") &&
        _toText == VilniusToday().ToString("yyyy-MM-dd");
    protected bool IsDefaultDateRange => IsDefaultDate;
    protected string FilterSummary
    {
        get
        {
            var parts = new List<string>();
            if (_filterMode != "All" && (_selectedSteps.Count > 0 || _filterSigned))
                parts.Add(Localizer["Roletai.Paperless.FilterSummary", Localizer[$"Roletai.Paperless.Filter.{_filterMode}"].Value,
                    string.Join(", ", _selectedSteps.Order().Select(x => Localizer["Roletai.Paperless.Step", x].Value)
                        .Concat(_filterSigned ? [Localizer["Roletai.Paperless.Signed"].Value] : []))].Value);
            if (_hideMine) parts.Add(Localizer["Roletai.Paperless.HideMine"].Value);
            if (IsSearching) parts.Add(Localizer["Roletai.Paperless.SearchSummary", _searchedOrder].Value);
            if (!IsSearching && !IsDefaultDate) parts.Add(Localizer["Roletai.Paperless.DateSummary", _fromText, _toText].Value);
            return string.Join(" · ", parts);
        }
    }
    protected string FilterExplanation => _filterMode switch
    {
        "Marked" => Localizer["Roletai.Paperless.ExplainMarked"].Value,
        "Unmarked" => Localizer["Roletai.Paperless.ExplainUnmarked"].Value,
        _ => Localizer["Roletai.Paperless.ExplainAll"].Value
    };

    protected List<RoletaiPaperlessRowDto> VisibleRows => (_data?.Rows ?? [])
        .Where(row => RoletaiPaperlessFilter.Matches(_filterMode, _selectedSteps, _filterSigned,
            _hideMine, (_data?.MyMarks ?? []).Where(x => x.ProductId == row.ParduotoGaminioID && x.Marked)
                .Select(x => x.Step).ToHashSet(),
            _data?.SignOffs.Any(x => x.ProductId == row.ParduotoGaminioID) == true,
            MineSigned(row.ParduotoGaminioID))).ToList();

    protected List<RoletaiGroup> GroupedRows
    {
        get
        {
            IEnumerable<IGrouping<string, RoletaiPaperlessRowDto>> groups = _view switch
            {
                "Fabric" => VisibleRows.GroupBy(x => string.IsNullOrWhiteSpace(x.Spalva) ? "—" : x.Spalva),
                "Type" => VisibleRows.GroupBy(x => x.TipoTrPavadinimas),
                _ => VisibleRows.GroupBy(x => string.IsNullOrWhiteSpace(x.KrepianciujuTipas) || x.KrepianciujuTipas == "0"
                    ? Localizer["Roletai.Paperless.NoGuides"].Value : x.KrepianciujuTipas)
            };
            groups = _view == "Guides"
                ? groups.OrderBy(x => x.Key == Localizer["Roletai.Paperless.NoGuides"].Value).ThenBy(x => x.Key)
                : groups.OrderBy(x => x.Key);
            return groups.Select(group => new RoletaiGroup(group.Key, (_view switch
            {
                "Fabric" => group.OrderByDescending(x => x.Audinys),
                "Type" => group.OrderBy(x => x.Uzsakymas).ThenBy(x => x.ParduotoGaminioID),
                _ => group.OrderByDescending(x => DecimalOrZero(x.Kreipianciosios))
            }).ToList())).ToList();
        }
    }

    protected bool MineSigned(long productId) => _data?.SignOffs.Any(x => x.ProductId == productId && x.Mine) == true;
    private bool IsMarked(long productId, int step) => _data?.MyMarks.Any(x => x.ProductId == productId && x.Step == step && x.Marked) == true;
    private static decimal DecimalOrZero(string? value) => decimal.TryParse(value, NumberStyles.Any,
        CultureInfo.InvariantCulture, out var result) ? result : 0;
    protected decimal GroupConsumption(RoletaiGroup group) => group.Rows.Sum(x => DecimalOrZero(x.AudinioSunaudojimas));
    private static DateOnly VilniusToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius")));
    protected static string VilniusTime(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius")).ToString("HH:mm");
    protected static string VilniusNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius")).ToString("yyyy-MM-dd HH:mm:ss");

    protected override async Task OnInitializedAsync()
    {
        _notificationKey = Notifier.Subscribe(productId =>
        {
            if (_data?.Rows.Any(x => x.ParduotoGaminioID == productId) == true)
                _ = InvokeAsync(() => LoadAsync(true));
        });
        var today = VilniusToday();
        _fromText = today.AddDays(-14).ToString("yyyy-MM-dd");
        _toText = today.ToString("yyyy-MM-dd");
        var auth = await Authentication.GetAuthenticationStateAsync();
        _workerName = auth.User.Identity?.Name ?? Localizer["Roletai.Paperless.Worker"].Value;
        _initials = string.Concat(_workerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2)
            .Select(x => char.ToUpperInvariant(x[0])));
        if (_initials.Length == 0) _initials = "LK";
        _storageKey += "." + (auth.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? _workerName);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _started) return;
        _started = true;
        try
        {
            var json = await JS.InvokeAsync<string?>("localStorage.getItem", _storageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var saved = JsonSerializer.Deserialize<Preferences>(json);
                if (saved is not null)
                {
                    if (ViewKeys.Contains(saved.View)) _view = saved.View;
                    if (FilterModes.Contains(saved.Mode)) _filterMode = saved.Mode;
                    _selectedSteps = saved.Steps?.Where(x => x is >= 1 and <= 3).ToHashSet() ?? [];
                    _filterSigned = saved.Signed;
                    _hideMine = saved.HideMine;
                }
            }
        }
        catch (JSException) { }
        await LoadAsync(false);
        _ = PollAsync();
    }

    protected async Task LoadAsync(bool polling)
    {
        if (!polling) _loading = true;
        try
        {
            var from = DateOnly.TryParseExact(_fromText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedFrom) ? parsedFrom : (DateOnly?)null;
            var to = DateOnly.TryParseExact(_toText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedTo) ? parsedTo : (DateOnly?)null;
            var response = await Api.GetRoletaiPaperlessAsync(IsSearching ? _searchedOrder : null,
                from, to, _lifetime.Token);
            if (response.Success && response.Value is not null)
            {
                _data = response.Value;
                foreach (var ((id, step), marked) in _pending)
                {
                    var old = _data.MyMarks.FirstOrDefault(x => x.ProductId == id && x.Step == step);
                    if (old is not null) _data.MyMarks.Remove(old);
                    _data.MyMarks.Add(new() { ProductId = id, Step = step, Marked = marked });
                }
                _error = null;
            }
            else _error = response.Error ?? Localizer["Roletai.Paperless.LoadFailed"].Value;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception) { _error = Localizer["Roletai.Paperless.LoadFailed"].Value; }
        finally { _loading = false; await InvokeAsync(StateHasChanged); }
    }

    private async Task PollAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(25));
            while (await timer.WaitForNextTickAsync(_lifetime.Token))
                await InvokeAsync(() => LoadAsync(true));
        }
        catch (OperationCanceledException) { }
    }

    protected async Task SearchAsync()
    {
        _searchedOrder = _searchInput.Trim();
        _showFilter = false;
        await LoadAsync(false);
    }
    protected async Task ClearSearchAsync()
    {
        _searchInput = _searchedOrder = "";
        await LoadAsync(false);
    }
    protected async Task ApplyFilterAsync()
    {
        if (!IsSearching && (!DateOnly.TryParseExact(_fromText,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var from)
            || !DateOnly.TryParseExact(_toText,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var to)
            || from > to))
        { _error = Localizer["Roletai.Paperless.InvalidDates"].Value; return; }
        _showFilter = false;
        await SavePreferencesAsync();
        await LoadAsync(false);
    }
    protected async Task ClearFiltersAsync()
    {
        _filterMode = "All"; _selectedSteps.Clear(); _filterSigned = _hideMine = false;
        _showFilter = false; _searchInput = _searchedOrder = "";
        var today = VilniusToday();
        _fromText = today.AddDays(-14).ToString("yyyy-MM-dd"); _toText = today.ToString("yyyy-MM-dd");
        await SavePreferencesAsync(); await LoadAsync(false);
    }
    protected async Task SetViewAsync(string view) { _view = view; await SavePreferencesAsync(); }
    protected void SetFilterMode(string mode) => _filterMode = mode;
    protected void ToggleFilterStep(int step)
    {
        if (!_selectedSteps.Add(step)) _selectedSteps.Remove(step);
        if (_filterMode == "All") _filterMode = "Marked";
    }
    protected void ToggleSignedFilter() { _filterSigned = !_filterSigned; if (_filterMode == "All") _filterMode = "Marked"; }
    protected void ToggleHideMine() => _hideMine = !_hideMine;
    private async Task SavePreferencesAsync()
    {
        try { await JS.InvokeVoidAsync("localStorage.setItem", _storageKey,
            JsonSerializer.Serialize(new Preferences(_view,_filterMode,_selectedSteps.ToArray(),_filterSigned,_hideMine))); }
        catch (JSException) { }
    }

    protected async Task ToggleMarkAsync((long ProductId, int Step) key)
    {
        if (_data is null) return;
        var marked = !IsMarked(key.ProductId,key.Step);
        var old = _data.MyMarks.FirstOrDefault(x => x.ProductId == key.ProductId && x.Step == key.Step);
        if (old is not null) _data.MyMarks.Remove(old);
        _data.MyMarks.Add(new() { ProductId=key.ProductId, Step=key.Step, Marked=marked });
        _pending[(key.ProductId,key.Step)] = marked;
        _saveState = "saving";
        await FlushMarksAsync();
    }
    protected Task RetryMarksAsync() => FlushMarksAsync();
    private async Task FlushMarksAsync()
    {
        if (!await _flushLock.WaitAsync(0)) return;
        try
        {
            while (_pending.Count > 0)
            {
                var pair = _pending.First();
                _pending.Remove(pair.Key);
                var result = await Api.SetRoletaiMarkAsync(pair.Key.Id,pair.Key.Step,pair.Value,_lifetime.Token);
                if (!result.Success)
                {
                    if (!_pending.ContainsKey(pair.Key)) _pending[pair.Key] = pair.Value;
                    _saveState = "error"; break;
                }
            }
            if (_pending.Count == 0)
            {
                _saveState = "saved";
                _ = HideSavedAsync();
            }
        }
        finally { _flushLock.Release(); await InvokeAsync(StateHasChanged); }
    }
    private async Task HideSavedAsync()
    {
        try { await Task.Delay(2200,_lifetime.Token); if (_saveState == "saved") { _saveState = ""; await InvokeAsync(StateHasChanged); } }
        catch (OperationCanceledException) { }
    }

    protected void OpenSignDialog(RoletaiPaperlessRowDto row)
    { _dialogError = null; _dialogRow = row; _withdraw = MineSigned(row.ParduotoGaminioID); }
    protected void CloseDialog() { _dialogRow = null; _dialogError = null; }
    protected async Task ConfirmDialogAsync()
    {
        if (_dialogRow is null) return;
        _signing = true;
        try
        {
            var id = _dialogRow.ParduotoGaminioID;
            if (_withdraw)
            {
                var response = await Api.WithdrawRoletaiAsync(id,_lifetime.Token);
                if (!response.Success) { _dialogError = Localizer["Roletai.Paperless.SignFailed"].Value; return; }
                _data?.SignOffs.RemoveAll(x => x.ProductId == id && x.Mine);
            }
            else
            {
                var response = await Api.SignRoletaiAsync(id,_lifetime.Token);
                if (!response.Success || response.Value is null) { _dialogError = Localizer["Roletai.Paperless.SignFailed"].Value; return; }
                _data?.SignOffs.RemoveAll(x => x.ProductId == id && x.Mine);
                _data?.SignOffs.Add(response.Value);
            }
            _dialogRow = null;
            Notifier.Publish(id);
            await LoadAsync(true);
        }
        finally { _signing = false; }
    }

    public void Dispose() { Notifier.Unsubscribe(_notificationKey); _lifetime.Cancel(); _lifetime.Dispose(); _flushLock.Dispose(); }
    private sealed record Preferences(string View, string Mode, int[]? Steps, bool Signed, bool HideMine);
}
