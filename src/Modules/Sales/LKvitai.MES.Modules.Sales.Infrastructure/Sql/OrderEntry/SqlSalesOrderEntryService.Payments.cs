using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public Task<OrderEntryCommandResult> PostPaymentAsync(Guid actorId,long orderId,PostOrderPaymentCommand command,CancellationToken ct)
    {
        if(!options.PaymentsEnabled)throw new OrderEntryException("PAYMENTS_DISABLED","Payment posting is not enabled for this deployment.",403);
        return Execute(actorId,command.CommandId,"payment.post.v1",orderId,command.ExpectedRevision,command,async(c,tx,before,token)=>
        {
            var p=command.Payment??throw new OrderEntryException("PAYMENT_REQUIRED","Payment input is required.",400);
            if(before!.Header.Cancelled)throw new OrderEntryException("ORDER_CANCELLED","Payments cannot be posted to a cancelled order through this test flow.",409);
            await RequireFinancialSchema(c,tx,token);
            if(!options.PaymentCurrencyId.HasValue)
                throw new OrderEntryException("PAYMENT_CONFIGURATION","Verified payment currency must be configured.",503);
            if(!LegacyPaymentRules.IsSupportedMethod(p.MethodId)||await Scalar(c,tx,"SELECT 1 FROM dbo.Zinynas_mokejimotipai WITH(HOLDLOCK) WHERE MokejimoTipasID=@method",token,("@method",p.MethodId)) is null)
                throw new OrderEntryException("PAYMENT_METHOD","Choose an existing supported legacy payment method.");
            if(!LegacyPaymentRules.IsValidPositiveAmount(p.Amount))
                throw new OrderEntryException("PAYMENT_AMOUNT","Enter a positive amount with two decimal places, matching legacy payment storage; refunds are not enabled.");
            if(p.Note?.Length>500)throw new OrderEntryException("PAYMENT_NOTE","Payment note is limited to 500 characters.");
            var serverDate=Convert.ToDateTime(await Scalar(c,tx,"SELECT CONVERT(date,GETDATE())",token));
            if(p.Date!=DateOnly.FromDateTime(serverDate))
                throw new OrderEntryException("PAYMENT_DATE","Only the current SQL business date is enabled for contained test postings.");
            await RequireExists(c,tx,"SELECT 1 FROM dbo.Zinynas_valiutos WHERE ValiutosID=@id",options.PaymentCurrencyId.Value,"Payment currency",token);
            var orderCurrency=await Scalar(c,tx,"SELECT ValiutosID FROM dbo.Uzsakymai WITH(HOLDLOCK) WHERE UzsakymasID=@order",token,("@order",orderId));
            if(orderCurrency is null||Convert.ToInt32(orderCurrency)!=options.PaymentCurrencyId.Value)
                throw new OrderEntryException("PAYMENT_CURRENCY_MISMATCH","The configured new-payment currency must match the order currency. No conversion of legacy amounts is performed.",422);
            int? selectedRegisterDefault=null;
            switch(p.MethodId)
            {
                case 1:
                    if(p.BankTransferId.HasValue||p.RegisterId.HasValue||!LegacyPaymentRules.IsValidPpkReceipt(p.PpkReceipt))
                        throw new OrderEntryException("PPK_REFERENCE","Enter one valid ten-character PPK receipt and no bank/register reference.");
                    break;
                case 2:
                    if(!p.BankTransferId.HasValue||p.RegisterId.HasValue||!string.IsNullOrEmpty(p.PpkReceipt))
                        throw new OrderEntryException("TRANSFER_SCOPE","Choose a bank transfer and clear other methods' document references.");
                    var transfer=(await Rows(c,tx,"SELECT Data,Suma,mAssignedSum FROM dbo.BankTransfersTable WITH(UPDLOCK,HOLDLOCK) WHERE NR=@id AND Country='LT'",token,("@id",p.BankTransferId.Value))).SingleOrDefault();
                    if(transfer is null)throw new OrderEntryException("TRANSFER_UNAVAILABLE","Choose an existing Lithuania bank transfer.");
                    if((D(transfer,"Suma")??0)-(D(transfer,"mAssignedSum")??0)<p.Amount)
                        throw new OrderEntryException("TRANSFER_AVAILABLE","Bank transfer does not have enough unassigned amount.");
                    if(!string.Equals(Convert.ToString(await Scalar(c,tx,"SELECT dbo.fn_IsNowMyWorkPeriod(@date)",token,("@date",transfer["Data"]))),"Y",StringComparison.Ordinal))
                        throw new OrderEntryException("TRANSFER_PERIOD_CLOSED","The bank transfer date is outside the current SQL account's posting period.");
                    break;
                case 81423987:
                    if(p.BankTransferId.HasValue||p.RegisterId.HasValue||!string.IsNullOrEmpty(p.PpkReceipt))throw new OrderEntryException("CASH_REFERENCE","Cash posting must not carry another method's document reference.");
                    break;
                case 834019474:
                    if(!p.RegisterId.HasValue||p.BankTransferId.HasValue||!string.IsNullOrEmpty(p.PpkReceipt))throw new OrderEntryException("REGISTER_SCOPE","Choose a register and clear other methods' document references.");
                    var register=(await Rows(c,tx,"""
                        SELECT e.DefaulEmployeeID FROM dbo.Zinynas_EKA e WITH(HOLDLOCK)
                        WHERE e.EKA_ID=@register AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() l WHERE l.UKS=e.DeptID);
                        """,token,("@register",p.RegisterId.Value))).SingleOrDefault();
                    if(register is null)throw new OrderEntryException("REGISTER_UNAVAILABLE","Choose an existing cash register from an accessible department.");
                    selectedRegisterDefault=I(register,"DefaulEmployeeID");
                    break;
                default:throw new OrderEntryException("PAYMENT_METHOD","This legacy payment method is not implemented.");
            }
            var receiver=(await Rows(c,tx,"""
                SELECT CAST(CASE WHEN e.bHideInApplicationFlag=0 AND EXISTS
                    (SELECT 1 FROM dbo.Loc_leidimas() l WHERE l.UKS=e.FilialasID) THEN 1 ELSE 0 END AS bit) SourceVisible,
                    CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=e.FilialasID) THEN 1 ELSE 0 END AS bit) DepartmentAllowed,
                    ISNULL(e.CashAmountOnHand,0) ReceiverCash
                FROM dbo.Zinynas_darbuotojai e WITH(UPDLOCK,HOLDLOCK) WHERE e.DarbuotojoID=@employee;
                """,token,("@employee",p.EmployeeId))).SingleOrDefault();
            if(receiver is null||!LegacyPaymentRules.IsReceiverAllowed(B(receiver,"SourceVisible"),
                options.AllowedPaymentEmployeeIds.Contains(p.EmployeeId)&&B(receiver,"DepartmentAllowed"),selectedRegisterDefault==p.EmployeeId))
                throw new OrderEntryException("PAYMENT_RECEIVER_SCOPE","Choose a visible accessible receiver, an explicitly approved receiver, or this selected register's actual default.",403);
            LegacyPaymentRules.ValidateBalanceAddition(p.Amount,before.Header.Paid,D(receiver,"ReceiverCash")??0);
            // Match the actual trigger's SYSTEM_USER policy immediately before
            // the first DML. A closed period is a definitive validation failure,
            // not an unknown committed outcome; never substitute another user.
            var period=await Scalar(c,tx,"SELECT dbo.fn_IsNowMyWorkPeriod(@date)",token,("@date",p.Date.ToDateTime(TimeOnly.MinValue)));
            if(!string.Equals(Convert.ToString(period),"Y",StringComparison.Ordinal))
                throw new OrderEntryException("PERIOD_CLOSED","The current SQL account's payment work period does not include this date. No payment has been posted.",422);
            await SyncStoredTotal(c,tx,orderId,token);
            var id=Convert.ToInt64(await Scalar(c,tx,"""
                DECLARE @ids table(Id bigint NOT NULL);
                INSERT dbo.Mokejimai(UzsakymasID,Data,Pastabos,Suma,ValiutosID,MokeijimoTipasID,EmployeeID,BankTransferNR,PPKKvitasNR,EKA_ID)
                OUTPUT INSERTED.MokejimoID INTO @ids
                VALUES(@order,@date,@note,@amount,@currency,@method,@employee,@transfer,@ppk,@register);
                SELECT Id FROM @ids;
                """,token,("@order",orderId),("@date",p.Date.ToDateTime(TimeOnly.MinValue)),("@note",p.Note),("@amount",p.Amount),("@currency",options.PaymentCurrencyId.Value),("@method",p.MethodId),("@employee",p.EmployeeId),("@transfer",p.BankTransferId),("@ppk",p.PpkReceipt),("@register",p.RegisterId)));
            return(orderId,(long?)id);
        },ct);
    }
    private async Task RequireFinancialSchema(SqlConnection c,SqlTransaction tx,CancellationToken ct)
    {
        if(await Scalar(c,tx,"SELECT COL_LENGTH(N'mes_sales.CommandReceipt',N'RelatedEntityId')",ct) is null)
            throw new OrderEntryException("RECEIPT_SCHEMA","Apply the reviewed additive receipt schema before enabling this operation.",503);
    }
}
