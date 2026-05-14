namespace LKvitai.MES.BuildingBlocks.ObjectStorage;

public sealed record ObjectStorageAvailability(bool IsAvailable, string? Reason);

public sealed class ObjectStorageOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public bool UseSsl { get; init; }
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public int CacheMaxAgeSeconds { get; init; } = 86400;

    public bool HasRequiredConfiguration =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(BucketName) &&
        !string.IsNullOrWhiteSpace(AccessKey) &&
        !string.IsNullOrWhiteSpace(SecretKey);
}
