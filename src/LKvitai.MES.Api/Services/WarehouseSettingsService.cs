using LKvitai.MES.Application.Services;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LKvitai.MES.Api.Services;

public interface IWarehouseSettingsService
{
    Task<WarehouseSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<Result<WarehouseSettingsDto>> UpdateAsync(
        UpdateWarehouseSettingsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class WarehouseSettingsService : IWarehouseSettingsService
{
    private const string CacheKey = "warehouse-settings:singleton";

    private readonly WarehouseDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WarehouseSettingsService> _logger;

    public WarehouseSettingsService(
        WarehouseDbContext dbContext,
        IMemoryCache memoryCache,
        ICurrentUserService currentUserService,
        ILogger<WarehouseSettingsService> logger)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<WarehouseSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(CacheKey, out WarehouseSettingsDto? cached) && cached is not null)
        {
            return cached;
        }

        var entity = await GetOrCreateAsync(cancellationToken);
        var dto = Map(entity);
        SetCache(dto);

        return dto;
    }

    public async Task<Result<WarehouseSettingsDto>> UpdateAsync(
        UpdateWarehouseSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CapacityThresholdPercent is < 0 or > 100)
        {
            return Result<WarehouseSettingsDto>.Fail(
                DomainErrorCodes.ValidationError,
                "Capacity threshold must be 0-100%.");
        }

        if (request.LowStockThreshold < 0)
        {
            return Result<WarehouseSettingsDto>.Fail(
                DomainErrorCodes.ValidationError,
                "Low stock threshold must be >= 0.");
        }

        if (request.ReorderPoint < 0)
        {
            return Result<WarehouseSettingsDto>.Fail(
                DomainErrorCodes.ValidationError,
                "Reorder point must be >= 0.");
        }

        if (!Enum.TryParse<PickStrategy>(request.DefaultPickStrategy, true, out var defaultPickStrategy) ||
            !Enum.IsDefined(defaultPickStrategy))
        {
            return Result<WarehouseSettingsDto>.Fail(
                DomainErrorCodes.ValidationError,
                "Default pick strategy must be FEFO or FIFO.");
        }

        var entity = await GetOrCreateAsync(cancellationToken);
        var previous = Map(entity);

        entity.CapacityThresholdPercent = request.CapacityThresholdPercent;
        entity.DefaultPickStrategy = defaultPickStrategy;
        entity.LowStockThreshold = request.LowStockThreshold;
        entity.ReorderPoint = request.ReorderPoint;
        entity.AutoAllocateOrders = request.AutoAllocateOrders;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var current = Map(entity);
        SetCache(current);

        _logger.LogInformation(
            "Warehouse settings updated by {UpdatedBy}. Capacity={OldCapacity}->{NewCapacity}, PickStrategy={OldPickStrategy}->{NewPickStrategy}, LowStock={OldLowStock}->{NewLowStock}, Reorder={OldReorder}->{NewReorder}, AutoAllocate={OldAutoAllocate}->{NewAutoAllocate}",
            _currentUserService.GetCurrentUserId(),
            previous.CapacityThresholdPercent,
            current.CapacityThresholdPercent,
            previous.DefaultPickStrategy,
            current.DefaultPickStrategy,
            previous.LowStockThreshold,
            current.LowStockThreshold,
            previous.ReorderPoint,
            current.ReorderPoint,
            previous.AutoAllocateOrders,
            current.AutoAllocateOrders);

        return Result<WarehouseSettingsDto>.Ok(current);
    }

    private async Task<WarehouseSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var entity = await _dbContext.WarehouseSettings
            .SingleOrDefaultAsync(x => x.Id == WarehouseSettings.SingletonId, cancellationToken);

        if (entity is not null)
        {
            return entity;
        }

        entity = new WarehouseSettings();
        _dbContext.WarehouseSettings.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    private void SetCache(WarehouseSettingsDto dto)
    {
        _memoryCache.Set(
            CacheKey,
            dto,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });
    }

    private static WarehouseSettingsDto Map(WarehouseSettings entity)
    {
        return new WarehouseSettingsDto(
            entity.Id,
            entity.CapacityThresholdPercent,
            entity.DefaultPickStrategy.ToString(),
            entity.LowStockThreshold,
            entity.ReorderPoint,
            entity.AutoAllocateOrders,
            entity.UpdatedBy,
            entity.UpdatedAt);
    }
}

public sealed record WarehouseSettingsDto(
    int Id,
    int CapacityThresholdPercent,
    string DefaultPickStrategy,
    int LowStockThreshold,
    int ReorderPoint,
    bool AutoAllocateOrders,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt);

public sealed record UpdateWarehouseSettingsRequest(
    int CapacityThresholdPercent,
    string DefaultPickStrategy,
    int LowStockThreshold,
    int ReorderPoint,
    bool AutoAllocateOrders);
