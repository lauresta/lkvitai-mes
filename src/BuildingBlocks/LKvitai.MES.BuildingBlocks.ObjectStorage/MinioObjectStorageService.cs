using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace LKvitai.MES.BuildingBlocks.ObjectStorage;

public sealed class MinioObjectStorageService : IObjectStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioObjectStorageService> _logger;
    private readonly SemaphoreSlim _availabilityLock = new(1, 1);

    private DateTimeOffset _lastAvailabilityCheck = DateTimeOffset.MinValue;
    private ObjectStorageAvailability _lastAvailability = new(false, "Object storage not initialized.");

    public MinioObjectStorageService(
        IMinioClient minioClient,
        ObjectStorageOptions options,
        ILogger<MinioObjectStorageService> logger)
    {
        _minioClient = minioClient;
        Options = options;
        _logger = logger;
    }

    public ObjectStorageOptions Options { get; }

    public async Task<ObjectStorageAvailability> EnsureAvailableAsync(CancellationToken cancellationToken = default)
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

            if (!Options.HasRequiredConfiguration)
            {
                _lastAvailability = new ObjectStorageAvailability(false, "Missing object storage configuration.");
                _lastAvailabilityCheck = DateTimeOffset.UtcNow;
                return _lastAvailability;
            }

            try
            {
                var exists = await _minioClient.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(Options.BucketName),
                    cancellationToken);

                _lastAvailability = exists
                    ? new ObjectStorageAvailability(true, null)
                    : new ObjectStorageAvailability(false, $"Bucket '{Options.BucketName}' does not exist.");
            }
            catch (AccessDeniedException)
            {
                _lastAvailability = new ObjectStorageAvailability(false, $"Access denied for bucket '{Options.BucketName}'.");
            }
            catch (MinioException ex)
            {
                _logger.LogWarning(ex, "Failed validating object storage access.");
                _lastAvailability = new ObjectStorageAvailability(false, ex.Message);
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
}
