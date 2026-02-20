using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Projections;
using Marten;
using Marten.Events.Projections;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

[Trait("Category", "Idempotency")]
public class ProjectionIdempotencyIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.DatabaseSchemaName = "warehouse_events";
            opts.Events.DatabaseSchemaName = "warehouse_events";
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            opts.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
        });

        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _store?.Dispose();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task ProjectionDaemon_ReprocessingWithoutNewEvents_ShouldNotDoubleStock()
    {
        DockerRequirement.EnsureEnabled();

        var streamId = "inbound-shipment:idempotency";
        await using (var session = _store!.LightweightSession())
        {
            session.Events.Append(streamId, new GoodsReceivedEvent
            {
                AggregateId = Guid.NewGuid(),
                WarehouseId = "WH1",
                ShipmentId = 1,
                LineId = 1,
                ItemId = 1,
                SKU = "RM-0001",
                ReceivedQty = 100m,
                BaseUoM = "PCS",
                DestinationLocationId = 1,
                DestinationLocation = "RECEIVING",
                Timestamp = DateTime.UtcNow
            });

            await session.SaveChangesAsync();
        }

        await RunDaemonAsync();

        var qtyAfterFirstPass = await ReadOnHandQtyAsync("WH1", "RECEIVING", "RM-0001");
        qtyAfterFirstPass.Should().Be(100m);

        // Re-run daemon without appending new events: projection state must stay stable.
        await RunDaemonAsync();

        var qtyAfterSecondPass = await ReadOnHandQtyAsync("WH1", "RECEIVING", "RM-0001");
        qtyAfterSecondPass.Should().Be(100m);
    }

    private async Task RunDaemonAsync()
    {
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(15));
        await daemon.StopAllAsync();
    }

    private async Task<decimal> ReadOnHandQtyAsync(string warehouseId, string location, string sku)
    {
        await using var query = _store!.QuerySession();
        var row = await Marten.QueryableExtensions.SingleOrDefaultAsync(
            query.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == warehouseId && x.Location == location && x.SKU == sku),
            CancellationToken.None);

        return row?.OnHandQty ?? 0m;
    }
}
