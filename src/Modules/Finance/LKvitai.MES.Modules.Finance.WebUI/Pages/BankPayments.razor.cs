using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Finance.WebUI.Pages;

public partial class BankPayments
{
    public sealed record PaymentRow(int Nr, DateOnly Date, string Firm, string Purpose, decimal Sum);
    public sealed record PaymentPage(IReadOnlyList<PaymentRow> Items, int Total, int CountryTotal,
        DateOnly? LastDate, int Page, int PageSize);
    private sealed record PasteRow(int Line, DateOnly? Date, string Firm, string Purpose, decimal? Sum,
        string Status, string? Error, string Raw, bool Selected,
        int? MatchedNr, string? MatchedFirm);
    private sealed record PasteResult(Guid PreviewId, int LatestNr, IReadOnlyList<PasteRow> Rows);
    private sealed record ImportResult(Guid ImportId, int NrFrom, int NrTo, int Count);
    private static readonly (string Key, string Label)[] Columns =
    [
        ("nr", "Bank.Nr"), ("date", "Bank.Date"), ("firm", "Bank.Firm"),
        ("purpose", "Bank.Purpose"), ("sum", "Bank.Sum")
    ];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Lithuanian = CultureInfo.GetCultureInfo("lt-LT");
    private const string InitialPageKey = "finance.bank-payments.initial-page";
    private const int MaxPersistedPageLength = 180_000;
    private PersistingComponentStateSubscription _persistingSub;
    private DotNetObjectReference<BankPayments>? _reference;
    private PaymentPage? _data;
    private PasteResult? _preview;
    private ImportResult? _toast;
    private HashSet<int> _selected = [];
    private HashSet<int> _justImported = [];
    private string _country = "LT", _period = "all", _sort = "nr", _search = "";
    private string? _error;
    private bool _desc = true, _armed, _loading = true, _busy;
    private int _page = 1, _searchGeneration, _loadGeneration;

    protected override void OnInitialized()
    {
        _persistingSub = ApplicationState.RegisterOnPersisting(() =>
        {
            if (_data is not null)
            {
                var packed = Pack(_data);
                if (packed.Length <= MaxPersistedPageLength)
                    ApplicationState.PersistAsJson(InitialPageKey, packed);
            }
            return Task.CompletedTask;
        });
    }

    private static string Pack(PaymentPage page)
    {
        using var output = new MemoryStream();
        using (var compressed = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
            JsonSerializer.Serialize(compressed, page, Json);
        return Convert.ToBase64String(output.ToArray());
    }

    private static PaymentPage? Unpack(string packed)
    {
        using var input = new MemoryStream(Convert.FromBase64String(packed));
        using var compressed = new BrotliStream(input, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<PaymentPage>(compressed, Json);
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (ApplicationState.TryTakeFromJson<string>(InitialPageKey, out var state) && state is not null &&
                Unpack(state) is { } persisted)
            {
                _data = persisted;
                _loading = false;
                return;
            }
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidDataException or InvalidOperationException)
        { /* Fall back to the API if persisted data is incompatible. */ }

        // Prerender the first page so rows appear with the HTML response, before
        // Blazor Server connects. The shared Portal cookie is forwarded only to
        // the internal Finance API. Interactive loads continue using browser fetch.
        var context = HttpContexts.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true || context.Response.HasStarted ||
            !context.Request.Cookies.TryGetValue(PortalAuthDefaults.CookieName, out var cookie)) return;
        context.Response.Headers.CacheControl = "private, no-store";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "/api/finance/bank-payments?country=LT&sort=nr&desc=true&page=1");
            request.Headers.TryAddWithoutValidation("Cookie", $"{PortalAuthDefaults.CookieName}={cookie}");
            using var response = await HttpClients.CreateClient("FinanceApi")
                .SendAsync(request, context.RequestAborted);
            if (!response.IsSuccessStatusCode) return;
            _data = await response.Content.ReadFromJsonAsync<PaymentPage>(Json, context.RequestAborted);
            _loading = _data is null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // The interactive browser request retains the existing error UI.
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _reference = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("financeBank.attach", _reference);
        if (_data is null) await Load();
    }
    private string Failure(Exception error)
    {
        var marker = "finance-http:";
        var offset = error.Message.IndexOf(marker, StringComparison.Ordinal);
        var code = offset < 0 ? "" : new string(error.Message[(offset + marker.Length)..].TakeWhile(char.IsDigit).ToArray());
        return code is "400" or "401" or "403" or "409" or "503"
            ? L["Bank.Failed" + code].Value : L["Bank.ActionError"].Value;
    }
    private static string Money(decimal amount) => amount.ToString("N2", Lithuanian) + " €";
    private string Status(string code) => L["Bank." + code].Value;
    private string ErrorText(string? code) => L["Bank.Err" + code].Value;
    private int CountStatus(string status) => _preview?.Rows.Count(x => x.Status == status) ?? 0;
    private List<PasteRow> SelectedRows() => _preview?.Rows.Where(x => _selected.Contains(x.Line) &&
        x.Date.HasValue && x.Sum.HasValue && x.Status != "Error").ToList() ?? [];
    private async Task<string> Request(string path, string method = "GET", object? body = null) =>
        await JS.InvokeAsync<string>("financeBank.request", path, method, body);
    private async Task Load()
    {
        var generation = ++_loadGeneration;
        _loading = true; _error = null; StateHasChanged();
        try
        {
            var from = _period switch
            {
                "30" => DateOnly.FromDateTime(DateTime.Today.AddDays(-30)).ToString("yyyy-MM-dd"),
                "90" => DateOnly.FromDateTime(DateTime.Today.AddDays(-90)).ToString("yyyy-MM-dd"),
                _ => ""
            };
            var path = $"?country={_country}&sort={_sort}&desc={_desc.ToString().ToLowerInvariant()}&page={_page}" +
                (from.Length > 0 ? "&from=" + from : "") +
                (_search.Length > 0 ? "&q=" + Uri.EscapeDataString(_search) : "");
            var result = JsonSerializer.Deserialize<PaymentPage>(await Request(path), Json);
            if (generation == _loadGeneration) _data = result;
        }
        catch (Exception ex) { if (generation == _loadGeneration) { _error = Failure(ex); _data = null; } }
        finally { if (generation == _loadGeneration) { _loading = false; StateHasChanged(); } }
    }
    private async Task ChangeCountry(string country)
    {
        _country = country; _page = 1; _preview = null; _armed = false; _toast = null; _justImported.Clear();
        await Load();
    }
    private async Task ChangePeriod(string period) { _period = period; _page = 1; await Load(); }
    private async Task Sort(string key)
    {
        if (_sort == key) _desc = !_desc; else { _sort = key; _desc = key == "nr" || key == "date"; }
        _page = 1; await Load();
    }
    private async Task Previous() { _page--; await Load(); }
    private async Task Next() { _page++; await Load(); }
    private async Task SearchChanged(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? "";
        var generation = ++_searchGeneration;
        await Task.Delay(300);
        if (generation != _searchGeneration) return;
        _page = 1; await Load();
    }
    private async Task ArmPaste()
    {
        _armed = true; _preview = null; _error = null;
        StateHasChanged(); await Task.Yield();
        await JS.InvokeVoidAsync("financeBank.focusPaste");
    }
    private void CancelPaste() { _armed = false; _preview = null; _error = null; }
    [JSInvokable]
    public async Task ReceivePaste(string text)
    {
        if (_busy) return;
        _busy = true; _error = null;
        try
        {
            _preview = JsonSerializer.Deserialize<PasteResult>(await Request("/parse", "POST", new { country = _country, text }), Json);
            _selected = _preview?.Rows.Where(x => x.Selected && x.Status == "New").Select(x => x.Line).ToHashSet() ?? [];
            _armed = false;
        }
        catch (Exception ex) { _error = Failure(ex); }
        finally { _busy = false; await InvokeAsync(StateHasChanged); }
    }
    private Task PasteSample() => ReceivePaste("2026.10.30\tUAB Pavyzdys\tUžsakymas VV-123\t1 020,01 €\n30.10.2026\tUAB Pavyzdys\tPakartotinis mokėjimas\t1 020,01 €\nNeteisinga eilutė");
    private void SelectRow(int line, object? value)
    {
        if (value is true || string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase)) _selected.Add(line);
        else _selected.Remove(line);
    }
    private async Task Import()
    {
        var chosen = SelectedRows();
        if (chosen.Count == 0 || _busy) return;
        _busy = true; _error = null;
        try
        {
            _toast = JsonSerializer.Deserialize<ImportResult>(await Request("", "POST", new {
                country = _country, previewId = _preview!.PreviewId,
                expectedNextNr = _preview.LatestNr + 1,
                selectedLines = chosen.Select(x => x.Line).ToArray()
            }), Json);
            _justImported = _toast is null ? [] : Enumerable.Range(_toast.NrFrom, _toast.Count).ToHashSet();
            _preview = null; _armed = false; _period = "all"; _search = ""; _page = 1; _sort = "nr"; _desc = true;
            await Load();
        }
        catch (Exception ex) { _error = Failure(ex); }
        finally { _busy = false; StateHasChanged(); }
    }
    private async Task Delete(int nr)
    {
        if (!await JS.InvokeAsync<bool>("financeBank.confirm", L["Bank.DeleteConfirm", nr].Value)) return;
        try { await Request("/" + nr, "DELETE"); _justImported.Remove(nr); await Load(); }
        catch (Exception ex) { _error = Failure(ex); StateHasChanged(); }
    }
    private async Task Undo()
    {
        if (_toast is null || !await JS.InvokeAsync<bool>("financeBank.confirm", L["Bank.UndoConfirm"].Value)) return;
        try { await Request("/imports/" + _toast.ImportId, "DELETE"); _toast = null; _justImported.Clear(); await Load(); }
        catch (Exception ex) { _error = Failure(ex); StateHasChanged(); }
    }
    public async ValueTask DisposeAsync()
    {
        _persistingSub.Dispose();
        // Dispose also runs for the static prerender instance, where JS interop
        // is unavailable and attach was never called.
        if (_reference is not null)
        {
            try { await JS.InvokeVoidAsync("financeBank.detach"); } catch (JSDisconnectedException) { }
        }
        _reference?.Dispose();
    }
}
