using System.Data;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// <see cref="ISalesCustomerSearch"/> over the legacy database: one read-only statement
/// (<c>Queries/SalesCustomerSearch.sql</c>) on a connection leased for the caller's legacy login.
/// </summary>
public sealed class SqlSalesCustomerSearch(SalesSqlOptions options, ILegacySqlConnectionFactory connections) : ISalesCustomerSearch
{
    public const int MaxRows = 50;

    private static readonly string Sql = ReadQuery();

    public async Task<IReadOnlyList<string>> SearchAsync(string search, int limit, CancellationToken cancellationToken)
    {
        var text = search?.Trim();
        if (string.IsNullOrEmpty(text)) return [];

        await using var lease = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(Sql, lease.Connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = options.CommandTimeoutSeconds > 0 ? options.CommandTimeoutSeconds : 30,
        };
        command.Parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 200) { Value = text });
        command.Parameters.Add(new SqlParameter("@Top", SqlDbType.Int) { Value = Math.Clamp(limit, 1, MaxRows) });

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0)) names.Add(reader.GetString(0));
        }

        return names;
    }

    private static string ReadQuery()
    {
        using var stream = typeof(SqlSalesCustomerSearch).Assembly.GetManifestResourceStream(
            "LKvitai.MES.Modules.Sales.Infrastructure.Sql.Queries.SalesCustomerSearch.sql")
            ?? throw new InvalidOperationException("Missing Sales customer search query.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
