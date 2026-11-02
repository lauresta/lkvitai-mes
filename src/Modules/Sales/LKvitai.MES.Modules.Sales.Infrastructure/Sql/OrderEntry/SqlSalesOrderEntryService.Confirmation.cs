using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public Task<OrderEntryCommandResult> ConfirmProductsAsync(Guid actorId,long orderId,ConfirmOrderProductsCommand command,CancellationToken ct)
    {
        if(!options.ProductionConfirmationEnabled)throw new OrderEntryException("CONFIRMATION_DISABLED","Production confirmation is not enabled for this deployment.",403);
        return Execute(actorId,command.CommandId,"production.confirm.v1",orderId,command.ExpectedRevision,command,async(c,tx,before,token)=>
        {
            await RequireFinancialSchema(c,tx,token);
            var number=LegacyProductionDocumentNumber.Create(options.ProductionDocumentPrefix??"",Guid.NewGuid());
            if(before!.Header.Cancelled)throw new OrderEntryException("ORDER_CANCELLED","Cancelled orders cannot be confirmed.",409);
            if(command.ProductIds is null||command.ProductIds.Count is <1 or >500||command.ProductIds.Distinct().Count()!=command.ProductIds.Count)
                throw new OrderEntryException("CONFIRMATION_ITEMS","Select one to five hundred distinct products from this order.");
            var policy=await Rows(c,tx,"EXEC dbo.sproc_CheckIfNewOrdersAllowedByCustID_forVBA @id",token,("@id",before.Header.CustomerId));
            if(policy.Count!=1)throw new OrderEntryException("ELIGIBILITY_UNAVAILABLE","Customer confirmation policy could not be established.",503);
            var action=I(policy[0],"@action");
            if(action==1)throw new OrderEntryException("CONFIRMATION_CREDIT",S(policy[0],"@msg")??"Customer policy prevents production confirmation.");
            if(action is null or <0 or >3)throw new OrderEntryException("INVALID_ELIGIBILITY_ACTION","Customer confirmation action is not supported.");
            var barcodes=new List<string>();
            foreach(var id in command.ProductIds)
            {
                var product=before.Products.SingleOrDefault(p=>p.Id==id)??throw new OrderEntryException("NOT_FOUND","Selected product is outside this order.",404);
                if(product.TypeId is null)throw new OrderEntryException("INCOMPLETE_PRODUCT","Product type is required before confirmation.");
                var rows=await Rows(c,tx,"""
                    SELECT b.iBarCode,t.TipoID,t.Gamininamas,t.KainosMatas,r.KainosIvestis FROM dbo.ParduotiGaminiai b
                    JOIN dbo.Zinynas_tipai t ON t.TipoID=b.ParduotoGaminioTipoID
                    JOIN dbo.Zinynas_rusys r ON r.RusiesID=t.Rusis
                    WHERE b.ParduotoGaminioID=@id AND b.UzsakymoID=@order;
                    """,token,("@id",id),("@order",orderId));
                var row=rows.SingleOrDefault()??throw new OrderEntryException("TYPE_UNAVAILABLE","Stored product type or category is unavailable.");
                var stored=LegacyConfirmationEligibility.Validate(product,new(I(row,"TipoID")!.Value,I(row,"KainosMatas"),S(row,"KainosIvestis"),B(row,"Gamininamas")));
                ValidateConfiguration(stored,I(row,"KainosMatas"));
                var barcode=S(row,"iBarCode")??throw new OrderEntryException("BARCODE_REQUIRED","Product has no production barcode.");
                var stock=await Rows(c,tx,"SELECT sAllow FROM dbo.view_CanConfirmBlinds WITH(HOLDLOCK) WHERE iBarCode=@barcode",token,("@barcode",barcode));
                if(stock.Any(r=>S(r,"sAllow")=="N"))throw new OrderEntryException("CONFIRMATION_STOCK","The legacy confirmation stock rule blocks a selected product.");
                barcodes.Add(barcode);
            }
            // The number avoids COUNT+1 races and respects the downstream32-char
            // parameter. A namespace does not isolate shared production selectors;
            // activation is an explicit deployment/owner decision, independent of scope.
            var actualBytes=await Scalar(c,tx,"SELECT COL_LENGTH(N'dbo.TAB_ProductionOutputDocuments',N'sProductionOutputDocumentNo')",token);
            if(actualBytes is null || Convert.ToInt32(actualBytes)<number.Length*2)
                throw new OrderEntryException("DOCUMENT_NUMBER_SCHEMA","The production-document column no longer supports the verified number format.",503);
            var document=Convert.ToInt32(await Scalar(c,tx,"""
                DECLARE @ids table(Id int NOT NULL);
                INSERT dbo.TAB_ProductionOutputDocuments(sProductionOutputDocumentNo,iAssignedStatusID,dtDateTime)
                OUTPUT INSERTED.iProductionOutputDocumentID INTO @ids VALUES(@number,1173102375,GETDATE());
                SELECT Id FROM @ids;
                """,token,("@number",number)));
            // One row per statement preserves scalar legacy trigger contracts. The
            // Sales operation is atomic: a rejection rolls back all selected lines.
            foreach(var barcode in barcodes)
                await NonQuery(c,tx,"INSERT dbo.TAB_ProductionOutputDocumentDetails(iProductionOutputDocumentID,iBarCode) VALUES(@document,@barcode)",token,("@document",document),("@barcode",barcode));
            return(orderId,(long?)document);
        },ct);
    }
}
