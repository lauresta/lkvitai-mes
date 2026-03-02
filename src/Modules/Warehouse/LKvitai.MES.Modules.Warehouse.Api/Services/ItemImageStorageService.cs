using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

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
    private readonly IMinioClient _minioClient;
    private readonly ILogger<ItemImageStorageService> _logger;
    private readonly SemaphoreSlim _availabilityLock = new(1, 1);

    private DateTimeOffset _lastAvailabilityCheck = DateTimeOffset.MinValue;
    private ItemImageAvailability _lastAvailability = new(false, "ItemImages not initialized.");

    public ItemImageStorageService(
        IMinioClient minioClient,
        ItemImageOptions options,
        ILogger<ItemImageStorageService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
        Options = options;
    }

    public ItemImageOptions Options { get; }

    public async Task<ItemImageAvailability> EnsureAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (DateTimeOffset.UtcNow - _lastAvailabilityCheck < TimeSpan.FromSeconds(30))
        {
            return _lastAvailability;
        }

        await _availabilityLock.WaitAsync(cancellationToken);
        try
        {
            if (DateTimeOffset.UtcNow - _lastAvailabilityCheck < TimeSpan.FromSeconds(30))
            {
                return _lastAvailability;
            }

            var missing = GetMissingConfigurationKeys();
            if (missing.Count > 0)
            {
                _lastAvailability = new ItemImageAvailability(
                    false,
                    $"Missing ItemImages configuration: {string.Join(", ", missing)}");
                _lastAvailabilityCheck = DateTimeOffset.UtcNow;
                return _lastAvailability;
            }

            try
            {
                var exists = await _minioClient.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(Options.BucketName),
                    cancellationToken);

                _lastAvailability = exists
                    ? new ItemImageAvailability(true, null)
                    : new ItemImageAvailability(false, $"Bucket '{Options.BucketName}' does not exist.");
            }
            catch (AccessDeniedException)
            {
                _lastAvailability = new ItemImageAvailability(
                    false,
                    $"Access denied for bucket '{Options.BucketName}'.");
            }
            catch (MinioException ex)
            {
                _logger.LogWarning(ex, "Failed validating MinIO access.");
                _lastAvailability = new ItemImageAvailability(false, ex.Message);
            }

            _lastAvailabilityCheck = DateTimeOffset.UtcNow;
            return _lastAvailability;
        }
        finally
        {
            _availabilityLock.Release();
        }
    }

    public async Task PutObjectAsync(
        string objectKey,
        Stream stream,
        long size,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await _minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(Options.BucketName)
                .WithObject(objectKey)
                .WithStreamData(stream)
                .WithObjectSize(size)
                .WithContentType(contentType),
            cancellationToken);
    }

    public async Task<MemoryStream> GetObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var result = new MemoryStream();
        await _minioClient.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(Options.BucketName)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(result)),
            cancellationToken);
        result.Position = 0;
        return result;
    }

    public async Task<string?> TryGetObjectEtagAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var stat = await _minioClient.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(Options.BucketName)
                    .WithObject(objectKey),
                cancellationToken);

            return stat.ETag?.Trim('"');
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await _minioClient.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(Options.BucketName)
                .WithObject(objectKey),
            cancellationToken);
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
