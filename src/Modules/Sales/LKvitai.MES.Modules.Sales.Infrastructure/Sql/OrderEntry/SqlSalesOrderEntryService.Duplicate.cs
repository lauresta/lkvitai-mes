using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public Task<OrderEntryCommandResult> DuplicateProductAsync(Guid actorId,long orderId,long productId,DuplicateProductCommand command,CancellationToken ct)
    {
        var warnings=new List<string>();
        return Execute(actorId,command.CommandId,"product.duplicate.v1",orderId,command.ExpectedRevision,new{productId,command},async(c,tx,before,token)=>
        {
            RequireEditable(before!);
            var source=Product(before!,productId);
            if(source.TypeId is null||source.WidthMm is null||source.HeightMm is null||source.Quantity is null||source.Quantity<=0||source.UnitPrice is null)
                throw new OrderEntryException("INCOMPLETE_PRODUCT","Complete the source product before duplicating it.");
            // Validate before INSERT; no unsafe return procedure or copied state/audit.
            await CheckEligibility(c,tx,before!.Header.CustomerId!.Value,token);
            var type=await ActiveType(c,tx,source.TypeId.Value,token);
            var newId=Convert.ToInt64(await Scalar(c,tx,"""
                DECLARE @ids table(Id bigint NOT NULL);
                INSERT dbo.ParduotiGaminiai(UzsakymoID,ParduotoGaminioTipoID,Spalva)
                OUTPUT INSERTED.ParduotoGaminioID INTO @ids VALUES(@order,@type,0);
                SELECT Id FROM @ids;
                """,token,("@order",orderId),("@type",source.TypeId.Value)));
            var afterInsert=await ReadAsync(c,tx,orderId,token)??throw new InvalidOperationException("Duplicated order not readable.");
            // Duplication intent preserves the configured visible values, including
            // manual PPP edits. Hidden technical slots are recalculated by server.
            var manual=Enumerable.Range(0,12).Where(i=>type[$"P{i+1:D2}"] is not null&&source.Parameters[i] is not null).ToArray();
            var calculation=await LegacyProductCalculationRevision.ComputeAsync(c,tx,source.TypeId.Value,source.ColourId,orderId,token);
            var useList=source.TrustedPrice==true||!LegacyPriceEntryRules.SupportsManualUnitPriceForType(source.TypeId.Value,S(type,"KainosIvestis"));
            var input=new ProductConfigurationInput(source.TypeId.Value,source.WidthMm.Value,source.HeightMm.Value,source.Quantity.Value,source.ColourId,source.Control,source.UnitPrice.Value,useList,source.Note,source.Parameters,true,manual,calculation);
            // Same transaction: authoritative calculation, price and material checks
            // include the original product's reservation and the new line's demand.
            await ConfigureProductCore(c,tx,afterInsert,orderId,newId,input,warnings,token);
            foreach(var accessory in source.Accessories)
            {
                if(accessory.Quantity is null or <=0||accessory.UnitPrice is null)
                    throw new OrderEntryException("INCOMPLETE_ACCESSORY","Complete attached accessories before duplicating this product.");
                Money(accessory.UnitPrice.Value);
                var applicable=await Scalar(c,tx,"""
                    SELECT TOP(1) 1 FROM dbo.Zinynas_tipai t
                    JOIN dbo.TCR_BlindCategoryAccessoryCategory mapping ON mapping.iBlindCategoryID=t.Rusis
                    JOIN dbo.Zinynas_priedai a ON a.iAccessoryCategoryID=mapping.iAccessoryCategoryID
                    WHERE t.TipoID=@type AND t.Naudojamas<>0 AND a.PriedoID=@accessory;
                    """,token,("@type",source.TypeId.Value),("@accessory",accessory.AccessoryId));
                if(applicable is null)throw new OrderEntryException("ACCESSORY_UNAVAILABLE","A source accessory is no longer applicable; duplication was not saved.");
                await NonQuery(c,tx,"INSERT dbo.ParduotiPriedai(PrduotoPriedoID,ParduotoGaminioID,ParduotoPriedoKiekis,ParduotoPriedoKaina) VALUES(@accessory,@product,@quantity,@price)",token,("@accessory",accessory.AccessoryId),("@product",newId),("@quantity",accessory.Quantity.Value),("@price",accessory.UnitPrice.Value));
                await SyncAccessoryTotals(c,tx,newId,token);
            }
            await SyncStoredTotal(c,tx,orderId,token);
            return(orderId,(long?)newId);
        },ct,warnings);
    }
}
