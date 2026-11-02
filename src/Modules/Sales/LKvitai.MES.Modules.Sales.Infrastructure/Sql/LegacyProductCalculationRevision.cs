using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>Optimistic fingerprint of server-owned calculation/pricing references, not a credential or save approval.</summary>
public static class LegacyProductCalculationRevision
{
    public static async Task<string> ComputeAsync(SqlConnection connection,SqlTransaction transaction,int typeId,int componentId,long? orderId,CancellationToken ct)
    {
        const string sql="""
            SELECT t.TipoID,t.Rusis,t.KainosMatas,t.MinKainosPlotis,t.MinKainosAukstis,t.MinKainosPlotas,
                   t.Naudojamas,t.Gamininamas,r.Naudojamas CategoryActive,r.KainosIvestis,r.TeikiamaNuolaida,
                   t.NPP01,t.NPP02,t.NPP03,t.NPP04,t.NPP05,t.NPP06,t.NPP07,t.NPP08,t.NPP09,t.NPP10,t.NPP11,t.NPP12,
                   r.P01,r.P02,r.P03,r.P04,r.P05,r.P06,r.P07,r.P08,r.P09,r.P10,r.P11,r.P12
            FROM dbo.Zinynas_tipai t WITH(HOLDLOCK)
            LEFT JOIN dbo.Zinynas_rusys r WITH(HOLDLOCK) ON r.RusiesID=t.Rusis WHERE t.TipoID=@TypeId;
            SELECT sConstantName,dConstantValue FROM dbo.DIC_Constants WITH(HOLDLOCK)
            WHERE sConstantName IN('MaxStep127','MaxStep89','EdgeFix127_AB','EdgeFix89_AB','EdgeFix127_CDE','EdgeFix89_CDE')
            ORDER BY sConstantName,iConstantID;
            SELECT u.UzsakovoID,u.Nuolaida,u.ValiutosID,c.bIsWholesalerFlag
            FROM dbo.Uzsakymai u WITH(HOLDLOCK)
            LEFT JOIN dbo.Zinynas_uzsakovai c WITH(HOLDLOCK) ON c.UzsakovoID=u.UzsakovoID WHERE u.UzsakymasID=@OrderId;
            SELECT d.iCustomerID,d.iTypeID,d.iDiscount FROM dbo.TCR_CustomersTypeDiscounts d WITH(HOLDLOCK)
            WHERE d.iCustomerID=2089646703 AND d.iTypeID=@TypeId
            ORDER BY d.iCustomerID,d.iTypeID,d.iDiscount;
            SELECT d.iCustomerID,d.iAccessoryCategoryID,d.iDiscount
            FROM dbo.TCR_CustomerAccessoryTypeDiscounts d WITH(HOLDLOCK)
            WHERE d.iCustomerID=2089646703 AND EXISTS(SELECT 1 FROM dbo.Zinynas_tipai t WITH(HOLDLOCK) WHERE t.TipoID=@TypeId AND t.Rusis=d.iAccessoryCategoryID)
            ORDER BY d.iCustomerID,d.iAccessoryCategoryID,d.iDiscount;
            SELECT c.iComponentID,c.iComponentCategoryID,c.iUnitID,c.iActionIDWhenNotInStock,c.bDenyOrderConfirmationWhenNotInStock
            FROM dbo.TBD_Components c WITH(HOLDLOCK) WHERE c.iComponentID=@ComponentId;
            SELECT m.iCategoryID,m.iComponentCategoryID
            FROM dbo.TCR_CategoriesColours m WITH(HOLDLOCK)
            WHERE EXISTS(SELECT 1 FROM dbo.Zinynas_tipai t WITH(HOLDLOCK) WHERE t.TipoID=@TypeId AND t.Rusis=m.iCategoryID)
            ORDER BY m.iCategoryID,m.iComponentCategoryID;
            SELECT g.sPriceGroupID,g.iTypeID,g.iComponentsPriceGroupID
            FROM dbo.TCR_TypeColourPriceGroup g WITH(HOLDLOCK)
            JOIN dbo.TBD_ComponentsPriceGroupRows components WITH(HOLDLOCK) ON components.iComponentsPriceGroupID=g.iComponentsPriceGroupID
            WHERE g.iTypeID=@TypeId AND components.iComponentID=@ComponentId
            ORDER BY g.sPriceGroupID,g.iTypeID,g.iComponentsPriceGroupID;
            SELECT l.iPriceID,l.iPriceGroupID,l.iWidth,l.iHeight,l.mPrice,l.iPeriodID,
                   p.dtValidFrom,p.dtValidUntil,p.iPriceGroupID PeriodPriceGroupId
            FROM dbo.DIC_PriceList l WITH(HOLDLOCK)
            JOIN dbo.TCR_PriceGroupPeriods p WITH(HOLDLOCK) ON p.iPeriodID=l.iPeriodID
            WHERE EXISTS(SELECT 1 FROM dbo.TCR_TypeColourPriceGroup g WITH(HOLDLOCK)
                         JOIN dbo.TBD_ComponentsPriceGroupRows components WITH(HOLDLOCK) ON components.iComponentsPriceGroupID=g.iComponentsPriceGroupID
                         WHERE g.iTypeID=@TypeId AND g.sPriceGroupID=l.iPriceGroupID AND components.iComponentID=@ComponentId)
            ORDER BY l.iPriceID,p.iPeriodID;
            """;
        await using var command=new SqlCommand(sql,connection,transaction){CommandTimeout=20};
        command.Parameters.Add("@TypeId",SqlDbType.Int).Value=typeId;
        command.Parameters.Add("@ComponentId",SqlDbType.Int).Value=componentId;
        command.Parameters.Add("@OrderId",SqlDbType.BigInt).Value=(object?)orderId??DBNull.Value;
        await using var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var tables=new List<IReadOnlyList<IReadOnlyList<object?>>>();
        do
        {
            var rows=new List<IReadOnlyList<object?>>();
            while(await reader.ReadAsync(ct).ConfigureAwait(false))
                rows.Add(Enumerable.Range(0,reader.FieldCount).Select(i=>reader.IsDBNull(i)?null:reader.GetValue(i)).ToArray());
            tables.Add(rows);
        } while(await reader.NextResultAsync(ct).ConfigureAwait(false));
        if(tables.Count==0||tables[0].Count!=1)
            throw new LegacyProductRuleException("TYPE_UNAVAILABLE","typeId","The selected type could not be loaded for calculation review.");
        return Compute(typeId,componentId,orderId,tables);
    }

    /// <summary>Deterministic typed binary encoding with explicit row/table boundaries, independent of JSON or locale.</summary>
    public static string Compute(int typeId,int componentId,long? orderId,IReadOnlyList<IReadOnlyList<IReadOnlyList<object?>>> tables)
    {
        using var stream=new MemoryStream();
        using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write("sales-calculation-references-v1");writer.Write(typeId);writer.Write(componentId);
            writer.Write(orderId.HasValue);if(orderId.HasValue)writer.Write(orderId.Value);
            writer.Write(tables.Count);
            foreach(var table in tables)
            {
                writer.Write(table.Count);
                foreach(var row in table)
                {
                    writer.Write(row.Count);
                    foreach(var value in row)Write(writer,value);
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void Write(BinaryWriter writer,object? value)
    {
        switch(value)
        {
            case null or DBNull:writer.Write((byte)0);break;
            case int i:writer.Write((byte)1);writer.Write(i);break;
            case long l:writer.Write((byte)2);writer.Write(l);break;
            case decimal d:writer.Write((byte)3);foreach(var bit in decimal.GetBits(d))writer.Write(bit);break;
            case bool b:writer.Write((byte)4);writer.Write(b);break;
            case string s:writer.Write((byte)5);writer.Write(s);break;
            case DateTime t:writer.Write((byte)6);writer.Write(t.Ticks);break;
            case short s:writer.Write((byte)7);writer.Write(s);break;
            case byte b:writer.Write((byte)8);writer.Write(b);break;
            default:throw new DataException($"Unsupported calculation-reference value type: {value.GetType().Name}.");
        }
    }
}
