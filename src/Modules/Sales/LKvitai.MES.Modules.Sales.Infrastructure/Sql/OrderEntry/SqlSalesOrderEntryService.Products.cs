using System.Data;
using System.Globalization;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Application.Pricing;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.Quotes;
using Microsoft.Data.SqlClient;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public Task<OrderEntryCommandResult> CreateProductAsync(Guid actorId,long orderId,CreateProductCommand command,CancellationToken ct)
    {
        var warnings=new List<string>();
        return Execute(actorId,command.CommandId,"product.create.v1",orderId,command.ExpectedRevision,command,async(c,tx,before,token)=>
        {
            RequireEditable(before!);
            warnings.AddRange(await CheckEligibility(c,tx,before!.Header.CustomerId!.Value,token));
            await ActiveType(c,tx,command.TypeId,token);
            var id=Convert.ToInt64(await Scalar(c,tx,"""
                DECLARE @ids table(Id bigint NOT NULL);
                INSERT dbo.ParduotiGaminiai(UzsakymoID,ParduotoGaminioTipoID,Spalva)
                OUTPUT INSERTED.ParduotoGaminioID INTO @ids VALUES(@order,@type,0);
                SELECT Id FROM @ids;
                """,token,("@order",orderId),("@type",command.TypeId)));
            return(orderId,(long?)id);
        },ct,warnings);
    }

    public Task<OrderEntryCommandResult> ConfigureProductAsync(Guid actorId,long orderId,long productId,ConfigureProductCommand command,CancellationToken ct)
    {
        var warnings=new List<string>();
        return Execute(actorId,command.CommandId,"product.configure.v1",orderId,command.ExpectedRevision,new{productId,command},
            (c,tx,before,token)=>ConfigureProductCore(c,tx,before!,orderId,productId,command.Product,warnings,token),ct,warnings);
    }

    private async Task<(long OrderId,long? ProductId)> ConfigureProductCore(SqlConnection c,SqlTransaction tx,OrderEditorSnapshot before,
        long orderId,long productId,ProductConfigurationInput p,List<string> warnings,CancellationToken token)
    {
            var old=Product(before,productId);
            if(p is null)throw new OrderEntryException("PRODUCT_REQUIRED","Product configuration is required.",400);
            if(before!.Header.Cancelled)throw new OrderEntryException("ORDER_CANCELLED","Cancelled orders cannot be configured in Sales.",409);
            warnings.AddRange(await CheckEligibility(c,tx,before.Header.CustomerId!.Value,token));
            var type=await ActiveType(c,tx,p.TypeId,token,p.TypeId!=old.TypeId);
            ValidateConfiguration(p,I(type,"KainosMatas"));
            var calculationInputsChanged=p.TypeId!=old.TypeId||p.WidthMm!=old.WidthMm||p.HeightMm!=old.HeightMm
                ||p.Quantity!=old.Quantity||p.ColourId!=old.ColourId||p.Control!=old.Control
                ||p.UnitPrice!=old.UnitPrice||p.UseListPrice||(p.ManualParameterSlots?.Count??0)>0;
            if(calculationInputsChanged)
            {
                if(string.IsNullOrWhiteSpace(p.CalculationRevision))throw new OrderEntryException("CALCULATION_PREVIEW_REQUIRED","Review the current server calculation before saving.",409);
                var currentCalculation=await LegacyProductCalculationRevision.ComputeAsync(c,tx,p.TypeId,p.ColourId,orderId,token);
                if(!string.Equals(p.CalculationRevision,currentCalculation,StringComparison.Ordinal))
                    throw new OrderEntryException("CALCULATION_CHANGED","Calculation or price references changed. Review a fresh calculation before saving.",409);
            }
            if(p.TypeId!=old.TypeId && old.Accessories.Count!=0)
                throw new OrderEntryException("TYPE_ACCESSORIES","Remove attached accessories before changing the product type.");
            var formulas=await LegacyProductRuleSql.LoadTypeFormulasAsync(c,tx,p.TypeId,token);
            var constants=await LegacyProductRuleSql.LoadConstantsAsync(c,tx,token);
            var recalculate=p.WidthMm!=old.WidthMm||p.HeightMm!=old.HeightMm||p.Quantity!=old.Quantity||p.TypeId!=old.TypeId;
            string?[] parameters;
            try
            {
                var storedParameters=p.TypeId!=old.TypeId?new string?[12]:old.Parameters;
                parameters=LegacyProductCalculator.Calculate(p.WidthMm,p.HeightMm,p.Quantity,p.ColourId,p.Control,formulas,storedParameters,constants,recalculate);
                // Intent describes manual edits performed AFTER the latest dimension recalculation.
                // It is not stored as a sticky override; the UI clears it on the next dependency edit.
                if(p.ManualParameterSlots is { Count: > 0 } manual)
                {
                    if(manual.Count>12 || manual.Distinct().Count()!=manual.Count || manual.Any(i=>i<0||i>11))
                        throw new OrderEntryException("PPP_OVERRIDE_SHAPE","Manual PPP slot indices must be distinct values from zero to eleven.");
                    foreach(var index in manual)
                    {
                        if(type[$"P{index+1:D2}"] is null)throw new OrderEntryException("PPP_HIDDEN","A hidden technical parameter cannot be overridden in this editor.");
                        parameters[index]=p.Parameters[index];
                    }
                    parameters=LegacyProductCalculator.Calculate(p.WidthMm,p.HeightMm,p.Quantity,p.ColourId,p.Control,formulas,parameters,constants,false);
                }
                LegacyMaterialSelectionRules.Validate(p.ColourId,Ppp12(parameters[11]),p.UseListPrice);
                var stockChanged=recalculate||p.ColourId!=old.ColourId||Ppp12(parameters[11])!=Ppp12(old.Parameters[11]);
                if(p.ColourId!=0 && stockChanged)
                {
                    var stock=await LegacyProductRuleSql.GetStockAsync(c,tx,p.TypeId,p.ColourId,productId,token);
                    var required=parameters[11] is null?0:decimal.Parse(parameters[11]!,CultureInfo.InvariantCulture);
                    var warning=LegacyProductCalculator.CheckStock(stock.FreeQuantity,stock.OwnReservedQuantity,required,stock.ActionId,stock.Unit);
                    if(warning is not null)warnings.Add(warning);
                }
            }
            catch(LegacyProductRuleException ex){throw new OrderEntryException(ex.Code,ex.Field+": "+ex.Message);}
            decimal unitPrice=p.UnitPrice;bool trusted=old.TrustedPrice??false;
            if(p.UseListPrice)
            {
                var quote=await UnitPrice(c,tx,p,before.Header.Date??throw new OrderEntryException("DATE_REQUIRED","Order date is required."),token);
                if(quote.Status!=SalesUnitPriceStatus.Found)throw new OrderEntryException("PRICE_"+quote.Status.ToString().ToUpperInvariant(),quote.Status==SalesUnitPriceStatus.Missing ? $"Product not saved: no list price matches this configuration ({p.WidthMm} × {p.HeightMm} mm; order date {before.Header.Date:yyyy-MM-dd}). Check the material and dimensions, or ask the price-list administrator to add a price." : "Product not saved: the price list contains conflicting or incomplete prices for this configuration. Ask the price-list administrator to correct the price before saving.");
                if(quote.UnitPrice!=p.UnitPrice)throw new OrderEntryException("PRICE_CHANGED","List price changed. Look up the price again before saving.",409);
                unitPrice=quote.UnitPrice!.Value;trusted=true;
            }
            else if(!LegacyPriceEntryRules.SupportsManualUnitPriceForType(p.TypeId,S(type,"KainosIvestis")) && (old.TrustedPrice!=true||old.UnitPrice!=p.UnitPrice||old.TypeId!=p.TypeId))
                throw new OrderEntryException("PRICE_LIST_REQUIRED","Product not saved: this product requires a price from the price list. Click From price list; if no price is found, check the material, dimensions and order date.");
            else if(old.UnitPrice!=p.UnitPrice)trusted=false;
            var changes=new List<(string Column,object? Value)>();
            void Add(string field,object? value,object? previous){if(!Equals(value,previous))changes.Add((field,value));}
            Add("ParduotoGaminioTipoID",p.TypeId,old.TypeId);Add("Plotis",p.WidthMm,old.WidthMm);Add("Aukstis",p.HeightMm,old.HeightMm);
            Add("Kiekis",p.Quantity,old.Quantity);Add("Spalva",p.ColourId,old.ColourId);Add("Valdymas",p.Control,old.Control);
            Add("Kaina",unitPrice,old.UnitPrice);Add("Pastabos",p.Note,old.Note);Add("bIsTrustedPriceFlag",trusted,old.TrustedPrice);
            for(var i=0;i<11;i++)Add($"PPP{i+1:D2}",parameters[i],old.Parameters[i]);
            var ppp12=parameters[11] is null?(decimal?)null:decimal.Parse(parameters[11]!,CultureInfo.InvariantCulture);
            var oldPpp12=old.Parameters[11] is null?(decimal?)null:decimal.Parse(old.Parameters[11]!,CultureInfo.InvariantCulture);
            Add("PPP12",ppp12,oldPpp12);
            // Chargeable measures depend on dimensions/type. The legacy bound form
            // may include Kaina in its UPDATE; our narrow update must explicitly
            // dispatch that same total-calculation trigger without a new price lookup.
            if((p.WidthMm!=old.WidthMm||p.HeightMm!=old.HeightMm||p.TypeId!=old.TypeId)
                && !changes.Any(x=>x.Column=="Kaina"))changes.Add(("Kaina",unitPrice));
            if(changes.Count!=0)
            {
                var values=changes.Select((x,i)=>("@v"+i,x.Value)).Append(("@id",(object?)productId)).Append(("@order",(object?)orderId)).ToArray();
                await NonQuery(c,tx,"UPDATE dbo.ParduotiGaminiai SET "+string.Join(",",changes.Select((x,i)=>x.Column+"=@v"+i))+" WHERE ParduotoGaminioID=@id AND UzsakymoID=@order",token,values);
            }
            await SyncStoredTotal(c,tx,orderId,token);
            return(orderId,(long?)productId);
    }


    private async Task<Dictionary<string,object?>> ActiveType(SqlConnection c,SqlTransaction tx,int typeId,CancellationToken ct,bool requireActive=true)
    {
        var rows=await Rows(c,tx,"""
            SELECT t.TipoID,t.Rusis,t.KainosMatas,t.Gamininamas,r.KainosIvestis,r.P01,r.P02,r.P03,r.P04,r.P05,r.P06,r.P07,r.P08,r.P09,r.P10,r.P11,r.P12
            FROM dbo.Zinynas_tipai t JOIN dbo.Zinynas_rusys r ON r.RusiesID=t.Rusis
            WHERE t.TipoID=@type AND (@active=0 OR(t.Naudojamas<>0 AND r.Naudojamas<>0));
            """,ct,("@type",typeId),("@active",requireActive));
        var row=rows.SingleOrDefault()??throw new OrderEntryException("TYPE_UNAVAILABLE","Type or category is not active.");
        if(requireActive)
        {
            var formulas=await LegacyProductRuleSql.LoadTypeFormulasAsync(c,tx,typeId,ct);
            if(LegacyProductCalculator.FindUnsupportedReason(formulas) is { } reason)
                throw new OrderEntryException("UNSUPPORTED_FORMULA",reason);
        }
        return row;
    }
    private static void ValidateConfiguration(ProductConfigurationInput p,int? unit)
        =>LegacyProductConfigurationValidation.ValidateConfiguration(p,unit);
    private static decimal? Ppp12(string? value)=>value is null?null:decimal.Parse(value,CultureInfo.InvariantCulture);
    private static void Money(decimal price)
        =>LegacyProductConfigurationValidation.ValidateUnitPrice(price);

    private async Task<SalesUnitPriceResult> UnitPrice(SqlConnection c,SqlTransaction tx,ProductConfigurationInput p,DateOnly date,CancellationToken ct)
    {
        var rows=await Rows(c,tx,"""
            SELECT COUNT_BIG(*) MatchingRows,COUNT(DISTINCT l.mPrice) DistinctPrices,
                COALESCE(SUM(CASE WHEN l.mPrice IS NULL THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END),0) NullPriceRows,
                MIN(l.mPrice) MinimumPrice,MAX(l.mPrice) MaximumPrice
            FROM dbo.TCR_TypeColourPriceGroup g
            JOIN dbo.DIC_PriceList l ON g.sPriceGroupID=l.iPriceGroupID
            JOIN dbo.TCR_PriceGroupPeriods period ON l.iPeriodID=period.iPeriodID
            JOIN dbo.TBD_ComponentsPriceGroups groups ON g.iComponentsPriceGroupID=groups.iComponentsPriceGroupID
            JOIN dbo.TBD_ComponentsPriceGroupRows component ON groups.iComponentsPriceGroupID=component.iComponentsPriceGroupID
            WHERE l.iWidth>=@width AND l.iHeight>=@height AND g.iTypeID=@type
              AND period.dtValidFrom<=@date AND period.dtValidUntil>=@date AND component.iComponentID=@component;
            """,ct,("@width",p.WidthMm),("@height",p.HeightMm),("@type",p.TypeId),("@date",date.ToDateTime(TimeOnly.MinValue)),("@component",p.ColourId));
        var r=rows.Single();return SalesUnitPricePolicy.Resolve(true,true,L(r,"MatchingRows")!.Value,I(r,"DistinctPrices")!.Value,L(r,"NullPriceRows")!.Value,D(r,"MinimumPrice"),D(r,"MaximumPrice"));
    }
}
