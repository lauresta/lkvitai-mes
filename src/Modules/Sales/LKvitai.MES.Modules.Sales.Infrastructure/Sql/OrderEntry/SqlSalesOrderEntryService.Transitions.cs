using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public Task<OrderEntryCommandResult> CancelAsync(Guid actorId,long orderId,CancelOrderCommand command,CancellationToken ct)
    {
        if(!options.CancellationEnabled)throw new OrderEntryException("CANCELLATION_DISABLED",CancellationDisabledReason,403);
        return Execute(actorId,command.CommandId,"order.cancel.v1",orderId,command.ExpectedRevision,command,async(c,tx,before,token)=>
        {
            if(before!.Header.StateId is not(4 or 7))throw new OrderEntryException("CANCELLATION_STATE","Only entered or stopped orders can be cancelled.",409);
            if(!before.Header.Cancelled)
            {
                if(await CancellationAuditUnavailableReason(c,tx,orderId,token) is { } reason)
                    throw new OrderEntryException("LEGACY_AUDIT_ID_RANGE",reason,422);
                await NonQuery(c,tx,"EXEC dbo.sp_CancellOrder @order",token,("@order",orderId));
            }
            return(orderId,(long?)null);
        },ct);
    }

    private const string CancellationDisabledReason="Order cancellation is disabled for this deployment. The legacy audit bigint compatibility issue is deferred by the owner.";
    private async Task<string?> CancellationAuditUnavailableReason(Microsoft.Data.SqlClient.SqlConnection c,
        Microsoft.Data.SqlClient.SqlTransaction tx,long orderId,CancellationToken ct)
    {
        if(!options.CancellationEnabled)return CancellationDisabledReason;
        var metadata=await Rows(c,tx,"""
            SELECT c.system_type_id SystemType,c.user_type_id UserType,c.is_nullable IsNullable
            FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.AUDIT_Anuliavimai',N'U') AND c.name=N'OrderID';
            """,ct);
        if(metadata.Count!=1||I(metadata[0],"UserType")!=I(metadata[0],"SystemType")
            ||B(metadata[0],"IsNullable")||I(metadata[0],"SystemType") is not(56 or 127))
            throw new OrderEntryException("CANCELLATION_AUDIT_CONFIGURATION","Legacy cancellation audit storage is not compatible with the reviewed schema.",503);
        return I(metadata[0],"SystemType")==56&&(orderId<int.MinValue||orderId>int.MaxValue)
            ?"Cancellation is unavailable until the legacy audit OrderID column supports bigint order IDs. The order has not been cancelled."
            :null;
    }
    public Task<OrderEntryCommandResult> DeleteDutyAsync(Guid actorId,long orderId,int dutyId,int employeeId,DeleteOrderDutyCommand command,CancellationToken ct)
        => Execute(actorId,command.CommandId,"duty.delete.v1",orderId,command.ExpectedRevision,new{dutyId,employeeId,command},async(c,tx,before,token)=>
        {
            if(!before!.Duties.Any(d=>d.DutyId==dutyId&&d.EmployeeId==employeeId))throw new OrderEntryException("NOT_FOUND","Duty assignment was not found.",404);
            // One row at a time invokes the actual protected-manager/acquisition-period triggers.
            await NonQuery(c,tx,"DELETE dbo.TCR_OrdersDutiesEmployees WHERE iOrderID=@order AND iDutyID=@duty AND iEmployeeID=@employee",token,("@order",orderId),("@duty",dutyId),("@employee",employeeId));
            return(orderId,(long?)null);
        },ct);
    public async Task<OrderEntryCommandResult> SetFinalPriceAsync(Guid actorId,long orderId,SetOrderFinalPriceCommand command,CancellationToken ct)
    {
        var result=await Execute(actorId,command.CommandId,"order.final-price.v1",orderId,command.ExpectedRevision,command,async(c,tx,before,token)=>
        {
            RequireEditable(before!);
            var discount=LegacyOrderFinalPrice.Discount(command.FinalPrice,before!.Header.DiscountableTotal??0,before.Header.NonDiscountableTotal??0);
            ValidateDiscount(discount);
            if(discount!=(before.Header.DiscountPercent??0))
                await NonQuery(c,tx,"UPDATE dbo.Uzsakymai SET Nuolaida=@discount WHERE UzsakymasID=@order",token,("@discount",discount),("@order",orderId));
            await SyncStoredTotal(c,tx,orderId,token);
            return(orderId,(long?)null);
        },ct);
        // The source CCur discount ratio and SQL money/100 conversion can differ
        // by a cent. Preserve those rules and report the actual committed total,
        // including on receipt replay; never claim the requested amount was saved.
        var actual=result.Order.Header.StoredTotal??result.Order.Header.CalculatedTotal;
        if(actual!=command.FinalPrice)
        {
            var warning=$"Legacy rounding saved final total {actual.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)} instead of requested {command.FinalPrice.ToString("0.00##",System.Globalization.CultureInfo.InvariantCulture)}.";
            result=result with { Order=result.Order with { Warnings=result.Order.Warnings.Append(warning).ToArray() } };
        }
        return result;
    }
}
