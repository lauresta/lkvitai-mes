using System.Globalization;
using System.Text.Json;
using System.Text.Encodings.Web;
using LKvitai.MES.Modules.Shopfloor.WebUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Pages;

public partial class TrapezoidDocument : IDisposable
{
    [Parameter, EditorRequired] public TrapezoidItemListDto Data { get; set; } = new();
    [Inject] private ShopfloorApiClient Api { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    private TrapezoidItemDto? _item;
    private TrapezoidInputsDto _inputs = new();
    private TrapezoidInputsDto _sourceInputs = new();
    private Dictionary<string, string> _sources = [];
    private TrapezoidCalculationDto? _calculation;
    private TrapezoidVersionDto? _saved;
    private List<TrapezoidVersionSummaryDto> _versions = [];
    private string? _error;
    private bool _recalculating;
    private bool _saving;
    private bool _loading;
    private bool _orderChanged;
    private bool _archived;
    private bool _pdfError;
    private string _mode = "front";
    private int _selectedSlat = 1;
    private int _generation;
    private Guid _requestId = Guid.NewGuid();
    private CancellationTokenSource? _debounce;
    private CancellationTokenSource? _loadCts;
    private bool Dirty => _saved is null || JsonSerializer.Serialize(_inputs) != JsonSerializer.Serialize(_saved.Inputs);
    private bool Complete => _inputs.ControlType is "A" or "B" && _inputs.SlatWidthMm is 89 or 127 &&
        _inputs.WidthMm > 0 && _inputs.ShortHeightMm > 0 &&
        _inputs.TallHeightMm >= _inputs.ShortHeightMm && _inputs.OverhangMm >= 0 &&
        _inputs.HighSide is "Left" or "Right";
    private string MissingFields => string.Join(", ", new (bool Missing, string Name)[]
    {
        (_inputs.ControlType is not ("A" or "B"), T("typeL")),
        (_inputs.SlatWidthMm is not (89 or 127), T("slatW")),
        (_inputs.WidthMm is null or <= 0, T("AD")),
        (_inputs.TallHeightMm is null or <= 0, T("CD")),
        (_inputs.ShortHeightMm is null or <= 0, T("AB")),
        (_inputs.OverhangMm is null, T("O")),
        (_inputs.HighSide is not ("Left" or "Right"), T("highSide"))
    }.Where(x => x.Missing).Select(x => x.Name));
    private bool CanSave => Data.CanEdit && _item?.Supported == true && Complete && _calculation is not null &&
        !_recalculating && !_saving && Dirty && !_archived;
    private bool CanPrint => _saved is not null && !Dirty && !_recalculating;
    private TrapezoidItemDto? DocumentItem => _saved is not null && (_archived || _orderChanged)
        ? _saved.Item : _item;
    private TrapezoidResultDto? Result => _archived ? _saved?.Calculation.Result : _calculation?.Result;
    private TrapezoidSlatDto? SelectedSlat => Result?.Slats.FirstOrDefault(x => x.Number == _selectedSlat);
    private double FromFixedEnd(TrapezoidSlatDto slat) => Result is null ? 0 :
        (Result.FixedOffsetMm + slat.AlongRailMm) * Math.Cos(Result.Angle);
    private int PageCount => Result?.SlatCount is > 30 ? 1 + (int)Math.Ceiling((Result.SlatCount - 30) / 60.0) : 1;
    private static string Num(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
    private string UsedDimension(string field, double? value) =>
        Num(value) + (_sources.GetValueOrDefault(field) == "manual" ? "*" : "");
    private string ManualSummary => ManualSummaryFor(_sources);
    private string ManualSummaryFor(IReadOnlyDictionary<string, string> sources) =>
        string.Join(", ", sources.Where(x => x.Value == "manual")
        .Select(x => x.Key switch
        {
            "widthMm" => "AD", "shortHeightMm" => "AB", "tallHeightMm" => "CD",
            "overhangMm" => T("O"), "highSide" => T("highSide"),
            "slatWidthMm" => T("slatW"), "controlType" => T("typeL"), _ => x.Key
        }));
    private static string Mm(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
    private string T(string key) => L[$"Trapezoid.{key}"].Value;
    private string Source(string field) => _sources.GetValueOrDefault(field, "empty") switch
    {
        "order" => T("src_order"), "item" => T("src_item"), "manual" => T("src_manual"),
        "version" => T("src_version"), _ => T("src_empty")
    };
    private string ErrorText => _error switch
    {
        "OutsideReferenceTable" => T("e_table").Replace("{0}", Num(Result?.LookupWidthMm)),
        "UnsupportedExtender" => T("e_ext").Replace("{0}", "?"),
        "BeyondLegacyPrintCapacity" => T("e_cap"),
        "SlatsOutsideOpening" => T("e_outside"),
        "InvalidDimensions" => T("e_geom"),
        "ORDER_CHANGED" => T("b_ocT").Replace("{0}", _saved?.Version.ToString() ?? "?"),
        "INCOMPLETE_DIMENSIONS" => T("why_incomplete"),
        _ => _error ?? ""
    };

    protected override async Task OnParametersSetAsync()
    {
        if (_item is not null && Data.Items.Any(x => x.ProductId == _item.ProductId)) return;
        if (Data.Items.Count > 0) await SelectItemAsync(Data.Items[0].ProductId);
    }

    private async Task SelectItemAsync(long id)
    {
        var item = Data.Items.FirstOrDefault(x => x.ProductId == id);
        if (item is null) return;
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        _debounce?.Cancel();
        _generation++;
        _item = item;
        _loading = true;
        _error = null;
        _pdfError = false;
        _archived = false;
        _orderChanged = false;
        _selectedSlat = 1;
        _saved = null;
        _calculation = null;
        _versions = [];
        _sourceInputs = new TrapezoidInputsDto
        {
            ControlType = item.ControlType,
            SlatWidthMm = item.SlatWidthItemMm,
            WidthMm = (double?)item.WidthMm,
            TallHeightMm = (double?)item.TallHeightMm,
            ShortHeightMm = (double?)item.ShortHeightMm,
            OverhangMm = (double?)item.OverhangMm ?? (item.ControlType == "B" ? 0 : null),
            HighSide = null // no verified orientation field exists in legacy data
        };
        _inputs = Clone(_sourceInputs);
        _sources = new()
        {
            ["controlType"] = item.ControlType is null ? "empty" : "item",
            ["slatWidthMm"] = item.SlatWidthItemMm is null ? "empty" : "item",
            ["widthMm"] = item.WidthMm is null ? "empty" : "order",
            ["tallHeightMm"] = item.TallHeightMm is null ? "empty" : "order",
            ["shortHeightMm"] = item.ShortHeightMm is null ? "empty" : "order",
            ["overhangMm"] = item.OverhangMm is null ? "empty" : "order",
            ["highSide"] = "empty"
        };
        try
        {
            var versions = await Api.GetTrapezoidVersionsAsync(Data.TaskId, id, ct);
            if (ct.IsCancellationRequested) return;
            if (versions.Success && versions.Value is not null)
            {
                _versions = versions.Value;
                if (_versions.Count > 0)
                {
                    var latest = await Api.GetTrapezoidVersionAsync(Data.TaskId, id,
                        _versions[0].Version, ct);
                    if (latest.Success && latest.Value is not null && !ct.IsCancellationRequested)
                    {
                        _saved = latest.Value;
                        _inputs = Clone(latest.Value.Inputs);
                        _sources = new(latest.Value.Sources);
                        _calculation = latest.Value.Calculation;
                        _orderChanged = latest.Value.Item.Revision != item.Revision;
                        _selectedSlat = Math.Min(8, Math.Max(1, Result?.SlatCount ?? 1));
                    }
                }
            }
            else _error = versions.Error;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!ct.IsCancellationRequested) { _loading = false; StateHasChanged(); }
        }
        if (!ct.IsCancellationRequested && _calculation is null && Complete) await RecalculateAsync();
    }

    private Task OnItemChanged(ChangeEventArgs e) =>
        long.TryParse(e.Value?.ToString(), out var id) ? SelectItemAsync(id) : Task.CompletedTask;
    private Task OnVersionChanged(ChangeEventArgs e) =>
        int.TryParse(e.Value?.ToString(), out var version) ? OpenVersionAsync(version) : Task.CompletedTask;

    private static TrapezoidInputsDto Clone(TrapezoidInputsDto x) => new()
    {
        ControlType = x.ControlType, SlatWidthMm = x.SlatWidthMm, WidthMm = x.WidthMm,
        ShortHeightMm = x.ShortHeightMm, TallHeightMm = x.TallHeightMm,
        OverhangMm = x.OverhangMm, HighSide = x.HighSide
    };

    private void SetText(string field, string value)
    {
        if (_archived) return;
        switch (field)
        {
            case "controlType": _inputs.ControlType = value; break;
            case "highSide": _inputs.HighSide = value; break;
            case "slatWidthMm": _inputs.SlatWidthMm = int.Parse(value, CultureInfo.InvariantCulture); break;
        }
        _sources[field] = "manual";
        QueueCalculation();
    }

    private void SetNumber(string field, ChangeEventArgs e)
    {
        if (_archived) return;
        var text = Convert.ToString(e.Value)?.Trim();
        var value = string.IsNullOrEmpty(text) ? (double?)null :
            double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture,
                out var parsed) ? parsed : null;
        switch (field)
        {
            case "widthMm": _inputs.WidthMm = value; break;
            case "shortHeightMm": _inputs.ShortHeightMm = value; break;
            case "tallHeightMm": _inputs.TallHeightMm = value; break;
            case "overhangMm": _inputs.OverhangMm = value; break;
        }
        _sources[field] = value is null ? "empty" : "manual";
        QueueCalculation();
    }

    private void Restore(string field)
    {
        if (_archived) return;
        switch (field)
        {
            case "widthMm": _inputs.WidthMm = _sourceInputs.WidthMm; break;
            case "shortHeightMm": _inputs.ShortHeightMm = _sourceInputs.ShortHeightMm; break;
            case "tallHeightMm": _inputs.TallHeightMm = _sourceInputs.TallHeightMm; break;
            case "overhangMm": _inputs.OverhangMm = _sourceInputs.OverhangMm; break;
            case "controlType": _inputs.ControlType = _sourceInputs.ControlType; break;
            case "slatWidthMm": _inputs.SlatWidthMm = _sourceInputs.SlatWidthMm; break;
            case "highSide": _inputs.HighSide = _sourceInputs.HighSide; break;
        }
        _sources[field] = field is "controlType" or "slatWidthMm" ? "item" :
            field == "highSide" || (field == "overhangMm" && _sourceInputs.OverhangMm is null) ? "empty" : "order";
        QueueCalculation();
    }

    private void ApplyOrderValues()
    {
        _inputs = Clone(_sourceInputs);
        _sources = new(_sources.ToDictionary(x => x.Key, x => x.Key is "controlType" or "slatWidthMm" ? "item" :
            x.Key == "highSide" ? "empty" : "order"));
        _orderChanged = false;
        QueueCalculation();
    }

    private void QueueCalculation()
    {
        _requestId = Guid.NewGuid();
        _error = null;
        _pdfError = false;
        _calculation = null;
        _debounce?.Cancel();
        var generation = ++_generation;
        if (!Complete) { _recalculating = false; return; }
        _recalculating = true;
        _debounce = new CancellationTokenSource();
        var ct = _debounce.Token;
        _ = DebouncedAsync(generation, ct);
    }

    private async Task DebouncedAsync(int generation, CancellationToken ct)
    {
        try
        {
            await Task.Delay(400, ct);
            await InvokeAsync(() => RecalculateAsync(generation, ct));
        }
        catch (OperationCanceledException) { }
    }

    private Task RecalculateAsync() => RecalculateAsync(++_generation, CancellationToken.None);
    private async Task RecalculateAsync(int generation, CancellationToken ct)
    {
        _recalculating = true;
        var input = Clone(_inputs);
        try
        {
            var result = await Api.CalculateTrapezoidAsync(input, ct);
            if (generation != _generation || ct.IsCancellationRequested) return;
            _calculation = result.Success ? result.Value : null;
            _error = result.Success ? null : result.Error;
            _selectedSlat = Math.Min(Math.Max(1, _selectedSlat), Math.Max(1, Result?.SlatCount ?? 1));
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (generation == _generation) { _recalculating = false; StateHasChanged(); }
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave || _item is null) return;
        _saving = true;
        _error = null;
        try
        {
            var result = await Api.SaveTrapezoidAsync(Data.TaskId, _item.ProductId, _requestId,
                _item.Revision, Clone(_inputs), _sources,
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "lt" ? "lt" : "en",
                CancellationToken.None);
            if (result.Success && result.Value is not null)
            {
                _saved = result.Value;
                _calculation = result.Value.Calculation;
                _sources = new(result.Value.Sources);
                _orderChanged = false;
                var versions = await Api.GetTrapezoidVersionsAsync(Data.TaskId, _item.ProductId, CancellationToken.None);
                if (versions.Success && versions.Value is not null) _versions = versions.Value;
            }
            else _error = result.Error;
        }
        finally { _saving = false; }
    }

    private async Task OpenVersionAsync(int version)
    {
        if (_item is null) return;
        var result = await Api.GetTrapezoidVersionAsync(Data.TaskId, _item.ProductId, version, CancellationToken.None);
        if (result.Success && result.Value is not null)
        {
            _saved = result.Value;
            _inputs = Clone(result.Value.Inputs);
            _sources = new(result.Value.Sources);
            _calculation = result.Value.Calculation;
            _archived = version != _versions.FirstOrDefault()?.Version;
            _selectedSlat = Math.Min(_selectedSlat, Result?.SlatCount ?? 1);
            _error = null;
        }
        else _error = result.Error;
    }

    private async Task RefreshAsync()
    {
        if (_item is null) return;
        var data = await Api.GetTrapezoidItemsAsync(Data.TaskId, CancellationToken.None);
        if (data.Success && data.Value is not null)
        {
            Data = data.Value;
            await SelectItemAsync(_item.ProductId);
        }
        else _error = data.Error;
    }

    private void SelectSlat(int number)
    {
        if (Result is null) return;
        _selectedSlat = Math.Clamp(number, 1, Result.SlatCount);
    }
    private void SchemeKey(KeyboardEventArgs e)
    {
        if (e.Key == "ArrowLeft") SelectSlat(_selectedSlat - 1);
        if (e.Key == "ArrowRight") SelectSlat(_selectedSlat + 1);
    }
    private void RowKey(KeyboardEventArgs e, int number)
    {
        if (e.Key == "ArrowUp") SelectSlat(number - 1);
        if (e.Key == "ArrowDown") SelectSlat(number + 1);
        if (e.Key is "Enter" or " ") SelectSlat(number);
    }
    private void SetMode(string mode) => _mode = mode;
    private static MarkupString SvgText(double x, double y, string value, string css = "", string anchor = "middle") =>
        new($"<text x=\"{x.ToString(CultureInfo.InvariantCulture)}\" y=\"{y.ToString(CultureInfo.InvariantCulture)}\" class=\"{HtmlEncoder.Default.Encode(css)}\" text-anchor=\"{anchor}\">{HtmlEncoder.Default.Encode(value)}</text>");
    private async Task GeneratePdfAsync()
    {
        if (!CanPrint || _item is null || _saved is null) return;
        _pdfError = false;
        var result = await Api.GenerateTrapezoidPdfAsync(Data.TaskId, _item.ProductId,
            _saved.Version, CancellationToken.None);
        if (!result.Success) { _pdfError = true; _error = result.Error; return; }
        var versions = await Api.GetTrapezoidVersionsAsync(Data.TaskId, _item.ProductId, CancellationToken.None);
        if (versions.Success && versions.Value is not null) _versions = versions.Value;
        var url = $"/api/shopfloor/production-tasks/{Data.TaskId}/trapezoid-items/{_item.ProductId}/versions/{_saved.Version}/pdf";
        await Js.InvokeVoidAsync("open", url, "_blank");
    }
    private async Task PrintAsync()
    {
        if (!CanPrint) return;
        await Js.InvokeVoidAsync("gamybaUi.print", "trapezoid");
    }
    public void Dispose() { _debounce?.Cancel(); _debounce?.Dispose(); _loadCts?.Cancel(); _loadCts?.Dispose(); }
}
