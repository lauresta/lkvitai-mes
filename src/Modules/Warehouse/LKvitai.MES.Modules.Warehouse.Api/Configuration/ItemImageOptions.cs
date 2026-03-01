namespace LKvitai.MES.Modules.Warehouse.Api.Configuration;

public sealed class ItemImageOptions
{
    public const string SectionName = "ItemImages";
    public const string ObjectKeyPrefix = "item-photos";

    public string Endpoint { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public bool UseSsl { get; init; }
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public int MaxUploadMb { get; init; } = 5;
    public int CacheMaxAgeSeconds { get; init; } = 86400;
    public string? ModelPath { get; init; }

    public static ItemImageOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new ItemImageOptions
        {
            Endpoint = section["Endpoint"] ?? string.Empty,
            BucketName = section["BucketName"] ?? section["Bucket"] ?? string.Empty,
            UseSsl = bool.TryParse(section["UseSsl"], out var useSsl) && useSsl,
            AccessKey = section["AccessKey"] ?? string.Empty,
            SecretKey = section["SecretKey"] ?? string.Empty,
            MaxUploadMb = int.TryParse(section["MaxUploadMb"], out var maxUploadMb) ? maxUploadMb : 5,
            CacheMaxAgeSeconds = int.TryParse(section["CacheMaxAgeSeconds"], out var cacheTtlSeconds) ? cacheTtlSeconds : 86400,
            ModelPath = section["ModelPath"]
        };
    }
}
