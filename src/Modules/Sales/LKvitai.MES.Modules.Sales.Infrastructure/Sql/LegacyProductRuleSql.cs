using System.Data;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

public sealed record LegacyProductStockContext(int ComponentId,decimal FreeQuantity,int? ActionId,decimal OwnReservedQuantity,
    string? Unit=null)
{
    public decimal AvailableForLine => FreeQuantity+OwnReservedQuantity;
}

/// <summary>Reads on the writer's connection/transaction. Never opens another transaction or mutates business rows.</summary>
public static class LegacyProductRuleSql
{
    public static async Task<IReadOnlyDictionary<string,double>> LoadConstantsAsync(SqlConnection connection,SqlTransaction transaction,CancellationToken ct)
    {
        const string sql="""
            SELECT sConstantName,dConstantValue FROM dbo.DIC_Constants WITH(HOLDLOCK)
            WHERE sConstantName IN ('MaxStep127','MaxStep89','EdgeFix127_AB','EdgeFix89_AB','EdgeFix127_CDE','EdgeFix89_CDE');
            """;
        await using var command=Command(sql,connection,transaction);
        await using var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var values=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        while(await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var name=reader.GetString(0);
            if(reader.IsDBNull(1)||!values.TryAdd(name,Convert.ToDouble(reader.GetDecimal(1))))
                throw new LegacyProductRuleException("INVALID_CALCULATION_CONSTANT","parameters","A required calculation constant is null or duplicated.");
        }
        return values;
    }
    public static async Task<IReadOnlyList<string?>> LoadTypeFormulasAsync(SqlConnection connection,SqlTransaction transaction,int typeId,CancellationToken ct)
    {
        const string sql="""
            SELECT NPP01,NPP02,NPP03,NPP04,NPP05,NPP06,NPP07,NPP08,NPP09,NPP10,NPP11,NPP12
            FROM dbo.Zinynas_tipai WITH(HOLDLOCK) WHERE TipoID=@TypeId;
            """;
        await using var command=Command(sql,connection,transaction);command.Parameters.Add("@TypeId",SqlDbType.Int).Value=typeId;
        await using var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if(!await reader.ReadAsync(ct).ConfigureAwait(false))throw new LegacyProductRuleException("TYPE_UNAVAILABLE","typeId","The selected product type does not exist.");
        return Enumerable.Range(0,12).Select(i=>reader.IsDBNull(i)?null:reader.GetString(i)).ToArray();
    }
    public static async Task<LegacyProductStockContext> GetStockAsync(SqlConnection connection,SqlTransaction transaction,int typeId,int componentId,long? currentProductId,CancellationToken ct,bool forUpdate=true)
    {
        // Component UPDLOCK serializes Sales operations for the same material. All
        // other participating range reads HOLDLOCK through caller commit so Access
        // cannot slip a reservation/stock change between comparison and mutation.
        var sql="""
            SELECT c.iActionIDWhenNotInStock,m.sName MaterialUnit
            FROM dbo.TBD_Components c COMPONENT_LOCK
            JOIN dbo.TBD_ComponentCategories cc WITH(HOLDLOCK)
              ON cc.iComponentCategoryID=c.iComponentCategoryID
            LEFT JOIN dbo.DIC_Units m WITH(HOLDLOCK) ON m.iUnitID=c.iUnitID
            WHERE c.iComponentID=@ComponentId AND c.iComponentID<>0
              AND EXISTS (SELECT 1 FROM dbo.TCR_CategoriesColours m WITH(HOLDLOCK)
                          JOIN dbo.Zinynas_tipai t WITH(HOLDLOCK) ON t.Rusis=m.iCategoryID
                          WHERE t.TipoID=@TypeId AND m.iComponentCategoryID=c.iComponentCategoryID);
            SELECT COALESCE(SUM(r.dQuantity),0) AS StockQuantity
            FROM dbo.TBD_BlindComponentsRemains r WITH(HOLDLOCK)
            WHERE r.iComponentID=@ComponentId AND r.iStorehouseID IN(1,2);
            SELECT COALESCE(SUM(b.PPP12),0) AS ReservedQuantity,
                   COALESCE(SUM(CASE WHEN b.ParduotoGaminioID=@CurrentProductId THEN b.PPP12 ELSE 0 END),0) AS OwnReservation
            FROM dbo.ParduotiGaminiai b WITH(HOLDLOCK)
            JOIN dbo.Uzsakymai o WITH(HOLDLOCK) ON o.UzsakymasID=b.UzsakymoID
            WHERE b.Spalva=@ComponentId AND b.BusenosID IN(4,1173102375) AND o.Sugadintas=0;
            """.Replace("COMPONENT_LOCK",forUpdate?"WITH(UPDLOCK,HOLDLOCK)":"WITH(HOLDLOCK)",StringComparison.Ordinal);
        await using var command=Command(sql,connection,transaction);
        command.Parameters.Add("@TypeId",SqlDbType.Int).Value=typeId;
        command.Parameters.Add("@ComponentId",SqlDbType.Int).Value=componentId;
        command.Parameters.Add("@CurrentProductId",SqlDbType.BigInt).Value=(object?)currentProductId??DBNull.Value;
        await using var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if(!await reader.ReadAsync(ct).ConfigureAwait(false))throw new LegacyProductRuleException("COLOUR_UNAVAILABLE","colourId","The selected material is not applicable to this product type.");
        int? action=reader.IsDBNull(0)?null:reader.GetInt32(0);
        var unit=reader.IsDBNull(1)?null:reader.GetString(1);
        if(!await reader.NextResultAsync(ct).ConfigureAwait(false)||!await reader.ReadAsync(ct).ConfigureAwait(false))throw new DataException("Material stock result is missing.");
        var stock=reader.GetDecimal(0);
        if(!await reader.NextResultAsync(ct).ConfigureAwait(false)||!await reader.ReadAsync(ct).ConfigureAwait(false))throw new DataException("Material reservation result is missing.");
        return new LegacyProductStockContext(componentId,stock-reader.GetDecimal(0),action,reader.GetDecimal(1),unit);
    }
    private static SqlCommand Command(string sql,SqlConnection connection,SqlTransaction transaction)=>new(sql,connection,transaction){CommandTimeout=20};
}
