using System.Data.Common;
using LKvitai.MES.SharedKernel;
using Marten;
using MediatR;

namespace LKvitai.MES.Application.Queries;

public record VerifyProjectionQuery : ICommand<VerifyProjectionResultDto>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public string ProjectionName { get; init; } = string.Empty;
}

public record VerifyProjectionResultDto
{
    public bool ChecksumMatch { get; init; }
    public string ProductionChecksum { get; init; } = string.Empty;
    public string ShadowChecksum { get; init; } = string.Empty;
    public int ProductionRowCount { get; init; }
    public int ShadowRowCount { get; init; }
}

public class VerifyProjectionQueryHandler
    : IRequestHandler<VerifyProjectionQuery, Result<VerifyProjectionResultDto>>
{
    private const string LocationBalanceTable = "mt_doc_locationbalanceview";
    private const string AvailableStockTable = "mt_doc_availablestockview";

    private readonly IDocumentStore _store;

    public VerifyProjectionQueryHandler(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<Result<VerifyProjectionResultDto>> Handle(
        VerifyProjectionQuery request,
        CancellationToken cancellationToken)
    {
        var projectionName = request.ProjectionName?.Trim();
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            return Result<VerifyProjectionResultDto>.Fail(
                DomainErrorCodes.ValidationError,
                "ProjectionName is required.");
        }

        var tableName = projectionName switch
        {
            "LocationBalance" => LocationBalanceTable,
            "AvailableStock" => AvailableStockTable,
            _ => null
        };

        if (tableName is null)
        {
            return Result<VerifyProjectionResultDto>.Fail(
                DomainErrorCodes.InvalidProjectionName,
                $"Projection '{projectionName}' is not supported.");
        }

        await using var session = _store.QuerySession();
        var connection = session.Connection
            ?? throw new InvalidOperationException("Marten query session connection is unavailable.");

        var productionTable = await ResolveQualifiedTableNameAsync(connection, tableName, cancellationToken);
        if (string.IsNullOrWhiteSpace(productionTable))
        {
            return Result<VerifyProjectionResultDto>.Fail(
                DomainErrorCodes.NotFound,
                $"Projection table for '{projectionName}' was not found.");
        }

        var shadowTable = await ResolveQualifiedTableNameAsync(connection, $"{tableName}_shadow", cancellationToken);
        if (string.IsNullOrWhiteSpace(shadowTable))
        {
            return Result<VerifyProjectionResultDto>.Fail(
                DomainErrorCodes.NotFound,
                $"Shadow table for projection '{projectionName}' was not found. Run rebuild first.");
        }

        var fieldExpression = projectionName switch
        {
            "LocationBalance" =>
                "id || ':' || COALESCE(data->>'warehouseId','') || ':' || " +
                "COALESCE(data->>'location','') || ':' || " +
                "COALESCE(data->>'sku','') || ':' || " +
                "COALESCE(data->>'quantity','0')",
            "AvailableStock" =>
                "id || ':' || COALESCE(data->>'warehouseId','') || ':' || " +
                "COALESCE(data->>'location','') || ':' || " +
                "COALESCE(data->>'sku','') || ':' || " +
                "COALESCE(data->>'onHandQty','0') || ':' || " +
                "COALESCE(data->>'hardLockedQty','0') || ':' || " +
                "COALESCE(data->>'availableQty','0')",
            _ => "id || data::text"
        };

        var productionChecksum = await ComputeChecksumAsync(
            connection, productionTable, fieldExpression, cancellationToken);
        var shadowChecksum = await ComputeChecksumAsync(
            connection, shadowTable, fieldExpression, cancellationToken);

        var productionRowCount = await CountRowsAsync(connection, productionTable, cancellationToken);
        var shadowRowCount = await CountRowsAsync(connection, shadowTable, cancellationToken);

        return Result<VerifyProjectionResultDto>.Ok(new VerifyProjectionResultDto
        {
            ChecksumMatch = string.Equals(productionChecksum, shadowChecksum, StringComparison.Ordinal),
            ProductionChecksum = productionChecksum,
            ShadowChecksum = shadowChecksum,
            ProductionRowCount = productionRowCount,
            ShadowRowCount = shadowRowCount
        });
    }

    private static async Task<string?> ResolveQualifiedTableNameAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
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

    private static async Task<int> CountRowsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(1) FROM {tableName}";
        var count = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count ?? 0);
    }

    private static async Task<string> ComputeChecksumAsync(
        DbConnection connection,
        string tableName,
        string fieldExpression,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COALESCE(
                MD5(STRING_AGG({fieldExpression}, '|' ORDER BY id)),
                'empty'
            )
            FROM {tableName}";

        return (string?)await cmd.ExecuteScalarAsync(cancellationToken) ?? "empty";
    }
}
