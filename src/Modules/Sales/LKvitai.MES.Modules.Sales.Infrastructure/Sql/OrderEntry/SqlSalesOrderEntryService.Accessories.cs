using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public Task<OrderEntryCommandResult> SaveAccessoryAsync(Guid actorId,long orderId,long productId,SaveAccessoryCommand command,CancellationToken ct)
    {
        var warnings=new List<string>();
        return Execute(actorId,command.CommandId,"accessory.save.v1",orderId,command.ExpectedRevision,new{productId,command},async(c,tx,before,token)=>
        {
            var parent=Product(before!,productId);
            warnings.AddRange(await CheckEligibility(c,tx,before!.Header.CustomerId!.Value,token));
            if(command.Quantity<=0||decimal.Round(command.Quantity,3)!=command.Quantity||command.Quantity>999999999999999.999m)
                throw new OrderEntryException("ACCESSORY_QUANTITY","Enter a positive accessory quantity with up to three decimal places.");
            Money(command.UnitPrice);
            var applicable=await Scalar(c,tx,"""
                SELECT TOP(1) 1 FROM dbo.Zinynas_tipai t
                JOIN dbo.TCR_BlindCategoryAccessoryCategory mapping ON mapping.iBlindCategoryID=t.Rusis
                JOIN dbo.Zinynas_priedai a ON a.iAccessoryCategoryID=mapping.iAccessoryCategoryID
                WHERE t.TipoID=@type AND t.Naudojamas<>0 AND a.PriedoID=@accessory;
                """,token,("@type",parent.TypeId),("@accessory",command.AccessoryId));
            if(applicable is null)throw new OrderEntryException("ACCESSORY_UNAVAILABLE","Accessory is not applicable to this product type.");
            var existing=parent.Accessories.SingleOrDefault(a=>a.AccessoryId==command.AccessoryId);
            if(existing is null)
                await NonQuery(c,tx,"INSERT dbo.ParduotiPriedai(PrduotoPriedoID,ParduotoGaminioID,ParduotoPriedoKiekis,ParduotoPriedoKaina) VALUES(@accessory,@product,@quantity,@price)",token,("@accessory",command.AccessoryId),("@product",productId),("@quantity",command.Quantity),("@price",command.UnitPrice));
            else if(existing.Quantity!=command.Quantity||existing.UnitPrice!=command.UnitPrice)
                await NonQuery(c,tx,"UPDATE dbo.ParduotiPriedai SET ParduotoPriedoKiekis=@quantity,ParduotoPriedoKaina=@price WHERE PrduotoPriedoID=@accessory AND ParduotoGaminioID=@product",token,("@accessory",command.AccessoryId),("@product",productId),("@quantity",command.Quantity),("@price",command.UnitPrice));
            await SyncAccessoryTotals(c,tx,productId,token);
            await SyncStoredTotal(c,tx,orderId,token);
            return(orderId,(long?)productId);
        },ct,warnings);
    }
    public Task<OrderEntryCommandResult> DeleteAccessoryAsync(Guid actorId,long orderId,long productId,int accessoryId,DeleteAccessoryCommand command,CancellationToken ct)
        => Execute(actorId,command.CommandId,"accessory.delete.v1",orderId,command.ExpectedRevision,new{productId,accessoryId,command},async(c,tx,before,token)=>
        {
            var parent=Product(before!,productId);
            if(!parent.Accessories.Any(a=>a.AccessoryId==accessoryId))throw new OrderEntryException("NOT_FOUND","Accessory is not attached to this product.",404);
            await NonQuery(c,tx,"DELETE dbo.ParduotiPriedai WHERE PrduotoPriedoID=@accessory AND ParduotoGaminioID=@product",token,("@accessory",accessoryId),("@product",productId));
            await SyncAccessoryTotals(c,tx,productId,token);
            await SyncStoredTotal(c,tx,orderId,token);return(orderId,(long?)productId);
        },ct);
    public Task<OrderEntryCommandResult> DeleteProductAsync(Guid actorId,long orderId,long productId,DeleteProductCommand command,CancellationToken ct)
        => Execute(actorId,command.CommandId,"product.delete.v1",orderId,command.ExpectedRevision,new{productId,command},async(c,tx,before,token)=>
        {
            var parent=Product(before!,productId);
            if(!parent.CanDelete)throw new OrderEntryException("PRODUCT_LOCKED","This product cannot be removed.",409);
            // This deliberately narrower safe path never disables a global trigger.
            if(await Scalar(c,tx,"SELECT TOP(1) 1 FROM dbo.TAB_ProductionOutputDocumentDetails d JOIN dbo.ParduotiGaminiai b ON b.iBarCode=d.iBarCode WHERE b.ParduotoGaminioID=@product",token,("@product",productId)) is not null)
                throw new OrderEntryException("PRODUCTION_HISTORY","A product with production history cannot be removed through Sales.",409);
            foreach(var accessory in parent.Accessories)
                await NonQuery(c,tx,"DELETE dbo.ParduotiPriedai WHERE ParduotoGaminioID=@product AND PrduotoPriedoID=@accessory",token,("@product",productId),("@accessory",accessory.AccessoryId));
            await NonQuery(c,tx,"DELETE dbo.ParduotiGaminiai WHERE ParduotoGaminioID=@product AND UzsakymoID=@order",token,("@product",productId),("@order",orderId));
            // The historical AFTER DELETE aggregate INNER JOIN misses the empty collection.
            await NonQuery(c,tx,"UPDATE dbo.Uzsakymai SET mOrderDiscountableBlindsCost=0,mOrderNONDiscountableBlindsCost=0,mOrderAccessoriesCost=0 WHERE UzsakymasID=@order AND NOT EXISTS(SELECT 1 FROM dbo.ParduotiGaminiai WHERE UzsakymoID=@order)",token,("@order",orderId));
            await SyncStoredTotal(c,tx,orderId,token);return(orderId,(long?)null);
        },ct);
    private async Task SyncAccessoryTotals(Microsoft.Data.SqlClient.SqlConnection c,Microsoft.Data.SqlClient.SqlTransaction tx,long productId,CancellationToken ct)
    {
        // Two enabled DELETE triggers disagree: one recomputes SUM, one subtracts
        // deleted rows. Normalize the derived totals once AFTER both have run,
        // using the exact current INSERT/UPDATE aggregate/discount contract.
        await NonQuery(c,tx,"""
            UPDATE b SET mBlindAccessoryCost=amount.Amount,
                mBlindAccessoryCustomerDiscount=discount.Factor,
                mBlindAccessoryFinalCost=final.Amount
            FROM dbo.ParduotiGaminiai b
            CROSS APPLY(SELECT CAST(ISNULL(SUM(ISNULL(a.ParduotoPriedoKiekis*a.ParduotoPriedoKaina,0)),0) AS money) Amount
                        FROM dbo.ParduotiPriedai a WHERE a.ParduotoGaminioID=b.ParduotoGaminioID) amount
            CROSS APPLY(SELECT CAST(dbo.fn_DiscountByCustomerAndAccessoryCategory(b.ParduotoGaminioID) AS decimal(18,3)) Factor) discount
            CROSS APPLY(SELECT CAST(ISNULL(amount.Amount*discount.Factor,0) AS money) Amount) final
            WHERE b.ParduotoGaminioID=@id AND
                (b.mBlindAccessoryCost IS NULL OR b.mBlindAccessoryCost<>amount.Amount
                 OR b.mBlindAccessoryFinalCost IS NULL OR b.mBlindAccessoryFinalCost<>final.Amount
                 OR b.mBlindAccessoryCustomerDiscount<>discount.Factor
                 OR (b.mBlindAccessoryCustomerDiscount IS NULL AND discount.Factor IS NOT NULL)
                 OR (b.mBlindAccessoryCustomerDiscount IS NOT NULL AND discount.Factor IS NULL));
            """,ct,("@id",productId));
    }

}
