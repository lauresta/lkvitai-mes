using System.Globalization;
using System.Text.Json;
using LKvitai.MES.Modules.Finance.WebUI.IncomesReport;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Finance.WebUI.Pages;

/// <summary>
/// Finance → Įplaukų judėjimas: money received (by payment date) or orders taken (by order date) for a period.
/// Header filters (period, dataset, branches, methods) drive the whole page; drill filters (chart, matrix, remarks,
/// quick toggles) only the table and its export. All of it lives in the URL.
/// </summary>
public partial class Incomes : IAsyncDisposable
{
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const int TableRows = 20;
    public const int RowHeight = 30;
    public const int ExcelProgressRows = 5000;
    public const int PlotHeight = 170;

    private static readonly Dictionary<string, string> MethodColors = new(StringComparer.Ordinal)
    {
        ["BANK"] = "var(--accent-500)", ["EKA"] = "var(--n-700)", ["PPK"] = "var(--sand-500)", ["KASA"] = "var(--n-300)",
    };
    private static readonly string[] BranchPalette =
        ["var(--accent-700)", "var(--accent-400)", "var(--n-700)", "var(--n-400)", "var(--sand-600)", "var(--sand-300)"];
    private const string OtherBranchColor = "var(--n-200)";
    public static IReadOnlyList<string> MethodOrder { get; } = ["BANK", "EKA", "PPK", "KASA"];

    private DotNetObjectReference<Incomes>? _reference;
    internal Virtualize<IncomesFlatItem>? Virtualizer { get; set; }
    private CancellationTokenSource _lifetime = new();

    internal IncomesUrlState State { get; private set; } = default!;
    internal IncomesSummaryDto? Summary { get; private set; }
    internal IncomesFailure? SummaryError { get; private set; }
    internal bool SummaryLoading { get; private set; } = true;
    internal IncomesRowSource? Rows { get; private set; }
    internal IncomesFailure? RowsError { get; private set; }
    internal bool RowsLoading { get; private set; } = true;
    internal IReadOnlyList<IncomesRowDto> PhoneRows { get; private set; } = [];
    internal string Viewport { get; private set; } = "desktop";
    internal string ChartBy { get; private set; } = "method";
    internal string? Bucket { get; private set; }
    internal bool Allowed { get; private set; }
    internal bool NoAccess { get; private set; }
    internal string? Toast { get; private set; }
    internal bool ExportOpen { get; private set; }
    internal bool CustomOpen { get; private set; }
    internal bool PhonePeriodOpen { get; private set; }
    internal string? OpenSelect { get; private set; }
    internal int PhoneCount { get; private set; } = 5;
    internal DateOnly Today { get; private set; } = DateOnly.FromDateTime(DateTime.Now);
    internal string UserName { get; private set; } = "";
    internal DateOnly CustomFrom { get; set; }
    internal DateOnly CustomTo { get; set; }
    private string _searchText = "";
    internal string SearchText => _searchText;
    private int _summaryGeneration;
    private int _rowsGeneration;
    private int _searchGeneration;
    private int _toastGeneration;
    private string _writtenQuery = "";

    internal bool IsMoney => !State.Orders;
    internal bool IsPhone => Viewport == "phone";
    internal bool IsTablet => Viewport == "tablet";
    internal string Unit => L[IsMoney ? "Incomes.UnitPayments" : "Incomes.UnitOrders"].Value;

    protected override async Task OnInitializedAsync()
    {
        var user = AuthenticationState is null ? null : (await AuthenticationState).User;
        Allowed = IncomesAccess.IsAllowed(user, Configuration);
        UserName = user?.FindFirst("display_name")?.Value ?? user?.Identity?.Name ?? "";
        State = IncomesUrlState.Parse(IncomesUrlState.QueryOf(Nav.Uri), Today);
        _searchText = State.Search;
        ChartBy = IsMoney ? "method" : "branch";
        _writtenQuery = State.ToQuery();
        Nav.LocationChanged += OnLocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !Allowed) return;
        _reference = DotNetObjectReference.Create(this);
        try { Viewport = await JS.InvokeAsync<string>("financeIncomes.watch", _lifetime.Token, _reference); }
        catch (JSException) { Viewport = "desktop"; }
        await LoadAllAsync();
    }

    [JSInvokable]
    public Task SetViewport(string viewport)
    {
        Viewport = viewport is "phone" or "tablet" ? viewport : "desktop";
        return InvokeAsync(async () =>
        {
            StateHasChanged();
            if (IsPhone) await LoadPhoneRowsAsync();
            else if (Virtualizer is not null) await Virtualizer.RefreshDataAsync();
        });
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        var path = Nav.ToBaseRelativePath(args.Location).Split('?', '#')[0].TrimEnd('/');
        if (!Allowed || !string.Equals(path, "incomes", StringComparison.OrdinalIgnoreCase)) return;
        var query = IncomesUrlState.Parse(IncomesUrlState.QueryOf(args.Location), Today).ToQuery();
        if (query == _writtenQuery) return;
        // Another link to this page (e.g. the shell search) — follow it.
        _ = InvokeAsync(async () =>
        {
            State = IncomesUrlState.Parse(IncomesUrlState.QueryOf(args.Location), Today);
            _searchText = State.Search;
            _writtenQuery = State.ToQuery();
            await LoadAllAsync();
        });
    }

    // ---- loading -------------------------------------------------------------------------------------------------

    private string ApiQuery => State.ToQuery() + (Bucket is null ? "" : "&bucket=" + Bucket);

    internal async Task LoadAllAsync()
    {
        await Task.WhenAll(LoadSummaryAsync(), LoadRowsAsync());
    }

    private async Task LoadSummaryAsync()
    {
        var generation = ++_summaryGeneration;
        SummaryLoading = true;
        SummaryError = null;
        StateHasChanged();
        try
        {
            var json = await JS.InvokeAsync<string>("financeIncomes.request", _lifetime.Token, "/summary?" + ApiQuery);
            var summary = JsonSerializer.Deserialize<IncomesSummaryDto>(json, Json);
            if (generation != _summaryGeneration) return;
            Summary = summary;
            if (summary is not null) Today = summary.Today;
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            if (generation != _summaryGeneration) return;
            var failure = Failure(ex, "/api/finance/incomes/summary");
            if (failure.Status == 403) NoAccess = true;
            else SummaryError = failure;
        }
        catch (Exception ex) when (ex is TaskCanceledException or JSDisconnectedException) { return; }
        finally
        {
            if (generation == _summaryGeneration)
            {
                SummaryLoading = false;
                StateHasChanged();
            }
        }
    }

    private async Task LoadRowsAsync()
    {
        var generation = ++_rowsGeneration;
        var query = State.ToQuery();
        var source = new IncomesRowSource((skip, take, ct) => FetchRowsAsync(query, skip, take, ct));
        RowsLoading = true;
        RowsError = null;
        StateHasChanged();
        try
        {
            await source.LoadAsync(_lifetime.Token);
            if (generation != _rowsGeneration) return;
            Rows = source;
            PhoneCount = 5;
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            if (generation != _rowsGeneration) return;
            var failure = Failure(ex, "/api/finance/incomes/rows");
            if (failure.Status == 403) NoAccess = true;
            else RowsError = failure;
            Rows = null;
        }
        catch (Exception ex) when (ex is TaskCanceledException or JSDisconnectedException) { return; }
        finally
        {
            if (generation == _rowsGeneration) RowsLoading = false;
        }
        StateHasChanged();
        try { await JS.InvokeVoidAsync("financeIncomes.scrollTableTop", _lifetime.Token); }
        catch (Exception ex) when (ex is JSException or TaskCanceledException) { /* no table yet, or the page is gone */ }
        if (Virtualizer is not null) await Virtualizer.RefreshDataAsync();
        if (IsPhone) await LoadPhoneRowsAsync();
    }

    internal Task ReloadRows() => LoadRowsAsync();

    private async Task<IncomesRowsPageDto> FetchRowsAsync(string query, int skip, int take, CancellationToken ct)
    {
        var json = await JS.InvokeAsync<string>("financeIncomes.request", ct,
            $"/rows?{query}&skip={skip.ToString(CultureInfo.InvariantCulture)}&take={take.ToString(CultureInfo.InvariantCulture)}");
        return JsonSerializer.Deserialize<IncomesRowsPageDto>(json, Json) ?? throw new JsonException("Empty rows page.");
    }

    internal async ValueTask<ItemsProviderResult<IncomesFlatItem>> ProvideRows(ItemsProviderRequest request)
    {
        var source = Rows;
        if (source?.Meta is null) return new ItemsProviderResult<IncomesFlatItem>([], 0);
        try
        {
            var items = await source.RangeAsync(request.StartIndex, request.Count, request.CancellationToken);
            return new ItemsProviderResult<IncomesFlatItem>(items, source.FlatCount);
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            RowsError = Failure(ex, "/api/finance/incomes/rows");
            await InvokeAsync(StateHasChanged);
            return new ItemsProviderResult<IncomesFlatItem>([], 0);
        }
    }

    private async Task LoadPhoneRowsAsync()
    {
        if (Rows is null) { PhoneRows = []; return; }
        try { PhoneRows = await Rows.FirstAsync(PhoneCount, _lifetime.Token); }
        catch (Exception ex) when (ex is JSException or JsonException) { RowsError = Failure(ex, "/api/finance/incomes/rows"); }
        StateHasChanged();
    }

    internal async Task ShowMorePhone()
    {
        PhoneCount += 10;
        await LoadPhoneRowsAsync();
    }

    private static IncomesFailure Failure(Exception error, string path)
    {
        var message = error.Message;
        var start = message.IndexOf('{');
        var end = message.IndexOf('}', Math.Max(0, start));
        if (start >= 0 && end > start)
        {
            try
            {
                if (JsonSerializer.Deserialize<IncomesFailure>(message[start..(end + 1)], Json) is { } failure) return failure;
            }
            catch (JsonException) { /* fall through */ }
        }
        return new IncomesFailure(0, path, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), "");
    }

    // ---- state changes -------------------------------------------------------------------------------------------

    private void WriteUrl()
    {
        _writtenQuery = State.ToQuery();
        Nav.NavigateTo("incomes?" + _writtenQuery, replace: true);
    }

    /// <summary>Header filters or dataset changed: everything reloads and drill filters go.</summary>
    internal Task ApplyHeader(IncomesUrlState next)
    {
        State = next.WithoutDrill();
        ExportOpen = false;
        WriteUrl();
        return LoadAllAsync();
    }

    /// <summary>Drill filters, search, sort or grouping changed: only the table reloads.</summary>
    internal Task ApplyTable(IncomesUrlState next)
    {
        State = next;
        WriteUrl();
        return LoadRowsAsync();
    }

    internal Task SetDataset(bool orders)
    {
        if (State.Orders == orders) return Task.CompletedTask;
        ChartBy = orders ? "branch" : "method";
        var group = State.Group switch
        {
            "method" or "employee" when orders => null,
            "consultant" when !orders => null,
            var value => value,
        };
        return ApplyHeader(State with { Orders = orders, Group = group, Sort = "date", SortDesc = true });
    }

    internal Task SetPreset(string preset)
    {
        if (preset == "custom")
        {
            CustomOpen = !CustomOpen;
            CustomFrom = State.From;
            CustomTo = State.To;
            return Task.CompletedTask;
        }
        CustomOpen = false;
        PhonePeriodOpen = false;
        var (from, to) = IncomesPresets.Range(preset, Today);
        return SetPeriod(from, to);
    }

    internal Task SetPeriod(DateOnly from, DateOnly to)
    {
        Bucket = null;
        var group = State.Group;
        var next = State with { From = from, To = to };
        if (group == "month" && !next.IsLong) group = null;
        return ApplyHeader(next with { Group = group == next.DefaultGroup ? null : group });
    }

    internal Task StepMonth(int direction) =>
        IncomesPresets.StepMonth(State.From, direction, Today) is { } range ? SetPeriod(range.From, range.To) : Task.CompletedTask;

    internal Task ApplyCustom()
    {
        var (from, to) = (CustomFrom, CustomTo);
        if (from == default || to == default || to < from || to.DayNumber - from.DayNumber >= 1100)
            return Task.CompletedTask;
        CustomOpen = false;
        PhonePeriodOpen = false;
        return SetPeriod(from, to);
    }

    internal Task ResetFilters()
    {
        Bucket = null;
        _searchText = "";
        var (from, to) = IncomesPresets.Range("last", Today);
        return ApplyHeader(new IncomesUrlState { From = from, To = to, Orders = State.Orders });
    }

    internal Task ToggleBranch(string? code) =>
        ApplyHeader(State with { Branches = code is null ? null : IncomesUrlState.Toggle(State.Branches, code, BranchCodes) });

    internal Task ToggleMethod(string? code) =>
        !IsMoney ? Task.CompletedTask
        : ApplyHeader(State with { Methods = code is null ? null : IncomesUrlState.Toggle(State.Methods, code, MethodOrder.ToList()) });

    private IReadOnlyCollection<string> BranchCodes => Summary?.Branches.Select(b => b.Code).ToList() ?? [];

    internal Task SetGroup(string group)
    {
        var value = group == State.DefaultGroup ? null : group;
        return ApplyTable(State with { Group = value });
    }

    internal Task SortBy(string key)
    {
        var desc = State.Sort == key ? !State.SortDesc : key is "date" or "amt" or "paid" or "disc";
        return ApplyTable(State with { Sort = key, SortDesc = desc });
    }

    internal async Task SearchChanged(ChangeEventArgs args)
    {
        _searchText = args.Value?.ToString() ?? "";
        var generation = ++_searchGeneration;
        try { await Task.Delay(300, _lifetime.Token); }
        catch (TaskCanceledException) { return; }
        if (generation != _searchGeneration || _searchText.Trim() == State.Search) return;
        var text = _searchText.Trim();
        await ApplyTable(State with { Search = text.Length > 100 ? text[..100] : text });
    }

    internal Task SetDrill(IncomesUrlState next) => ApplyTable(next);

    internal Task DrillDay(IncomesBucketDto bucket)
    {
        var bucketKind = Summary?.Period.Bucket ?? "day";
        var cleared = State with { Day = null, Week = null, Month = null };
        if (bucketKind == "week")
        {
            var week = ISOWeek.GetWeekOfYear(bucket.From.ToDateTime(TimeOnly.MinValue));
            return SetDrill(State.Week == week ? cleared : cleared with { Week = week });
        }
        if (bucketKind == "month")
            return SetDrill(State.Month == bucket.Key ? cleared : cleared with { Month = bucket.Key });
        return SetDrill(State.Day == bucket.From ? cleared : cleared with { Day = bucket.From });
    }

    internal Task DrillCell(string branch, string? column)
    {
        var (method, week, month) = ColumnDrill(column);
        var same = State.DrillBranch == branch && State.DrillMethod == method && State.Week == week && State.Month == month;
        var cleared = State with { DrillBranch = null, DrillMethod = null, Week = null, Month = null };
        return SetDrill(same ? cleared : cleared with { DrillBranch = branch, DrillMethod = method, Week = week, Month = month });
    }

    internal Task DrillColumn(string column)
    {
        var (method, week, month) = ColumnDrill(column);
        var same = State.DrillBranch is null && State.DrillMethod == method && State.Week == week && State.Month == month;
        var cleared = State with { DrillBranch = null, DrillMethod = null, Week = null, Month = null };
        return SetDrill(same ? cleared : cleared with { DrillMethod = method, Week = week, Month = month });
    }

    internal Task DrillBranch(string branch)
    {
        var same = State.DrillBranch == branch && State.DrillMethod is null && State.Week is null && State.Month is null;
        var cleared = State with { DrillBranch = null, DrillMethod = null, Week = null, Month = null };
        return SetDrill(same ? cleared : cleared with { DrillBranch = branch });
    }

    private (string? Method, int? Week, string? Month) ColumnDrill(string? column) => column switch
    {
        null => (null, null, null),
        _ when IsMoney => (column, null, null),
        ['W', .. var week] => (null, int.Parse(week, CultureInfo.InvariantCulture), null),
        ['M', .. var month] => (null, null, month),
        _ => (null, null, null),
    };

    internal Task DrillRemark(IncomesDrillDto drill) => SetDrill(State.WithoutDrill() with
    {
        Day = drill.Day, DrillBranch = drill.Branch, DrillMethod = drill.Method,
        Refunds = drill.Refunds, NoEmployee = drill.NoEmployee, Cancelled = drill.Cancelled,
    });

    internal Task ToggleQuick(string kind) => SetDrill(kind switch
    {
        "refunds" => State with { Refunds = !State.Refunds },
        "noEmployee" => State with { NoEmployee = !State.NoEmployee },
        _ => State with { Cancelled = !State.Cancelled },
    });

    internal Task ClearDrill()
    {
        _searchText = "";
        return SetDrill(State.WithoutDrill() with { Search = "" });
    }

    internal async Task ToggleGroup(string key)
    {
        if (Rows is null) return;
        Rows.Toggle(key);
        if (Virtualizer is not null) await Virtualizer.RefreshDataAsync();
        StateHasChanged();
    }

    internal Task SetChartBy(string by)
    {
        if (!IsMoney && by == "method") return Task.CompletedTask;
        ChartBy = by;
        return Task.CompletedTask;
    }

    internal Task SetBucket(string bucket)
    {
        Bucket = bucket == DefaultBucket ? null : bucket;
        return LoadSummaryAsync();
    }

    internal string DefaultBucket => State.IsLong ? "month" : "day";

    // ---- toolbar actions ---------------------------------------------------------------------------------------

    internal async Task CopyLink()
    {
        try
        {
            await JS.InvokeAsync<string>("financeIncomes.copyLink", _lifetime.Token);
            ShowToast(L["Incomes.LinkCopied"].Value);
        }
        catch (JSException) { ShowToast(L["Incomes.CopyFailed"].Value); }
    }

    internal void ToggleExport() => ExportOpen = !ExportOpen;

    internal async Task Download()
    {
        ExportOpen = false;
        var file = ExportFileName;
        ShowToast(Rows?.Meta?.Total > ExcelProgressRows ? L["Incomes.ExportProgress", file].Value : L["Incomes.ExportPreparing", file].Value,
            persistent: true);
        try
        {
            await JS.InvokeVoidAsync("financeIncomes.download", _lifetime.Token, State.ToQuery());
            ShowToast(L["Incomes.ExportReady", file].Value);
        }
        catch (JSException) { ShowToast(L["Incomes.ExportFailed"].Value); }
    }

    internal async Task Print()
    {
        var money = IsMoney;
        var setup = new
        {
            columns = money
                ? new[] { L["Incomes.Col.Date"].Value, L["Incomes.Col.Order"].Value, L["Incomes.Col.Branch"].Value,
                    L["Incomes.Col.Method"].Value, L["Incomes.Col.Note"].Value, L["Incomes.Col.Employee"].Value, L["Incomes.Col.Amount"].Value }
                : new[] { L["Incomes.Col.Date"].Value, L["Incomes.Col.Order"].Value, L["Incomes.Col.Branch"].Value,
                    L["Incomes.Col.Consultant"].Value, L["Incomes.Col.Customer"].Value, L["Incomes.Col.Status"].Value, L["Incomes.Col.Value"].Value },
            fields = money
                ? new[] { "date", "orderNo", "branch", "method", "note", "employee", "amountEur" }
                : new[] { "date", "orderNo", "branch", "consultant", "customer", "status", "valueEur" },
            methods = MethodOrder.ToDictionary(code => code, code => L["Incomes.Method." + code].Value),
            statuses = new Dictionary<string, string>
            {
                ["accepted"] = L["Incomes.Status.Accepted"].Value, ["cancelled"] = L["Incomes.Status.Cancelled"].Value,
            },
            noEmployee = L["Incomes.NoEmployee"].Value,
            totalLabel = L["Incomes.Total"].Value,
            failed = L["Incomes.Error.Title"].Value,
        };
        try { await JS.InvokeVoidAsync("financeIncomes.print", _lifetime.Token, State.ToQuery(), setup); }
        catch (JSException) { ShowToast(L["Incomes.Error.Title"].Value); }
    }

    internal void ShowToast(string text, bool persistent = false)
    {
        Toast = text;
        var generation = ++_toastGeneration;
        StateHasChanged();
        if (!persistent) _ = HideToastLaterAsync(generation);
    }

    private async Task HideToastLaterAsync(int generation)
    {
        try { await Task.Delay(4200, _lifetime.Token); }
        catch (TaskCanceledException) { return; }
        await InvokeAsync(() =>
        {
            if (generation != _toastGeneration) return;
            Toast = null;
            StateHasChanged();
        });
    }

    internal void CloseToast() => Toast = null;

    internal void ToggleSelect(string name) => OpenSelect = OpenSelect == name ? null : name;

    internal void TogglePhonePeriod()
    {
        PhonePeriodOpen = !PhonePeriodOpen;
        CustomFrom = State.From;
        CustomTo = State.To;
    }

    public async ValueTask DisposeAsync()
    {
        Nav.LocationChanged -= OnLocationChanged;
        await _lifetime.CancelAsync();
        _lifetime.Dispose();
        if (_reference is not null)
        {
            try { await JS.InvokeVoidAsync("financeIncomes.unwatch", CancellationToken.None); }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException) { /* circuit gone */ }
            _reference.Dispose();
        }
    }
}
