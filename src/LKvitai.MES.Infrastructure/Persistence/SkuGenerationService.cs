using LKvitai.MES.Application.Services;
using LKvitai.MES.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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
            // Inserts first row per prefix, then increments on conflict.
            var nextValue = await _dbContext.Database
                .SqlQuery<int>($"""
                    INSERT INTO public.sku_sequences ("Prefix", "NextValue", "RowVersion")
                    VALUES ({prefix}, 2, '\x'::bytea)
                    ON CONFLICT ("Prefix")
                    DO UPDATE SET "NextValue" = public.sku_sequences."NextValue" + 1
                    RETURNING "NextValue";
                    """)
                .SingleAsync(cancellationToken);

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
