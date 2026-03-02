using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class MasterDataFoundationIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private DbContextOptions<WarehouseDbContext>? _options;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();
        await _postgres.StartAsync();

        await using (var conn = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
            await cmd.ExecuteNonQueryAsync();
        }

        _options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Items_DuplicateSku_ShouldFailWithUniqueConstraint()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext("user-a");
        await SeedCategoryAndUomAsync(db);

        db.Items.Add(new Item
        {
            InternalSKU = "RM-0001",
            Name = "First Item",
            CategoryId = 1,
            BaseUoM = "PCS",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        db.Items.Add(new Item
        {
            InternalSKU = "RM-0001",
            Name = "Second Item",
            CategoryId = 1,
            BaseUoM = "PCS",
            Status = "Active"
        });

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task Items_InvalidCategory_ShouldFailWithForeignKeyConstraint()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext("user-b");
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        await db.SaveChangesAsync();

        db.Items.Add(new Item
        {
            InternalSKU = "RM-0002",
            Name = "Invalid Category Item",
            CategoryId = 999,
            BaseUoM = "PCS",
            Status = "Active"
        });

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task AuditableEntities_ShouldPopulateCreatedAndUpdatedFields()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext("audit-user");

        var supplier = new Supplier
        {
            Code = "SUP-001",
            Name = "Supplier One"
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        supplier.CreatedBy.Should().Be("audit-user");
        supplier.CreatedAt.Should().NotBe(default);
        supplier.UpdatedBy.Should().BeNull();
        supplier.UpdatedAt.Should().BeNull();

        supplier.Name = "Supplier One Updated";
        await db.SaveChangesAsync();

        supplier.UpdatedBy.Should().Be("audit-user");
        supplier.UpdatedAt.Should().NotBeNull();
    }

    private WarehouseDbContext CreateDbContext(string userId)
        => new(_options!, new StaticCurrentUserService(userId));

    private async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext("system");
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    private static async Task SeedCategoryAndUomAsync(WarehouseDbContext db)
    {
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "RAW", Name = "Raw Materials" });
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        await db.SaveChangesAsync();
    }

    private sealed class StaticCurrentUserService : ICurrentUserService
    {
        private readonly string _userId;

        public StaticCurrentUserService(string userId)
        {
            _userId = userId;
        }

        public string GetCurrentUserId() => _userId;
    }
}
