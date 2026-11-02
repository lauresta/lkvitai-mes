using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Components;

namespace LKvitai.MES.Modules.Sales.WebUI.Components;

public partial class NewOrderWorkspace
{
    private bool _confirmationSheetOpen;
    private string _confirmationError="";
    private string _confirmationRevision="";
    private readonly HashSet<long> _confirmationSelected=[];
    private bool CanConfirmLiveProducts => IsLive && _order is { ConfirmationEnabled:true, WritesAllowed:true, Cancelled:false };
    private bool ConfirmationDirty => IsLive && _confirmationSheetOpen && _confirmationSelected.Count>0;
    private IReadOnlyList<DemoOrderLine> ConfirmationCandidates => _order?.Lines.Where(line =>
        line.LegacyId is not null && line.IsManufactured==true && line.Quantity>0 && line.HasStoredPrice && line.HasStoredDimensions && line.StateId is 4 or 7).ToArray() ?? [];

    private async Task OpenConfirmationAsync()
    {
        if(!CanConfirmLiveProducts || _order?.LegacyId is not { } id)return;
        if(_paymentSheetOpen){_liveError="Close the payment sheet before confirming production.";return;}
        if(HeaderDirty||ProductDirty||AddressDirty||PaymentDirty)
        { _liveError="Save or discard current header, product, address and payment inputs before confirming production.";return; }
        _busy=true;
        try
        {
            ApplySnapshot(await Entry.ReadAsync(id));
            if(!CanConfirmLiveProducts){_liveError="Production confirmation is unavailable for this order.";return;}
            _confirmationSelected.Clear();_confirmationRevision=_order!.Revision;
            _confirmationError="";_confirmationSheetOpen=true;
        }
        catch(SalesOrderEntryFailure ex){_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private void SelectConfirmationProduct(long id,ChangeEventArgs e)
    {
        if(e.Value is true)_confirmationSelected.Add(id);else _confirmationSelected.Remove(id);
    }
    private void CloseConfirmation()
    {
        if(Entry.HasPendingCommand){_confirmationError="Recover the outstanding command before closing confirmation.";return;}
        _confirmationSheetOpen=false;_confirmationSelected.Clear();
    }
    private async Task ConfirmSelectedProductsAsync()
    {
        if(!CanConfirmLiveProducts||_order?.LegacyId is not { } id)return;
        if(HeaderDirty||ProductDirty||AddressDirty||PaymentDirty||_order.Revision!=_confirmationRevision)
        { _confirmationError="The order or inputs changed. Save or discard edits, then reopen confirmation.";return; }
        if(_confirmationSelected.Count is <1 or >500 || _confirmationSelected.Any(id=>!ConfirmationCandidates.Any(line=>line.LegacyId==id)))
        { _confirmationError="Select between one and 500 saved manufactured products.";return; }
        _busy=true;
        try
        {
            var command=new ConfirmOrderProductsCommand(Guid.NewGuid(),_order.Revision,_confirmationSelected.OrderBy(x=>x).ToArray());
            var result=await Entry.ConfirmProductsAsync(id,command);
            ApplySnapshot(result.Order);
            ProductionConfirmationRecovered();
        }
        catch(SalesOrderEntryFailure ex){_confirmationError=ex.Message;_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private void ProductionConfirmationRecovered()
    {
        _confirmationSheetOpen=false;_confirmationSelected.Clear();_confirmationError="";
        _saveIsError=false;_saveMessage="Products confirmed for production.";
    }
}
