using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Sales.WebUI.Components;

public partial class NewOrderWorkspace
{
    private string _noticeText = "";
    private string _noticeTitle = "";
    private string _noticeKind = "warning";
    private int _noticeVersion;
    private string? _focusInvalidField;
    private string? _lastInvalidField;
    private readonly Dictionary<string,string> _productErrors = new();
    private string? ProductError(string field) => _productErrors.GetValueOrDefault(field);
    private string ProductFieldClass(string field) => ProductError(field) is null ? "" : "has-error";
    private void DismissNotice() => _noticeText = "";
    private void ShowNotice(string text, string kind = "warning", string? title = null)
    {
        _noticeText = ApiMessage(text); _noticeKind = kind; _noticeTitle = title ?? L["Workspace.Attention"].Value; _noticeVersion++;
    }
    private void ClearProductErrors() { _productErrors.Clear(); _lastInvalidField=null; }
    private async Task FocusProductErrorAsync()
    {
        if(_draftModule is not null && _lastInvalidField is not null)
            await _draftModule.InvokeVoidAsync("focusInvalidField", _lastInvalidField);
    }
    private Microsoft.AspNetCore.Components.RenderFragment ProductFieldError(string field) => builder =>
    {
        if(ProductError(field) is not { } error)return;
        builder.OpenElement(0,"div");
        builder.AddAttribute(1,"id","config-"+field+"-error");
        builder.AddAttribute(2,"class","lk-field-error");
        builder.AddContent(3,error);
        builder.CloseElement();
    };
    private void ShowProductFailure(SalesOrderEntryFailure failure, bool focusField = true)
    {
        var field = failure.Code switch
        {
            "INSUFFICIENT_MATERIAL" or "UNKNOWN_STOCK_POLICY" or "INVALID_MATERIAL" or "COLOUR_REQUIRED" or "MATERIAL_NOT_ALLOWED" or "COLOUR_UNAVAILABLE" => "colour",
            "PRICE_LIST_REQUIRED" or "PRICE_NOT_FOUND" or "PRICE_CHANGED" or "INVALID_PRICE" or "PRICE_MODE" or "PRICE_MISSING" or "PRICE_AMBIGUOUS" or "PRICE_INVALIDREFERENCE" or "PRICE_INVALIDPRICEDATA" => "price",
            "CONTROL_LENGTH" => "control",
            "PRODUCT_NOTE_LENGTH" => "note",
            "INVALID_DIMENSIONS" or "DIMENSIONS_REQUIRED" => _configuringLine?.WidthMm <= 0 ? "width" : _configuringLine?.HeightMm <= 0 ? "height" : "quantity",
            "INVALID_MATERIAL_REQUIREMENT" => "ppp-11",
            _ => ""
        };
        var message = failure.Message;
        if(failure.Code == "LEGACY_VALIDATION" && message.Contains("UPPER CASE CHARACTERS", StringComparison.OrdinalIgnoreCase))
        {
            field="note";
            message=L["Product.NoteCapitalization"].Value;
        }
        // Older legacy rule responses carry a technical field prefix. Keep the
        // operator text readable while using that prefix to locate the input.
        var separator = message.IndexOf(": ", StringComparison.Ordinal);
        if (separator > 0)
        {
            var prefix = message[..separator];
            var prefixField = prefix switch { "colourId" => "colour", "unitPrice" => "price", "widthMm" => "width", "heightMm" => "height", "quantity" => "quantity", _ => "" };
            if (prefix.StartsWith("PPP", StringComparison.Ordinal) && int.TryParse(prefix[3..], out var slot) && slot is >= 1 and <= 12)
                prefixField = "ppp-" + (slot-1);
            if (prefixField.Length > 0) { field=prefixField; message=message[(separator+2)..]; }
        }
        if(field.Length > 0) { _productErrors[field]=message; _lastInvalidField="config-"+field; if(focusField)_focusInvalidField=_lastInvalidField; }
        ShowNotice(message, "error", L["Product.ChangesNotSaved"].Value);
    }
}
