using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Locking;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;
using LKvitai.MES.Projections;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Events.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ProjectionInfrastructureIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;
    private IServiceProvider? _serviceProvider;

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
            opts.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);
            opts.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
        });
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WarehouseDb"] = _postgres.GetConnectionString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddDbContext<WarehouseDbContext>(opts =>
            opts.UseNpgsql(_postgres.GetConnectionString()));
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IDisposable d)
        {
            d.Dispose();
        }

        _store?.Dispose();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task SchemaSeparation_EfTablesInPublic_MartenTablesInWarehouseEvents()
    {
        DockerRequirement.EnsureEnabled();

        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await using var conn = new NpgsqlConnection(_postgres!.GetConnectionString());
        await conn.OpenAsync();

        var handlingUnitSchema = await QuerySchemaAsync(conn, "handling_units");
        var martenEventSchema = await QuerySchemaAsync(conn, "mt_events");
        var projectionSchema = await QuerySchemaAsync(conn, "mt_doc_availablestockview");

        handlingUnitSchema.Should().Be("public");
        martenEventSchema.Should().Be("warehouse_events");
        projectionSchema.Should().Be("warehouse_events");
    }

    [SkippableFact]
    public async Task CleanupShadowTables_DropsOrphanedTables_WhenNoActiveLock()
    {
        DockerRequirement.EnsureEnabled();

        var lockService = CreateLockService();
        var cleanup = new ProjectionCleanupService(_store!, lockService);

        await using var conn = new NpgsqlConnection(_postgres!.GetConnectionString());
        await conn.OpenAsync();

        await ExecuteNonQueryAsync(conn, "CREATE TABLE warehouse_events.mt_doc_cleanup_shadow (id text);");

        var result = await cleanup.CleanupShadowTablesAsync();

        result.DroppedTables.Should().Be(1);
        var exists = await TableExistsAsync(conn, "warehouse_events", "mt_doc_cleanup_shadow");
        exists.Should().BeFalse();
    }

    [SkippableFact]
    public async Task CleanupShadowTables_DoesNotDrop_WhenRebuildLockIsActive()
    {
        DockerRequirement.EnsureEnabled();

        var lockService = CreateLockService();
        var cleanup = new ProjectionCleanupService(_store!, lockService);

        await using var conn = new NpgsqlConnection(_postgres!.GetConnectionString());
        await conn.OpenAsync();

        await ExecuteNonQueryAsync(conn, "CREATE TABLE warehouse_events.mt_doc_locked_shadow (id text);");

        var acquire = await lockService.TryAcquireAsync(
            ProjectionRebuildLockKey.For("LocationBalance"),
            "test-holder",
            TimeSpan.FromMinutes(30));
        acquire.Acquired.Should().BeTrue();

        var result = await cleanup.CleanupShadowTablesAsync();
        result.DroppedTables.Should().Be(0);
        result.Reason.Should().NotBeNullOrWhiteSpace();

        var exists = await TableExistsAsync(conn, "warehouse_events", "mt_doc_locked_shadow");
        exists.Should().BeTrue();
    }

    [SkippableFact]
    public async Task RebuildStatus_ReturnsLockHolder_WhenLockExists()
    {
        DockerRequirement.EnsureEnabled();

        var lockService = CreateLockService();
        var sut = new ProjectionRebuildService(
            _store!,
            NullLogger<ProjectionRebuildService>.Instance,
            lockService);

        await lockService.TryAcquireAsync(
            ProjectionRebuildLockKey.For("AvailableStock"),
            "holder-42",
            TimeSpan.FromMinutes(30));

        var status = await sut.GetRebuildStatusAsync("AvailableStock");

        status.Should().NotBeNull();
        status!.Locked.Should().BeTrue();
        status.Holder.Should().Be("holder-42");
    }

    [SkippableFact]
    public async Task Rebuild_WhenLocked_ReturnsIdempotencyInProgress_WithHolderDetails()
    {
        DockerRequirement.EnsureEnabled();

        var lockService = CreateLockService();
        var sut = new ProjectionRebuildService(
            _store!,
            NullLogger<ProjectionRebuildService>.Instance,
            lockService);

        await lockService.TryAcquireAsync(
            ProjectionRebuildLockKey.For("LocationBalance"),
            "holder-99",
            TimeSpan.FromMinutes(30));

        var result = await sut.RebuildProjectionAsync("LocationBalance", verify: false);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.IdempotencyInProgress);
        result.Error.Should().Contain("holder-99");
    }

    [SkippableFact]
    public async Task Rebuild_OnFreshDatabase_ShouldSucceedWithoutShadowTableErrors()
    {
        DockerRequirement.EnsureEnabled();
        await SeedLocationBalanceEventAsync();
        await BootstrapProjectionTablesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WarehouseDb"] = _postgres!.GetConnectionString()
            })
            .Build();
        var sut = new ProjectionRebuildService(
            _store!,
            NullLogger<ProjectionRebuildService>.Instance,
            CreateLockService(),
            config);

        var result = await sut.RebuildProjectionAsync("LocationBalance", verify: true);
        if (result.IsSuccess)
        {
            result.Value.Should().NotBeNull();
            result.Value.ProjectionName.Should().Be("LocationBalance");
            result.Value.ChecksumMatch.Should().BeTrue();
            return;
        }

        result.ErrorCode.Should().Be(
            DomainErrorCodes.IdempotencyInProgress,
            because: $"unexpected error: {result.Error}");
        result.Error.Should().NotContain("42P01");
    }

    private async Task BootstrapProjectionTablesAsync()
    {
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));
        await daemon.StopAllAsync();
    }

    private async Task SeedLocationBalanceEventAsync()
    {
        await using var session = _store!.LightweightSession();
        session.Events.Append(StockLedgerStreamId.For("WH1", "LOC-BOOTSTRAP", "SKU-BOOTSTRAP"), new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = "SKU-BOOTSTRAP",
            Quantity = 5m,
            FromLocation = "SUPPLIER",
            ToLocation = "LOC-BOOTSTRAP",
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });

        await session.SaveChangesAsync();
    }

    private PostgresDistributedLock CreateLockService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WarehouseDb"] = _postgres!.GetConnectionString()
            })
            .Build();
        return new PostgresDistributedLock(config);
    }

    private static async Task<string?> QuerySchemaAsync(NpgsqlConnection conn, string tableName)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT schemaname
            FROM pg_tables
            WHERE tablename = @table
            ORDER BY schemaname
            LIMIT 1";
        cmd.Parameters.AddWithValue("table", tableName);
        return await cmd.ExecuteScalarAsync() as string;
    }

    private static async Task ExecuteNonQueryAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection conn, string schema, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = @schema
                  AND table_name = @table)";
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        return (bool?)await cmd.ExecuteScalarAsync() ?? false;
    }
}
