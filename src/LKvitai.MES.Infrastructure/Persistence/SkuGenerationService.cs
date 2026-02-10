using LKvitai.MES.Application.Services;
using LKvitai.MES.Domain.Entities;
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
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var sequence = await _dbContext.SKUSequences
                        .SingleOrDefaultAsync(x => x.Prefix == prefix, cancellationToken);

                    int next;
                    if (sequence is null)
                    {
                        next = 1;
                        _dbContext.SKUSequences.Add(new SKUSequence
                        {
                            Prefix = prefix,
                            NextValue = 2
                        });
                    }
                    else
                    {
                        next = sequence.NextValue;
                        sequence.NextValue += 1;
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    return $"{prefix}-{next:D4}";
                }
                catch (DbUpdateConcurrencyException) when (attempt < 3)
                {
                    await tx.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                }
                catch (DbUpdateException ex) when (attempt < 3 && IsUniqueConstraintViolation(ex))
                {
                    await tx.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                }
            }

            throw new InvalidOperationException("Could not generate SKU due to repeated concurrency conflicts.");
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

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
