using LKvitai.MES.Modules.Warehouse.Application.Ports;
using Marten;

namespace LKvitai.MES.Infrastructure.Persistence;

public class MartenProjectionVerificationDataAccess : IProjectionVerificationDataAccess
{
    private readonly IDocumentStore _store;

    public MartenProjectionVerificationDataAccess(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<string?> ResolveQualifiedTableNameAsync(string tableName, CancellationToken cancellationToken)
    {
        await using var session = _store.QuerySession();
        var connection = session.Connection
            ?? throw new InvalidOperationException("Marten query session connection is unavailable.");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT n.nspname || '.' || c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND c.relname = @tableName
            ORDER BY CASE WHEN n.nspname = 'public' THEN 0 ELSE 1 END, n.nspname
            LIMIT 1";

        var tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "tableName";
        tableParam.Value = tableName;
        cmd.Parameters.Add(tableParam);

        return await cmd.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task<int> CountRowsAsync(string tableName, CancellationToken cancellationToken)
    {
        await using var session = _store.QuerySession();
        var connection = session.Connection
            ?? throw new InvalidOperationException("Marten query session connection is unavailable.");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(1) FROM {tableName}";
        var count = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count ?? 0);
    }

    public async Task<string> ComputeChecksumAsync(
        string tableName,
        string fieldExpression,
        string sortExpression,
        CancellationToken cancellationToken)
    {
        await using var session = _store.QuerySession();
        var connection = session.Connection
            ?? throw new InvalidOperationException("Marten query session connection is unavailable.");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COALESCE(
                MD5(STRING_AGG({fieldExpression}, '|' ORDER BY {sortExpression})),
                'empty'
            )
            FROM {tableName}";

        return (string?)await cmd.ExecuteScalarAsync(cancellationToken) ?? "empty";
    }
}
