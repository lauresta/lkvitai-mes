namespace LKvitai.MES.BuildingBlocks.ObjectStorage;

public interface IObjectStorageService
{
    ObjectStorageOptions Options { get; }
    Task<ObjectStorageAvailability> EnsureAvailableAsync(CancellationToken cancellationToken = default);
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
