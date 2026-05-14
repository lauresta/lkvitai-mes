namespace LKvitai.MES.BuildingBlocks.ObjectStorage;

public sealed class UnavailableObjectStorageService : IObjectStorageService
{
    private readonly string _reason;

    public UnavailableObjectStorageService(ObjectStorageOptions options, string reason)
    {
        Options = options;
        _reason = reason;
    }

    public ObjectStorageOptions Options { get; }

    public Task<ObjectStorageAvailability> EnsureAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ObjectStorageAvailability(false, _reason));

    public Task PutObjectAsync(string objectKey, Stream stream, long size, string contentType, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(_reason);

    public Task<MemoryStream> GetObjectAsync(string objectKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(_reason);

    public Task<string?> TryGetObjectEtagAsync(string objectKey, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(_reason);
}
