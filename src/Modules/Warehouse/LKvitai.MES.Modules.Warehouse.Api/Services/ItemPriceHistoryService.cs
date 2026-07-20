using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface IItemPriceHistoryService
{
    Task WriteAsync(ItemPriceHistoryWriteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemPriceHistoryDto>> QueryAsync(int itemId, CancellationToken cancellationToken = default);
}

public sealed class ItemPriceHistoryService : IItemPriceHistoryService
{
    private readonly WarehouseDbContext _dbContext;

    public ItemPriceHistoryService(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(ItemPriceHistoryWriteRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new ItemPriceHistory
        {
            ItemId = request.ItemId,
            PriceType = request.PriceType,
            PriceGroupId = request.PriceGroupId,
            OldAmount = request.OldAmount,
            NewAmount = request.NewAmount,
            ChangedBy = string.IsNullOrWhiteSpace(request.ChangedBy) ? "unknown" : request.ChangedBy.Trim(),
            ChangedAt = request.ChangedAt == default ? DateTimeOffset.UtcNow : request.ChangedAt,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        };

        _dbContext.ItemPriceHistories.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemPriceHistoryDto>> QueryAsync(int itemId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ItemPriceHistories
            .AsNoTracking()
            .Where(x => x.ItemId == itemId)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => new ItemPriceHistoryDto(
                x.Id,
                x.ItemId,
                x.PriceType,
                x.PriceGroupId,
                x.OldAmount,
                x.NewAmount,
                x.ChangedBy,
                x.ChangedAt,
                x.Reason))
            .ToListAsync(cancellationToken);
    }
}

public sealed record ItemPriceHistoryWriteRequest(
    int ItemId,
    string PriceType,
    int? PriceGroupId,
    decimal? OldAmount,
    decimal NewAmount,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string? Reason);

public sealed record ItemPriceHistoryDto(
    long Id,
    int ItemId,
    string PriceType,
    int? PriceGroupId,
    decimal? OldAmount,
    decimal NewAmount,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string? Reason);
