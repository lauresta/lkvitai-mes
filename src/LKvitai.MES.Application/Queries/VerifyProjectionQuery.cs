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
    private const string OutboundOrderSummaryTable = "outbound_order_summary";
    private const string ShipmentSummaryTable = "shipment_summary";
    private const string DispatchHistoryTable = "dispatch_history";
    private const string OnHandValueTable = "on_hand_value";
    private const string InboundShipmentSummaryTable = "mt_doc_inboundshipmentsummaryview";

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
            "OutboundOrderSummary" => OutboundOrderSummaryTable,
            "ShipmentSummary" => ShipmentSummaryTable,
            "DispatchHistory" => DispatchHistoryTable,
            "OnHandValue" => OnHandValueTable,
            "InboundShipmentSummary" => InboundShipmentSummaryTable,
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
            "OutboundOrderSummary" =>
                "\"Id\"::text || ':' || COALESCE(\"OrderNumber\",'') || ':' || " +
                "COALESCE(\"Type\",'') || ':' || COALESCE(\"Status\",'') || ':' || " +
                "COALESCE(\"CustomerName\",'') || ':' || COALESCE(\"ItemCount\"::text,'0') || ':' || " +
                "COALESCE(\"OrderDate\"::text,'') || ':' || COALESCE(\"RequestedShipDate\"::text,'') || ':' || " +
                "COALESCE(\"PackedAt\"::text,'') || ':' || COALESCE(\"ShippedAt\"::text,'') || ':' || " +
                "COALESCE(\"ShipmentId\"::text,'') || ':' || COALESCE(\"ShipmentNumber\",'') || ':' || " +
                "COALESCE(\"TrackingNumber\",'')",
            "ShipmentSummary" =>
                "\"Id\"::text || ':' || COALESCE(\"ShipmentNumber\",'') || ':' || " +
                "COALESCE(\"OutboundOrderId\"::text,'') || ':' || COALESCE(\"OutboundOrderNumber\",'') || ':' || " +
                "COALESCE(\"CustomerName\",'') || ':' || COALESCE(\"Carrier\",'') || ':' || " +
                "COALESCE(\"TrackingNumber\",'') || ':' || COALESCE(\"Status\",'') || ':' || " +
                "COALESCE(\"PackedAt\"::text,'') || ':' || COALESCE(\"DispatchedAt\"::text,'') || ':' || " +
                "COALESCE(\"DeliveredAt\"::text,'') || ':' || COALESCE(\"PackedBy\",'') || ':' || " +
                "COALESCE(\"DispatchedBy\",'')",
            "DispatchHistory" =>
                "\"Id\"::text || ':' || COALESCE(\"ShipmentId\"::text,'') || ':' || " +
                "COALESCE(\"ShipmentNumber\",'') || ':' || COALESCE(\"OutboundOrderNumber\",'') || ':' || " +
                "COALESCE(\"Carrier\",'') || ':' || COALESCE(\"TrackingNumber\",'') || ':' || " +
                "COALESCE(\"VehicleId\",'') || ':' || COALESCE(\"DispatchedAt\"::text,'') || ':' || " +
                "COALESCE(\"DispatchedBy\",'') || ':' || COALESCE(\"ManualTracking\"::text,'')",
            "OnHandValue" =>
                "\"Id\"::text || ':' || COALESCE(\"ItemId\"::text,'') || ':' || " +
                "COALESCE(\"ItemSku\",'') || ':' || COALESCE(\"ItemName\",'') || ':' || " +
                "COALESCE(\"CategoryId\"::text,'') || ':' || COALESCE(\"CategoryName\",'') || ':' || " +
                "COALESCE(\"Qty\"::text,'0') || ':' || COALESCE(\"UnitCost\"::text,'0') || ':' || " +
                "COALESCE(\"TotalValue\"::text,'0') || ':' || COALESCE(\"LastUpdated\"::text,'')",
            "InboundShipmentSummary" =>
                "id || ':' || COALESCE(data->>'shipmentId','0') || ':' || " +
                "COALESCE(data->>'referenceNumber','') || ':' || " +
                "COALESCE(data->>'supplierId','0') || ':' || " +
                "COALESCE(data->>'supplierName','') || ':' || " +
                "COALESCE(data->>'totalExpectedQty','0') || ':' || " +
                "COALESCE(data->>'totalReceivedQty','0') || ':' || " +
                "COALESCE(data->>'completionPercent','0') || ':' || " +
                "COALESCE(data->>'totalLines','0') || ':' || " +
                "COALESCE(data->>'status','') || ':' || " +
                "COALESCE(data->>'expectedDate','') || ':' || " +
                "COALESCE(data->>'createdAt','') || ':' || " +
                "COALESCE(data->>'lastUpdated','')",
            _ => "id || data::text"
        };

        var sortExpression = projectionName switch
        {
            "OutboundOrderSummary" => "\"Id\"",
            "ShipmentSummary" => "\"Id\"",
            "DispatchHistory" => "\"Id\"",
            "OnHandValue" => "\"Id\"",
            _ => "id"
        };

        var productionChecksum = await ComputeChecksumAsync(
            connection, productionTable, fieldExpression, sortExpression, cancellationToken);
        var shadowChecksum = await ComputeChecksumAsync(
            connection, shadowTable, fieldExpression, sortExpression, cancellationToken);

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
        string sortExpression,
        CancellationToken cancellationToken)
    {
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
