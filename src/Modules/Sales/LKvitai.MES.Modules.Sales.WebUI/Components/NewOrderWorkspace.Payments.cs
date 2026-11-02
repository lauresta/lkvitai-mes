using System.Globalization;
using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Sales.WebUI.Components;

public partial class NewOrderWorkspace
{
    private OrderPaymentReferences? _paymentReferences;
    private int? _paymentMethodId, _paymentEmployeeId, _paymentRegisterId, _paymentBankTransferId;
    private string _paymentSaved = "";
    private string _paymentAmountText = "";
    private string _paymentBankSearch = "";
    private OrderPaymentBankTransferPage? _paymentBankPage;
    private bool _paymentLookupBusy, _paymentBankLoading, _paymentContextValid;
    private bool _paymentFocusReceiver;
    private ElementReference _paymentReceiverElement;
    private int _paymentContextGeneration, _paymentBankGeneration;
    private bool CanPostLivePayment => _order is { PaymentsEnabled:true, WritesAllowed:true, Cancelled:false };
    private OrderPaymentMethodReference? SelectedPaymentMethod => _paymentReferences?.Methods.FirstOrDefault(x => x.Id == _paymentMethodId);
    private IEnumerable<OrderPaymentMethodReference> PaymentMethods => _paymentReferences?.Methods ?? [];
    private bool PaymentMethodAllowed(OrderPaymentMethodReference method) => method.IsSupported && _order?.AllowedPaymentMethods.Contains(method.Id)==true;
    private OrderPaymentBankTransferReference? SelectedPaymentBank => _paymentReferences?.SelectedBankTransfer;
    private bool PaymentContextMatches => _paymentReferences?.Selection is { } selection && selection.MethodId==_paymentMethodId && selection.BankTransferId==_paymentBankTransferId && selection.RegisterId==_paymentRegisterId;
    private bool PaymentDownstreamEnabled => _paymentContextValid && !_paymentLookupBusy && PaymentContextMatches && SelectedPaymentMethod is { } method && PaymentMethodAllowed(method)
        && string.IsNullOrEmpty(_paymentReferences?.PrerequisiteError)
        && (!method.RequiresPpkReceipt || LegacyPaymentRules.IsValidPpkReceipt(_paymentReference))
        && (!method.RequiresBankTransfer || SelectedPaymentBank is { IsAvailable:true } bank && bank.Id==_paymentBankTransferId)
        && (!method.RequiresRegister || _paymentReferences?.SelectedRegister?.Id==_paymentRegisterId && _paymentRegisterId is not null);
    private bool PaymentCanSubmit => _paymentReferences?.Enabled==true && _paymentReferences.OrderRevision==_order?.Revision && CanPostLivePayment && PaymentDownstreamEnabled && !_paymentBankLoading;
    private string? PaymentPrerequisiteMessage => SelectedPaymentMethod?.RequiresPpkReceipt==true && !LegacyPaymentRules.IsValidPpkReceipt(_paymentReference)
        ? "Enter the actual ten-character PPK receipt number to continue."
        : _paymentReferences?.PrerequisiteError;
    private bool PaymentDirty => IsLive && _paymentSheetOpen && PaymentDraftState() != _paymentSaved;
    private string PaymentDraftState() => JsonSerializer.Serialize(new { _paymentDate, _paymentAmount, _paymentAmountText, _paymentMethodId, _paymentEmployeeId, _paymentRegisterId, _paymentBankTransferId, _paymentReference, _paymentNote });
    private string PaymentMoney(decimal amount) => amount.ToString("N2", CultureInfo.GetCultureInfo("lt-LT")) + " " + (_paymentReferences?.Currency?.Code ?? "—");
    private static string BankAmount(decimal amount) => amount.ToString(amount==decimal.Round(amount,2)?"N2":amount==decimal.Round(amount,3)?"N3":"N4",CultureInfo.GetCultureInfo("lt-LT"));
    private static string BankTransferLabel(OrderPaymentBankTransferReference bank) => $"{bank.Payer ?? "Payer not specified"} · {bank.Date?.ToString("yyyy-MM-dd") ?? "—"} · {BankAmount(bank.AvailableAmount)}";

    private async Task OpenLivePaymentAsync()
    {
        if (_order?.LegacyId is not { } id || !CanPostLivePayment) return;
        if (_confirmationSheetOpen) { _liveError="Close production confirmation before opening a payment."; return; }
        if (!await LeaveProductAsync()) return;
        if (HeaderDirty || AddressDirty) { _liveError="Save the header and address changes before posting a payment."; return; }
        _busy=true;
        _paymentError="";
        _paymentReferences=null;
        try
        {
            ApplySnapshot(await Entry.ReadAsync(id));
            _paymentReferences=await Entry.PaymentReferencesAsync(id);
            _paymentDate=_paymentReferences.BusinessDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            _paymentReference=""; ResetPaymentDownstream();
            _paymentBankSearch=""; _paymentBankPage=null;
            _paymentRegisterId=null; _paymentBankTransferId=null;
            _paymentMethodId=PaymentMethods.Any(x=>x.Id==_paymentReferences.DefaultMethodId && PaymentMethodAllowed(x)) ? _paymentReferences.DefaultMethodId : null;
            _paymentSheetOpen=true;
            if(_paymentMethodId is not null)await RefreshPaymentContextAsync();
            SetPaymentReferenceError();
            _paymentSaved=PaymentDraftState();
        }
        catch (SalesOrderEntryFailure ex) { _liveError=ex.Message; }
        finally { _busy=false; }
    }

    private bool PaymentCommandPending => Entry.HasPendingCommand;
    private void ResetPaymentDownstream()
    {
        _paymentEmployeeId=null; _paymentAmount=0; _paymentAmountText=""; _paymentNote=""; _paymentError="";
        _paymentFocusReceiver=false;
    }
    private async Task ChangePaymentMethod(ChangeEventArgs e)
    {
        if(PaymentCommandPending)return;
        _paymentContextGeneration++;_paymentBankGeneration++;
        _paymentMethodId=int.TryParse(e.Value?.ToString(),out var id) ? id : null;
        _paymentReference=""; _paymentRegisterId=null; _paymentBankTransferId=null;
        _paymentBankPage=null;_paymentBankSearch="";_paymentBankLoading=false;
        ResetPaymentDownstream();
        await RefreshPaymentContextAsync();
    }
    private void ChangePaymentEmployee(ChangeEventArgs e)
    {
        if(PaymentCommandPending || !PaymentDownstreamEnabled)return;
        _paymentEmployeeId=int.TryParse(e.Value?.ToString(),out var id) ? id : null;
        _paymentError="";
    }
    private void ChangePaymentAmount(ChangeEventArgs e)
    {
        if(PaymentCommandPending || !PaymentDownstreamEnabled)return;
        _paymentAmountText=e.Value?.ToString() ?? "";
        _paymentAmount=decimal.TryParse(_paymentAmountText,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint|NumberStyles.AllowExponent,CultureInfo.InvariantCulture,out var amount) ? amount : 0;
        _paymentError="";
    }
    private void ChangePaymentPpk(ChangeEventArgs e)
    {
        if(PaymentCommandPending)return;
        var wasValid=LegacyPaymentRules.IsValidPpkReceipt(_paymentReference);
        _paymentReference=e.Value?.ToString() ?? "";
        _paymentError="";
        if(!LegacyPaymentRules.IsValidPpkReceipt(_paymentReference))ResetPaymentDownstream();
        else if(!wasValid)_paymentFocusReceiver=true;
    }
    private async Task ChangePaymentBank(ChangeEventArgs e)
    {
        if(PaymentCommandPending)return;
        _paymentBankTransferId=int.TryParse(e.Value?.ToString(),out var id) ? id : null;
        _paymentEmployeeId=null;_paymentError="";
        await RefreshPaymentContextAsync();
    }
    private async Task ChangePaymentRegister(ChangeEventArgs e)
    {
        if(PaymentCommandPending)return;
        _paymentRegisterId=int.TryParse(e.Value?.ToString(),out var id) ? id : null;
        _paymentEmployeeId=null;_paymentError="";
        await RefreshPaymentContextAsync();
    }
    private void SetPaymentReferenceError()
    {
        if(_paymentReferences?.Enabled!=true)_paymentError=_paymentReferences?.UnavailableReason ?? "Payment posting is unavailable.";
        else if(_paymentReferences.OrderRevision!=_order?.Revision)_paymentError="The order changed. Close and reopen payments to refresh balances.";
    }
    private async Task RefreshPaymentContextAsync()
    {
        if(_order?.LegacyId is not { } orderId || PaymentCommandPending)return;
        var method=_paymentMethodId;var bank=_paymentBankTransferId;var register=_paymentRegisterId;
        var generation=++_paymentContextGeneration;
        _paymentContextValid=false;
        _paymentLookupBusy=true;
        try
        {
            var references=await Entry.PaymentReferencesAsync(orderId,method,bank,register);
            if(generation!=_paymentContextGeneration || PaymentCommandPending || method!=_paymentMethodId || bank!=_paymentBankTransferId || register!=_paymentRegisterId)return;
            _paymentReferences=references;
            _paymentContextValid=true;
            _paymentDate=references.BusinessDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            _paymentEmployeeId=references.DefaultEmployeeId is { } employee && references.Employees.Any(x=>x.Id==employee) ? employee : null;
            SetPaymentReferenceError();
        }
        catch(SalesOrderEntryFailure ex){if(generation==_paymentContextGeneration)_paymentError=ex.Message;}
        finally{if(generation==_paymentContextGeneration)_paymentLookupBusy=false;}
        if(generation==_paymentContextGeneration && SelectedPaymentMethod?.RequiresBankTransfer==true && _paymentBankPage is null)await SearchPaymentBanksAsync();
    }
    private Task SearchPaymentBanksAsync() => LoadPaymentBanksAsync(1);
    private async Task LoadPaymentBanksAsync(int page)
    {
        if(_order?.LegacyId is not { } orderId || PaymentCommandPending || SelectedPaymentMethod?.RequiresBankTransfer!=true)return;
        var generation=++_paymentBankGeneration;
        var method=_paymentMethodId;
        _paymentBankLoading=true;
        try
        {
            var result=await Entry.PaymentBankTransfersAsync(orderId,_paymentBankSearch,page,25,_paymentBankTransferId);
            if(generation!=_paymentBankGeneration || method!=_paymentMethodId || PaymentCommandPending)return;
            _paymentBankPage=result;
            if(result.Selected is { } selected && selected.Id==_paymentBankTransferId && _paymentReferences is not null)
            {
                _paymentReferences=_paymentReferences with{SelectedBankTransfer=selected};
                if(!selected.IsAvailable)SetPaymentReferenceError();
            }
        }
        catch(SalesOrderEntryFailure ex){if(generation==_paymentBankGeneration)_paymentError=ex.Message;}
        finally{if(generation==_paymentBankGeneration)_paymentBankLoading=false;}
    }

    private async Task PostLivePaymentAsync()
    {
        if (_order?.LegacyId is not { } id || _paymentReferences is not { Enabled:true } references || !CanPostLivePayment)
        { _paymentError="Payment posting is unavailable. Reopen the payment sheet to refresh its options."; return; }
        if (HeaderDirty || AddressDirty) { _paymentError="Save header and address changes before posting a payment."; return; }
        if (!PaymentCanSubmit) { _paymentError="Complete the selected method's reference before posting a payment."; return; }
        if (!LegacyPaymentRules.IsValidPositiveAmount(_paymentAmount) || references.MaximumAmount is { } maximum && _paymentAmount>maximum)
        { _paymentError="Enter a positive amount with at most two decimal places."; return; }
        if (!DateOnly.TryParseExact(_paymentDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date) || date!=references.BusinessDate)
        { _paymentError="Use the current business date."; return; }
        var method=SelectedPaymentMethod;
        if (method is null || !PaymentMethodAllowed(method) || _paymentEmployeeId is not { } employee || !references.Employees.Any(x=>x.Id==employee) || references.Currency is null)
        { _paymentError="Choose an available payment method and employee."; return; }
        if (method.RequiresPpkReceipt && !LegacyPaymentRules.IsValidPpkReceipt(_paymentReference))
        { _paymentError="Enter the actual ten-character PPK receipt number."; return; }
        if (method.RequiresBankTransfer && !(SelectedPaymentBank is { IsAvailable:true } selectedBank && selectedBank.Id==_paymentBankTransferId && selectedBank.AvailableAmount>=_paymentAmount))
        { _paymentError="Choose a bank transfer with a sufficient available amount."; return; }
        if (method.RequiresRegister && references.SelectedRegister?.Id!=_paymentRegisterId)
        { _paymentError="Choose an available register."; return; }
        if (_paymentNote.Length>500) { _paymentError="Payment notes can contain at most 500 characters."; return; }
        _busy=true;
        try
        {
            var input=new OrderPaymentInput(date,_paymentAmount,method.Id,employee,
                method.RequiresBankTransfer ? _paymentBankTransferId : null,
                method.RequiresPpkReceipt ? _paymentReference.Trim() : null,
                method.RequiresRegister ? _paymentRegisterId : null,
                string.IsNullOrWhiteSpace(_paymentNote) ? null : _paymentNote.Trim());
            var result=await Entry.PostPaymentAsync(id,new PostOrderPaymentCommand(Guid.NewGuid(),_order.Revision,input));
            ApplySnapshot(result.Order);
            _paymentSheetOpen=false; _paymentReferences=null; _paymentError="";
            _saveIsError=false; _saveMessage="Payment posted; balances refreshed.";
        }
        catch (SalesOrderEntryFailure ex) { _paymentError=ex.Message; _liveError=ex.Message; }
        finally { _busy=false; }
    }
}
