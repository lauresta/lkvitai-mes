using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using System.Data;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public async Task<OrderPaymentReferences> GetPaymentReferencesAsync(long orderId,CancellationToken ct,OrderPaymentReferenceSelection? selection=null)
    {
        await using var lease=await connections.OpenAsync(ct);var c=lease.Connection;
        await using(var setup=new SqlCommand("SET LOCK_TIMEOUT 5000",c))await setup.ExecuteNonQueryAsync(ct);
        OrderEditorSnapshot snapshot;
        await using(var read=await lease.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {
            snapshot=await ReadAsync(c,read,orderId,ct)??throw new OrderEntryException("NOT_FOUND","Order not found.",404);
            RequireScope(snapshot);await read.CommitAsync(ct);
        }
        // Choice lists need committed data, not serializable range locks over
        // every employee/bank row. The financial command revalidates exact IDs.
        await using var tx=await lease.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        var result=await PaymentReferences(c,tx,snapshot.Header,snapshot.Revision,selection,ct);
        await tx.CommitAsync(ct);return result;
    }

    public async Task<OrderPaymentBankTransferPage> SearchPaymentBankTransfersAsync(long orderId,string? search,int page,int pageSize,int? selectedId,CancellationToken ct)
    {
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);
        search=search?.Trim();if(search?.Length>150)throw new OrderEntryException("PAYMENT_SEARCH","Search is limited to150 characters.",400);
        await using var lease=await connections.OpenAsync(ct);var c=lease.Connection;
        await using(var setup=new SqlCommand("SET LOCK_TIMEOUT 5000",c))await setup.ExecuteNonQueryAsync(ct);
        await using var tx=await lease.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        var scope=(await Rows(c,tx,"SELECT s.FilialoID,u.UzsakymoSerija FROM dbo.Uzsakymai u JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija WHERE u.UzsakymasID=@order AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=s.FilialoID)",ct,("@order",orderId))).SingleOrDefault()
            ??throw new OrderEntryException("NOT_FOUND","Order not found.",404);
        if(I(scope,"FilialoID")!=7||!options.AllowedSeriesIds.Contains(I(scope,"UzsakymoSerija")!.Value))
            throw new OrderEntryException("TEST_BRANCH_ONLY","Payment choices are restricted to configured Testavimas orders.",403);
        if(!options.PaymentsEnabled)throw new OrderEntryException("PAYMENTS_DISABLED","Payment posting is disabled for this deployment.",403);
        var literal=search?.Replace("[","[[]").Replace("%","[%]").Replace("_","[_]");
        var pattern="%"+literal+"%";
        const string predicate="""
            b.Country='LT' AND b.Suma-ISNULL(b.mAssignedSum,0)>0
            AND (@search IS NULL OR b.Firma LIKE @pattern OR b.Paskirtis LIKE @pattern
                 OR CONVERT(nvarchar(16),b.NR)=@search OR CONVERT(varchar(10),b.Data,23) LIKE @pattern)
            """;
        var count=Convert.ToInt32(await Scalar(c,tx,"SELECT COUNT(*) FROM dbo.BankTransfersTable b WHERE "+predicate,ct,("@search",search),("@pattern",pattern)));
        var rows=await Rows(c,tx,"""
            SELECT b.NR,b.Data,b.Firma,b.Paskirtis,b.Suma,b.mAssignedSum,
                CAST(CASE WHEN dbo.fn_IsNowMyWorkPeriod(b.Data)='Y' THEN 1 ELSE 0 END AS bit) DateAllowed
            FROM dbo.BankTransfersTable b WHERE
            """+" "+predicate+" ORDER BY b.Data DESC,b.NR DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;",ct,
            ("@search",search),("@pattern",pattern),("@skip",((long)page-1)*pageSize),("@take",pageSize));
        var currencyCode=options.PaymentCurrencyId is null?null:Convert.ToString(await Scalar(c,tx,"SELECT ValiutosKodas FROM dbo.Zinynas_valiutos WHERE ValiutosID=@id",ct,("@id",options.PaymentCurrencyId)));
        var items=rows.Select(r=>BankReference(r,currencyCode)).ToArray();
        var selected=selectedId.HasValue?await PaymentBankReference(c,tx,selectedId.Value,currencyCode,ct):null;
        await tx.CommitAsync(ct);return new(items,count,page,pageSize,selected);
    }

    private async Task<OrderPaymentReferences> PaymentReferences(SqlConnection c,SqlTransaction tx,
        OrderHeaderSnapshot header,string revision,OrderPaymentReferenceSelection? selection,CancellationToken ct)
    {
        selection??=new();
        var date=DateOnly.FromDateTime(Convert.ToDateTime(await Scalar(c,tx,"SELECT CONVERT(date,GETDATE())",ct)));
        OrderPaymentReferences Unavailable(string reason,OrderPaymentCurrencyReference? currency=null)
            =>new(false,reason,date,revision,currency,null,null,[],[],[],[],selection);
        if(!Scope(header.BranchId,header.SeriesId))return Unavailable("Payment posting is restricted to configured Testavimas orders.");
        if(!options.PaymentsEnabled)return Unavailable("Payment posting is disabled for this deployment.");
        if(header.Cancelled)return Unavailable("Payments cannot be posted to a cancelled order through this test flow.");
        if(options.PaymentCurrencyId is null)return Unavailable("New-payment currency must be configured.");
        var currencyRow=(await Rows(c,tx,"SELECT ValiutosID,ValiutosKodas,Kursas FROM dbo.Zinynas_valiutos WHERE ValiutosID=@id",ct,("@id",options.PaymentCurrencyId))).SingleOrDefault();
        if(currencyRow is null||D(currencyRow,"Kursas") is null)return Unavailable("Configured payment currency is unavailable.");
        var currency=new OrderPaymentCurrencyReference(I(currencyRow,"ValiutosID")!.Value,S(currencyRow,"ValiutosKodas")??$"currency #{options.PaymentCurrencyId}",D(currencyRow,"Kursas")!.Value);
        var orderCurrency=await Scalar(c,tx,"SELECT ValiutosID FROM dbo.Uzsakymai WHERE UzsakymasID=@order",ct,("@order",header.Id));
        if(orderCurrency is null||Convert.ToInt32(orderCurrency)!=currency.Id)return Unavailable("Configured new-payment currency does not match this order. Legacy amounts are not converted.",currency);

        var registerRows=await PaymentRegisterRows(c,tx,ct);
        var registers=registerRows.Select(RegisterReference).ToArray();
        var selectedRegister=selection.MethodId==LegacyPaymentRules.RegisterMethodId
            ?registers.SingleOrDefault(r=>r.Id==selection.RegisterId):null;
        var selectedBank=selection.MethodId==LegacyPaymentRules.BankMethodId&&selection.BankTransferId.HasValue
            ?await PaymentBankReference(c,tx,selection.BankTransferId.Value,currency.Code,ct):null;
        var employeeRows=await Rows(c,tx,"""
            SELECT e.DarbuotojoID,e.DarbuotojoPavadinimas,e.FilialasID,e.bHideInApplicationFlag
            FROM dbo.Zinynas_darbuotojai e
            WHERE e.bHideInApplicationFlag=0 AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() l WHERE l.UKS=e.FilialasID)
            ORDER BY e.DarbuotojoPavadinimas,e.DarbuotojoID;
            """,ct);
        var employees=employeeRows.Select(EmployeeReference).ToDictionary(e=>e.Id);
        var extraIds=options.AllowedPaymentEmployeeIds.AsEnumerable();
        if(selectedRegister?.DefaultEmployeeId is { } nativeDefault)extraIds=extraIds.Append(nativeDefault);
        foreach(var id in extraIds.Distinct())
        {
            var row=(await Rows(c,tx,"SELECT e.DarbuotojoID,e.DarbuotojoPavadinimas,e.FilialasID,e.bHideInApplicationFlag FROM dbo.Zinynas_darbuotojai e WHERE e.DarbuotojoID=@id AND (@registerDefault=1 OR EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=e.FilialasID))",ct,("@id",id),("@registerDefault",selectedRegister?.DefaultEmployeeId==id))).SingleOrDefault();
            if(row is not null)employees[id]=EmployeeReference(row);
        }
        var approvedCashDefault=options.AllowedPaymentEmployeeIds.Distinct().ToArray();
        int? cashDefault=approvedCashDefault.Length==1&&employees.ContainsKey(approvedCashDefault[0])?approvedCashDefault[0]:null;
        int? bankDefault=employees.ContainsKey(LegacyPaymentRules.BankDefaultEmployeeId)?LegacyPaymentRules.BankDefaultEmployeeId:null;
        var methodRows=await Rows(c,tx,"SELECT MokejimoTipasID,MokejimoTipas FROM dbo.Zinynas_mokejimotipai ORDER BY MokejimoTipas,MokejimoTipasID",ct);
        var methods=methodRows.Select(r=>
        {
            var id=I(r,"MokejimoTipasID")!.Value;var supported=LegacyPaymentRules.IsSupportedMethod(id);
            return new OrderPaymentMethodReference(id,S(r,"MokejimoTipas")??$"method #{id}",id==LegacyPaymentRules.PpkMethodId,
                id==LegacyPaymentRules.BankMethodId,id==LegacyPaymentRules.RegisterMethodId,
                id==LegacyPaymentRules.BankMethodId?bankDefault:id==LegacyPaymentRules.CashMethodId?cashDefault:null,
                supported,supported?null:"This legacy method is not implemented.");
        }).ToArray();
        string? prerequisite=null;int? defaultEmployee=null;
        if(selection.MethodId is { } method)
        {
            if(!methods.Any(m=>m.Id==method&&m.IsSupported))prerequisite="Select an available payment method.";
            else if(method==LegacyPaymentRules.BankMethodId)
            {
                if(selectedBank?.IsAvailable!=true)prerequisite=selectedBank?.UnavailableReason??"Choose an available Lithuania bank transfer.";
                else defaultEmployee=bankDefault;
            }
            else if(method==LegacyPaymentRules.RegisterMethodId)
            {
                if(selectedRegister is null)prerequisite="Choose an accessible cash register.";
                else if(selectedRegister.DefaultEmployeeId is { } receiver&&employees.ContainsKey(receiver))defaultEmployee=receiver;
            }
            else if(method==LegacyPaymentRules.CashMethodId)defaultEmployee=cashDefault;
        }
        var period=await Scalar(c,tx,"SELECT dbo.fn_IsNowMyWorkPeriod(CONVERT(date,GETDATE()))",ct);
        var enabled=string.Equals(Convert.ToString(period),"Y",StringComparison.Ordinal)&&employees.Count>0;
        var reason=enabled?null:employees.Count==0?"No source-valid payment receiver is available.":"The current SQL account's payment work period does not include the current business date.";
        return new(enabled,reason,date,revision,currency,defaultEmployee,methods.Length==1?methods[0].Id:null,
            methods,employees.Values.OrderBy(e=>e.Name,StringComparer.CurrentCulture).ThenBy(e=>e.Id).ToArray(),registers,[],selection,selectedBank,selectedRegister,prerequisite,
            LegacyPaymentRules.AmountDecimalPlaces,LegacyPaymentRules.MaximumAdditionalAmount(header.Paid));
    }

    private async Task<List<Dictionary<string,object?>>> PaymentRegisterRows(SqlConnection c,SqlTransaction tx,CancellationToken ct)
        =>await Rows(c,tx,"""
            SELECT e.EKA_ID,e.EKA_NAME,e.DeptID,e.DefaulEmployeeID,w.DarbuotojoPavadinimas,w.bHideInApplicationFlag
            FROM dbo.Zinynas_EKA e
            LEFT JOIN dbo.Zinynas_darbuotojai w ON w.DarbuotojoID=e.DefaulEmployeeID
            WHERE EXISTS(SELECT 1 FROM dbo.Loc_leidimas() l WHERE l.UKS=e.DeptID)
            ORDER BY e.EKA_NAME,e.EKA_ID;
            """,ct);
    private static OrderPaymentRegisterReference RegisterReference(Dictionary<string,object?> row)
        =>new(I(row,"EKA_ID")!.Value,S(row,"EKA_NAME")??$"register #{I(row,"EKA_ID")}",I(row,"DefaulEmployeeID"),I(row,"DeptID"),S(row,"DarbuotojoPavadinimas")?.Trim(),B(row,"bHideInApplicationFlag"));
    private static OrderPaymentEmployeeReference EmployeeReference(Dictionary<string,object?> row)
        =>new(I(row,"DarbuotojoID")!.Value,S(row,"DarbuotojoPavadinimas")?.Trim()??$"employee #{I(row,"DarbuotojoID")}",I(row,"FilialasID"),B(row,"bHideInApplicationFlag"));
    private async Task<OrderPaymentBankTransferReference?> PaymentBankReference(SqlConnection c,SqlTransaction tx,int id,string? currencyCode,CancellationToken ct)
    {
        var row=(await Rows(c,tx,"""
            SELECT b.NR,b.Data,b.Firma,b.Paskirtis,b.Suma,b.mAssignedSum,
                CAST(CASE WHEN dbo.fn_IsNowMyWorkPeriod(b.Data)='Y' THEN 1 ELSE 0 END AS bit) DateAllowed
            FROM dbo.BankTransfersTable b WHERE b.NR=@id AND b.Country='LT';
            """,ct,("@id",id))).SingleOrDefault();
        return row is null?null:BankReference(row,currencyCode);
    }
    private static OrderPaymentBankTransferReference BankReference(Dictionary<string,object?> row,string? currencyCode)
    {
        var remaining=(D(row,"Suma")??0)-(D(row,"mAssignedSum")??0);
        var allowed=remaining>=LegacyPaymentRules.SmallestAmount&&B(row,"DateAllowed");
        var reason=remaining<=0?"This bank transfer has no unassigned amount.":remaining<LegacyPaymentRules.SmallestAmount
            ?"The raw unassigned remainder is below the payment table's 0.01 storage precision. It cannot be allocated without rounding."
            :!B(row,"DateAllowed")?"The bank transfer date is outside the current SQL account's posting period.":null;
        return new(I(row,"NR")!.Value,Date(row,"Data"),remaining,S(row,"Firma"),S(row,"Paskirtis"),D(row,"Suma"),D(row,"mAssignedSum"),allowed,reason,LegacyPaymentRules.BankDefaultEmployeeId,PostingCurrencyCode:currencyCode);
    }
}
