using System.Data;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public Task<OrderEntryCommandResult> CreateAsync(Guid actorId,CreateOrderCommand command,CancellationToken ct)
        => Execute(actorId,command.CommandId,"header.create.v1",null,null,command,async(c,tx,_,token)=>
        {
            var h=command.Header;ValidateHeader(h);ValidateDiscount(h.DiscountPercent);
            var series=(await Rows(c,tx,"SELECT s.* FROM dbo.Zinynas_UzsakymoSerijos s WITH(UPDLOCK,HOLDLOCK) WHERE s.UzsakymoSerijaID=@id AND s.bHideInApplicationFlag=0 AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=s.FilialoID)",token,("@id",h.SeriesId))).SingleOrDefault()??throw new OrderEntryException("NOT_FOUND","Series not found.",404);
            if(I(series,"FilialoID")!=7 || !options.AllowedSeriesIds.Contains(h.SeriesId))throw new OrderEntryException("TEST_BRANCH_ONLY","Choose a configured Testavimas series.",403);
            var year=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius")).Year;
            var number=LegacyOrderNumber.Normalize(h.Number,S(series,"UzsakymoSerijaPav")!,year);
            await ValidateHeaderReferences(c,tx,h,token);
            var id=Convert.ToInt64(await Scalar(c,tx,"""
                DECLARE @ids table(Id bigint NOT NULL);
                INSERT dbo.Uzsakymai(UzsakymoSerija,UzsakymoNumeris,UzsakymoGrupe,UzsakymoData,UzsakymoTerminas,
                    UzsakovoID,VietaID,VadybininkoID,KonsultantoID,PriorityID,Montavimo_data,sNotes,Nuolaida,ValiutosID)
                OUTPUT INSERTED.UzsakymasID INTO @ids
                VALUES(@series,@number,@group,@date,@due,@customer,@place,@manager,@consultant,@priority,@mount,@note,@discount,2);
                SELECT Id FROM @ids;
                """,token,("@series",h.SeriesId),("@number",number.Sequence),("@group",number.Group),("@date",h.Date.ToDateTime(TimeOnly.MinValue)),("@due",h.Due.ToDateTime(TimeOnly.MinValue)),("@customer",h.CustomerId),("@place",h.StoreId),("@manager",h.ManagerId),("@consultant",h.ConsultantId),("@priority",h.PriorityId),("@mount",h.MountingDate?.ToDateTime(TimeOnly.MinValue)),("@note",h.Note),("@discount",h.DiscountPercent)));
            return(id,(long?)null);
        },ct);

    public Task<OrderEntryCommandResult> UpdateAsync(Guid actorId,long orderId,UpdateOrderCommand command,CancellationToken ct)
        => Execute(actorId,command.CommandId,"header.update.v1",orderId,command.ExpectedRevision,command,async(c,tx,before,token)=>
        {
            var h=command.Header;var old=before!.Header;ValidateHeader(h);
            if(h.DiscountPercent!=(old.DiscountPercent??0))ValidateDiscount(h.DiscountPercent);
            if(h.SeriesId!=old.SeriesId || !string.Equals(h.Number,old.Number,StringComparison.OrdinalIgnoreCase))
                throw new OrderEntryException("NUMBER_IMMUTABLE","Persisted order series and number cannot be changed through this editor.",409);
            // Place/due/mount/notes have independent legacy entry paths. Other fields require header permission.
            if(h.CustomerId!=old.CustomerId||h.ManagerId!=old.ManagerId||h.ConsultantId!=old.ConsultantId||h.PriorityId!=old.PriorityId||h.DiscountPercent!=(old.DiscountPercent??0)||h.Date!=old.Date)
                RequireEditable(before);
            await ValidateHeaderReferences(c,tx,h,token,old);
            var changes=new List<(string Column,object? Value)>();
            if(h.Date!=old.Date)changes.Add(("UzsakymoData",h.Date.ToDateTime(TimeOnly.MinValue)));
            if(h.Due!=old.Due)changes.Add(("UzsakymoTerminas",h.Due.ToDateTime(TimeOnly.MinValue)));
            if(h.MountingDate!=old.MountingDate)changes.Add(("Montavimo_data",h.MountingDate?.ToDateTime(TimeOnly.MinValue)));
            if(h.CustomerId!=old.CustomerId)changes.Add(("UzsakovoID",h.CustomerId));
            if(h.StoreId!=old.StoreId)changes.Add(("VietaID",h.StoreId));
            if(h.ManagerId!=old.ManagerId)changes.Add(("VadybininkoID",h.ManagerId));
            if(h.ConsultantId!=old.ConsultantId)changes.Add(("KonsultantoID",h.ConsultantId));
            if(h.PriorityId!=old.PriorityId)changes.Add(("PriorityID",h.PriorityId));
            if(h.Note!=old.Note)changes.Add(("sNotes",h.Note));
            if(h.DiscountPercent!=(old.DiscountPercent??0))changes.Add(("Nuolaida",h.DiscountPercent));
            if(changes.Count!=0)
            {
                var parameters=changes.Select((x,i)=>("@v"+i,x.Value)).Append(("@id",(object?)orderId)).ToArray();
                await NonQuery(c,tx,"UPDATE dbo.Uzsakymai SET "+string.Join(",",changes.Select((x,i)=>x.Column+"=@v"+i))+" WHERE UzsakymasID=@id",token,parameters);
            }
            await SyncStoredTotal(c,tx,orderId,token);
            return(orderId,(long?)null);
        },ct);

    private static void ValidateHeader(OrderHeaderInput h)
    {
        if(h is null)throw new OrderEntryException("HEADER_REQUIRED","Order header is required.",400);
        if(h.Date<new DateOnly(1753,1,1)||h.Due<new DateOnly(1753,1,1)||h.MountingDate<new DateOnly(1753,1,1))throw new OrderEntryException("INVALID_DATE","Enter a valid SQL order date.");
        if(h.Note?.Length>128)throw new OrderEntryException("NOTE_LENGTH","Order note is limited to 128 characters.");
        // >=20 invokes real subscription mail; deliberately unavailable in the initial shared-production test slice.
        if(h.DiscountPercent< -99999999.9999999999m || decimal.Round(h.DiscountPercent,10)!=h.DiscountPercent)throw new OrderEntryException("DISCOUNT_RANGE","Discount exceeds legacy decimal precision.");
    }
    private static void ValidateDiscount(decimal discount)
    {
        if(discount>=20)throw new OrderEntryException("DISCOUNT_NOT_ENABLED","This discount requires a separately verified mail/operational path.");
    }
    private async Task ValidateHeaderReferences(SqlConnection c,SqlTransaction tx,OrderHeaderInput h,CancellationToken ct,OrderHeaderSnapshot? existing=null)
    {
        if(existing is null || h.CustomerId!=existing.CustomerId)
            await RequireExists(c,tx,"SELECT 1 FROM dbo.Zinynas_uzsakovai c WHERE c.UzsakovoID=@id AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=c.PriskirtasFilialui)",h.CustomerId,"Customer",ct);
        if(existing is null || h.StoreId!=existing.StoreId)
            await RequireExists(c,tx,"SELECT 1 FROM dbo.Zinynas_vieta v WHERE v.VietaID=@id AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=v.Filialas)",h.StoreId,"Store",ct);
        if(h.ManagerId.HasValue && (existing is null || h.ManagerId!=existing.ManagerId))
            await RequireExists(c,tx,"SELECT 1 FROM dbo.Zinynas_darbuotojai e WHERE e.DarbuotojoID=@id AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=e.FilialasID)",h.ManagerId.Value,"Manager",ct);
        if(h.ConsultantId.HasValue && (existing is null || h.ConsultantId!=existing.ConsultantId))
            await RequireExists(c,tx,"SELECT 1 FROM dbo.Zinynas_darbuotojai e WHERE e.DarbuotojoID=@id AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=e.FilialasID)",h.ConsultantId.Value,"Consultant",ct);
        await RequireExists(c,tx,"SELECT 1 FROM dbo.DIC_Priorities WHERE PriorityID=@id",h.PriorityId,"Priority",ct);
    }
    private async Task RequireExists(SqlConnection c,SqlTransaction tx,string text,int id,string name,CancellationToken ct)
    {if(await Scalar(c,tx,text,ct,("@id",id)) is null)throw new OrderEntryException("INVALID_REFERENCE",name+" reference is unavailable.");}
    private async Task<IReadOnlyList<string>> CheckEligibility(SqlConnection c,SqlTransaction tx,int customerId,CancellationToken ct)
    {
        var customer=(await Rows(c,tx,"SELECT NeleistiPardavinetiJeiVeluojaMoketi,Agnum_KLID,VeluotuNeapmoketuSaskaituKiekis FROM dbo.Zinynas_uzsakovai WHERE UzsakovoID=@id",ct,("@id",customerId))).SingleOrDefault()??throw new OrderEntryException("INVALID_CUSTOMER","Customer does not exist.");
        if(S(customer,"NeleistiPardavinetiJeiVeluojaMoketi")!="N" && I(customer,"Agnum_KLID").HasValue && (I(customer,"VeluotuNeapmoketuSaskaituKiekis")??0)!=0)
            throw new OrderEntryException("CUSTOMER_OVERDUE","Customer overdue invoices block product entry.");
        // This inspected procedure performs SELECTs only; keep its precedence/null rules intact.
        var eligibility=await Rows(c,tx,"EXEC dbo.sproc_CheckIfNewOrdersAllowedByCustID_forVBA @id",ct,("@id",customerId));
        if(eligibility.Count!=1)throw new DataException("Customer eligibility did not return one result.");
        var action=I(eligibility[0],"@action");var message=S(eligibility[0],"@msg")??"Customer eligibility restriction.";
        if(action==0)throw new OrderEntryException("CUSTOMER_BLOCKED",message);
        if(action is null || action is <0 or >3)throw new OrderEntryException("INVALID_ELIGIBILITY_ACTION","The customer has an unsupported eligibility action. Select a customer with a verified policy.");
        return action is 1 or 3 ? new[]{message} : [];
    }
    private async Task SyncStoredTotal(SqlConnection c,SqlTransaction tx,long orderId,CancellationToken ct)
    {
        // Same final amount expression as sp_frmOrder. Targeted update only when changed.
        await NonQuery(c,tx,"""
            UPDATE u SET UzsakymoVerteSuNuolaida=calculated.Amount
            FROM dbo.Uzsakymai u CROSS APPLY(SELECT CAST(ROUND(
                (ISNULL(u.mOrderDiscountableBlindsCost,0)+ISNULL(u.mOrderAccessoriesCost,0))/100*(100-ISNULL(u.Nuolaida,0))
                +ISNULL(u.mOrderNONDiscountableBlindsCost,0),2) AS decimal(18,2)) Amount) calculated
            WHERE u.UzsakymasID=@id AND (u.UzsakymoVerteSuNuolaida IS NULL OR u.UzsakymoVerteSuNuolaida<>calculated.Amount);
            """,ct,("@id",orderId));
    }
}
