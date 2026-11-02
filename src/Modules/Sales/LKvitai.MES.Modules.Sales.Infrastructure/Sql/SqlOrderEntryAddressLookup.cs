using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using System.Data;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed class SqlOrderEntryAddressLookup(SalesSqlOptions options, ILegacySqlConnectionFactory connections) : IOrderEntryAddressLookup
{
    public async Task<IReadOnlyList<OrderAddressReferenceDto>> GetAsync(int? parentId, string? search, int? id, CancellationToken ct)
    {
        var term=search?.Trim();
        if(term?.Length>100)throw new ArgumentException("Address search is limited to100 characters.",nameof(search));
        var pattern=string.IsNullOrEmpty(term)?null:"%"+term.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_").Replace("[","\\[")+"%";
        await using var lease=await connections.OpenAsync(ct);var connection=lease.Connection;
        const string sql="""
            SELECT TOP(100) a.iID,a.sName,a.iParentID,a.sPath,
                CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.DIC_Addresses c WHERE c.iParentID=a.iID) THEN 1 ELSE 0 END AS bit) HasChildren
            FROM dbo.DIC_Addresses a
            WHERE (@Id IS NULL OR a.iID=@Id)
              AND (@ParentId IS NULL OR a.iParentID=@ParentId)
              AND (@Search IS NULL OR a.sName LIKE @Search ESCAPE N'\' OR a.sPath LIKE @Search ESCAPE N'\')
              AND (@Id IS NOT NULL OR @ParentId IS NOT NULL OR @Search IS NOT NULL
                   OR a.iParentID IS NULL OR a.iParentID=0
                   OR NOT EXISTS(SELECT 1 FROM dbo.DIC_Addresses p WHERE p.iID=a.iParentID))
            ORDER BY a.sName,a.iID;
            """;
        await using var command=new SqlCommand(sql,connection){CommandTimeout=options.CommandTimeoutSeconds};
        command.Parameters.Add("@Id",SqlDbType.Int).Value=(object?)id??DBNull.Value;
        command.Parameters.Add("@ParentId",SqlDbType.Int).Value=(object?)parentId??DBNull.Value;
        command.Parameters.Add("@Search",SqlDbType.NVarChar,202).Value=(object?)pattern??DBNull.Value;
        await using var reader=await command.ExecuteReaderAsync(ct);var result=new List<OrderAddressReferenceDto>();
        while(await reader.ReadAsync(ct))result.Add(new(reader.GetInt32(0),reader.IsDBNull(1)?"":reader.GetString(1),reader.IsDBNull(2)?null:reader.GetInt32(2),reader.IsDBNull(3)?null:reader.GetString(3),reader.GetBoolean(4)));
        return result;
    }
}
