using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LKvitai.MES.Infrastructure.Persistence;

public sealed class SkuGenerationService : ISkuGenerationService
{
    private readonly WarehouseDbContext _dbContext;

    public SkuGenerationService(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateNextSkuAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var prefix = await ResolvePrefixAsync(categoryId, cancellationToken);
        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            // Atomic upsert to allocate SKU number.
            // Uses raw command execution because INSERT..RETURNING is non-composable in EF LINQ.
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO public.sku_sequences ("Prefix", "NextValue", "RowVersion")
                VALUES (@prefix, 2, '\x'::bytea)
                ON CONFLICT ("Prefix")
                DO UPDATE SET "NextValue" = public.sku_sequences."NextValue" + 1
                RETURNING "NextValue";
                """;

            var prefixParameter = new NpgsqlParameter("@prefix", prefix);
            command.Parameters.Add(prefixParameter);

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null || scalar is DBNull)
            {
                throw new InvalidOperationException("SKU sequence allocation did not return a value.");
            }

            var nextValue = Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);

            var issuedNumber = nextValue - 1;
            return $"{prefix}-{issuedNumber:D4}";
        });
    }

    private async Task<string> ResolvePrefixAsync(int categoryId, CancellationToken cancellationToken)
    {
        var categoryCode = await _dbContext.ItemCategories
            .Where(c => c.Id == categoryId)
            .Select(c => c.Code)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.Equals(categoryCode, "RAW", StringComparison.OrdinalIgnoreCase))
        {
            return "RM";
        }

        if (string.Equals(categoryCode, "FINISHED", StringComparison.OrdinalIgnoreCase))
        {
            return "FG";
        }

        return "ITEM";
    }
}
