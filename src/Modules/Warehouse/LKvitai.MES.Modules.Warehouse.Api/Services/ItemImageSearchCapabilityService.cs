using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed record ItemImageSearchCapability(
    bool IsEnabled,
    string? DisabledReason,
    bool HasPgvector,
    bool HasModelFile);

public interface IItemImageSearchCapabilityService
{
    Task<ItemImageSearchCapability> GetCapabilityAsync(CancellationToken cancellationToken = default);
}

public sealed class ItemImageSearchCapabilityService : IItemImageSearchCapabilityService
{
    private readonly IItemImageStorageService _storage;
    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<ItemImageSearchCapabilityService> _logger;

    public ItemImageSearchCapabilityService(
        IItemImageStorageService storage,
        WarehouseDbContext dbContext,
        ILogger<ItemImageSearchCapabilityService> logger)
    {
        _storage = storage;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ItemImageSearchCapability> GetCapabilityAsync(CancellationToken cancellationToken = default)
    {
        var modelPath = _storage.Options.ModelPath?.Trim();
        var hasModel = !string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath);
        if (!hasModel)
        {
            return new ItemImageSearchCapability(
                false,
                "Image search model is unavailable. Configure ITEMIMAGES__MODEL_PATH to an existing file.",
                false,
                false);
        }

        bool hasPgvector;
        try
        {
            var result = await _dbContext.Database.SqlQueryRaw<bool>(
                    "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector');")
                .FirstAsync(cancellationToken);
            hasPgvector = result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed checking pgvector capability.");
            return new ItemImageSearchCapability(
                false,
                "Unable to verify pgvector capability.",
                false,
                true);
        }

        if (!hasPgvector)
        {
            return new ItemImageSearchCapability(
                false,
                "pgvector extension is not enabled in this environment.",
                false,
                true);
        }

        return new ItemImageSearchCapability(true, null, true, true);
    }
}
