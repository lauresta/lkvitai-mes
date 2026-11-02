using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

namespace LKvitai.MES.Modules.Sales.WebUI.Components;

public partial class NewOrderWorkspace
{
    private async Task RecoverCommandAsync()
    {
        if(Entry.PendingCommandId is not { } id)return;
        _busy=true;
        try
        {
            OrderEntryCommandResult result;
            try
            {
                var outcome=await Entry.OutcomeAsync(id);
                if(Entry.PendingOrderId is { } expected && outcome.OrderId!=expected)
                    throw new Services.SalesOrderEntryFailure("The earlier result refers to another order. Recovery input is retained; this editor has not been changed.",true,409,"RECOVERY_SCOPE_MISMATCH");
                var snapshot=await Entry.ReadAsync(outcome.OrderId);
                result=new(snapshot with{Warnings=snapshot.Warnings.Concat(outcome.Warnings ?? []).Distinct().ToArray()},outcome.ProductId,true,outcome.PaymentId,outcome.ProductionDocumentId);
                await Entry.ClearRecoveredAsync(id);
            }
            catch(Services.SalesOrderEntryFailure ex) when(ex.Status==404)
            {
                // A missing receipt is not permission to generate another ID.
                result=await Entry.RetryPendingAsync();
            }
            if(_order?.LegacyId is not { } current || result.Order.Header.Id!=current)
            {
                _leaving=true;
                Navigation.NavigateTo("entry/"+result.Order.Header.Id);
                return;
            }
            ApplySnapshot(result.Order,result.ProductId);
            if(result.PaymentId is not null){_paymentSheetOpen=false;_saveIsError=false;_saveMessage="Payment posted; balances refreshed.";}
            if(result.ProductionDocumentId is not null)ProductionConfirmationRecovered();
            _liveError="The saved result was recovered. Current values are shown."+(string.IsNullOrWhiteSpace(_liveError)?"":" "+_liveError);
        }
        catch(Services.SalesOrderEntryFailure ex){_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private async Task CheckLegacySaveAsync()
    {
        if(Entry.LegacyPendingId is not { } id)return;
        _busy=true;
        try
        {
            _legacySaveOutcome=await Entry.OutcomeAsync(id);
            await Entry.ClearRecoveredLegacyAsync(id);
            _liveError="The earlier saved result was found. Open its order using the link; this editor has not been changed.";
        }
        catch(Services.SalesOrderEntryFailure ex)
        {
            _liveError=ex.Status==404
                ? "No confirmed result was found for that earlier save. Its recovery marker is retained. Check the original order before entering the same operation again. Other orders remain available."
                : ex.Message;
        }
        finally{_busy=false;}
    }
    private void OpenOtherOrders() => Navigation.NavigateTo("");
}
