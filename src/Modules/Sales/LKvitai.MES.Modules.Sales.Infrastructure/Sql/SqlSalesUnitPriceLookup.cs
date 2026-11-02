using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using System.Data;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Application.Pricing;
using LKvitai.MES.Modules.Sales.Contracts.Quotes;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// SELECT-only equivalent of the current Access FindPrice predicates. Aggregate
/// matching rows to expose unordered TOP(1) ambiguity rather than choose a new
/// implicit ordering. No legacy price function (which returns int) is invoked.
/// </summary>
public sealed class SqlSalesUnitPriceLookup : ISalesUnitPriceLookup
{
    private readonly SalesSqlOptions _options;
    private readonly ILegacySqlConnectionFactory _connections;

    public SqlSalesUnitPriceLookup(SalesSqlOptions options, ILegacySqlConnectionFactory connections)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("Live price lookup requires a SQL connection.", nameof(options));
        _options = options;
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    public async Task<SalesUnitPriceResult> GetUnitPriceAsync(
        SalesUnitPriceQuery query, CancellationToken cancellationToken)
    {
        SalesUnitPricePolicy.Validate(query);
        await using var lease = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var connection = lease.Connection;
        const string sql = """
            SELECT
                CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Zinynas_tipai WHERE TipoID = @TypeId)
                     THEN 1 ELSE 0 END AS bit) AS TypeExists,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.TBD_Components WHERE iComponentID = @ComponentId)
                     THEN 1 ELSE 0 END AS bit) AS ComponentExists,
                COUNT_BIG(*) AS MatchingRows,
                COUNT(DISTINCT l.mPrice) AS DistinctPrices,
                COALESCE(SUM(CASE WHEN l.mPrice IS NULL THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END), 0) AS NullPriceRows,
                MIN(l.mPrice) AS MinimumPrice,
                MAX(l.mPrice) AS MaximumPrice
            FROM dbo.TCR_TypeColourPriceGroup AS g
            INNER JOIN dbo.DIC_PriceList AS l ON g.sPriceGroupID = l.iPriceGroupID
            INNER JOIN dbo.TCR_PriceGroupPeriods AS p ON l.iPeriodID = p.iPeriodID
            INNER JOIN dbo.TBD_ComponentsPriceGroups AS cg
                ON g.iComponentsPriceGroupID = cg.iComponentsPriceGroupID
            INNER JOIN dbo.TBD_ComponentsPriceGroupRows AS cgr
                ON cg.iComponentsPriceGroupID = cgr.iComponentsPriceGroupID
            WHERE l.iWidth >= @Width AND l.iHeight >= @Height
              AND g.iTypeID = @TypeId
              AND p.dtValidFrom <= @OrderDate AND p.dtValidUntil >= @OrderDate
              AND cgr.iComponentID = @ComponentId;
            """;
        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = _options.CommandTimeoutSeconds > 0 ? _options.CommandTimeoutSeconds : 30,
        };
        command.Parameters.Add("@TypeId", SqlDbType.Int).Value = query.TypeId;
        command.Parameters.Add("@ComponentId", SqlDbType.Int).Value = query.ComponentId;
        command.Parameters.Add("@Width", SqlDbType.Int).Value = query.WidthMm;
        command.Parameters.Add("@Height", SqlDbType.Int).Value = query.HeightMm;
        command.Parameters.Add("@OrderDate", SqlDbType.DateTime).Value = query.OrderDate.ToDateTime(TimeOnly.MinValue);
        // Quantity is required by legacy FindPrice, but is not a unit-price predicate.
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new DataException("Price lookup aggregate is missing.");
        return SalesUnitPricePolicy.Resolve(
            reader.GetBoolean(reader.GetOrdinal("TypeExists")),
            reader.GetBoolean(reader.GetOrdinal("ComponentExists")),
            reader.GetInt64(reader.GetOrdinal("MatchingRows")),
            reader.GetInt32(reader.GetOrdinal("DistinctPrices")),
            reader.GetInt64(reader.GetOrdinal("NullPriceRows")),
            NullableMoney(reader, "MinimumPrice"), NullableMoney(reader, "MaximumPrice"));
    }

    private static decimal? NullableMoney(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }
}
