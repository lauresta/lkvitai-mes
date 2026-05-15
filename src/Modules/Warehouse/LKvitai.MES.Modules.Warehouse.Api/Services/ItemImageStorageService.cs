using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.BuildingBlocks.ObjectStorage;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed record ItemImageAvailability(bool IsAvailable, string? Reason);

public interface IItemImageStorageService
{
    ItemImageOptions Options { get; }
    Task<ItemImageAvailability> EnsureAvailableAsync(CancellationToken cancellationToken = default);
    Task PutObjectAsync(
        string objectKey,
        Stream stream,
        long size,
        string contentType,
        CancellationToken cancellationToken = default);
    Task<MemoryStream> GetObjectAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<string?> TryGetObjectEtagAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default);
}

public sealed class ItemImageStorageService : IItemImageStorageService
{
    private readonly IObjectStorageService _objectStorage;

    public ItemImageStorageService(
        IObjectStorageService objectStorage,
        ItemImageOptions options,
        ILogger<ItemImageStorageService> logger)
    {
        _objectStorage = objectStorage;
        Options = options;
    }

    public ItemImageOptions Options { get; }

    public async Task<ItemImageAvailability> EnsureAvailableAsync(CancellationToken cancellationToken = default)
    {
        var missing = GetMissingConfigurationKeys();
        if (missing.Count > 0)
        {
            return new ItemImageAvailability(false, $"Missing ItemImages configuration: {string.Join(", ", missing)}");
        }

        var availability = await _objectStorage.EnsureAvailableAsync(cancellationToken);
        return new ItemImageAvailability(availability.IsAvailable, availability.Reason);
    }

    public async Task PutObjectAsync(
        string objectKey,
        Stream stream,
        long size,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await _objectStorage.PutObjectAsync(objectKey, stream, size, contentType, cancellationToken);
    }

    public async Task<MemoryStream> GetObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        return await _objectStorage.GetObjectAsync(objectKey, cancellationToken);
    }

    public async Task<string?> TryGetObjectEtagAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        return await _objectStorage.TryGetObjectEtagAsync(objectKey, cancellationToken);
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await _objectStorage.DeleteObjectAsync(objectKey, cancellationToken);
    }

    private List<string> GetMissingConfigurationKeys()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(Options.Endpoint))
        {
            missing.Add("ITEMIMAGES__ENDPOINT");
        }

        if (string.IsNullOrWhiteSpace(Options.BucketName))
        {
            missing.Add("ITEMIMAGES__BUCKET");
        }

        if (string.IsNullOrWhiteSpace(Options.AccessKey))
        {
            missing.Add("ITEMIMAGES__ACCESSKEY");
        }

        if (string.IsNullOrWhiteSpace(Options.SecretKey))
        {
            missing.Add("ITEMIMAGES__SECRETKEY");
        }

        return missing;
    }
}
