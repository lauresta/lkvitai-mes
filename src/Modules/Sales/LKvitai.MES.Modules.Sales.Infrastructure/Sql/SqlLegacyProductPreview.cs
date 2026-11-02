using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using System.Globalization;
using System.Data;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
/// <summary>Short read-only calculation against server-owned type formulas and constants.</summary>
public sealed class SqlLegacyProductPreview(SalesSqlOptions options, ILegacySqlConnectionFactory connections) : ILegacyProductPreview
{
    public async Task<LegacyProductPreviewDto> CalculateAsync(LegacyProductPreviewRequest request,CancellationToken ct)
    {
        if(request.WidthMm<0||request.HeightMm<0||request.Quantity<=0)
            throw new LegacyProductRuleException("INVALID_DIMENSIONS","dimensions","Dimensions must be nonnegative and quantity positive.");
        if(request.Parameters is null||request.Parameters.Count!=12)
            throw new LegacyProductRuleException("PPP_SHAPE","parameters","Exactly twelve PPP slots are required.");
        if(request.ProductId.HasValue&&!request.OrderId.HasValue)
            throw new LegacyProductRuleException("PRODUCT_ORDER_REQUIRED","productId","The current product requires its order ID.");
        await using var lease=await connections.OpenAsync(ct).ConfigureAwait(false);
        var connection=lease.Connection;
        await using var transaction=await lease.BeginTransactionAsync(ct).ConfigureAwait(false);
        if(request.OrderId.HasValue)
        {
            // Denied and absent parents are indistinguishable. Never let a foreign
            // product influence another actor's reservation add-back.
            const string ownershipSql="""
                SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Uzsakymai u WITH(HOLDLOCK) JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija WHERE u.UzsakymasID=@OrderId AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=s.FilialoID))
                  AND (@ProductId IS NULL OR EXISTS(SELECT 1 FROM dbo.ParduotiGaminiai WITH(HOLDLOCK)
                      WHERE ParduotoGaminioID=@ProductId AND UzsakymoID=@OrderId))
                  THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;
                """;
            await using var ownership=new SqlCommand(ownershipSql,connection,transaction){CommandTimeout=Math.Min(options.CommandTimeoutSeconds>0?options.CommandTimeoutSeconds:20,20)};
            ownership.Parameters.Add("@OrderId",SqlDbType.BigInt).Value=request.OrderId.Value;
            ownership.Parameters.Add("@ProductId",SqlDbType.BigInt).Value=(object?)request.ProductId??DBNull.Value;
            var matches=(bool)(await ownership.ExecuteScalarAsync(ct).ConfigureAwait(false)??false);
            if(!matches)throw new OrderEntryException("NOT_FOUND","Order or product not found.",404);
        }
        var formulas=await LegacyProductRuleSql.LoadTypeFormulasAsync(connection,transaction,request.TypeId,ct).ConfigureAwait(false);
        var constants=await LegacyProductRuleSql.LoadConstantsAsync(connection,transaction,ct).ConfigureAwait(false);
        var parameters=LegacyProductCalculator.Calculate(request.WidthMm,request.HeightMm,request.Quantity,
            request.ColourId,request.Control,formulas,request.Parameters,constants,request.RecalculateParameters);
        var required=parameters[11] is null ? (decimal?)null : decimal.Parse(parameters[11]!,CultureInfo.InvariantCulture);
        LegacyProductStockContext? stock=null;
        LegacyStockPreviewDecision? decision=null;
        if(request.ColourId is { } componentId&&componentId!=0)
        {
            stock=await LegacyProductRuleSql.GetStockAsync(connection,transaction,request.TypeId,componentId,
                request.ProductId,ct,forUpdate:false).ConfigureAwait(false);
            decision=LegacyProductCalculator.PreviewStock(stock.FreeQuantity,stock.OwnReservedQuantity,required,stock.ActionId);
        }
        var calculationRevision=await LegacyProductCalculationRevision.ComputeAsync(connection,transaction,
            request.TypeId,request.ColourId??0,request.OrderId,ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return new LegacyProductPreviewDto(parameters,required,stock?.AvailableForLine,stock?.FreeQuantity,
            stock?.ActionId,decision?.Disposition,decision?.IsShort,stock?.Unit,calculationRevision);
    }
}
