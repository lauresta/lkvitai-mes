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
        var rawEndpoint = section["Endpoint"] ?? string.Empty;

        return new ItemImageOptions
        {
            Endpoint = NormalizeEndpoint(rawEndpoint),
            BucketName = section["BucketName"] ?? section["Bucket"] ?? string.Empty,
            UseSsl = bool.TryParse(section["UseSsl"], out var useSsl) && useSsl,
            AccessKey = section["AccessKey"] ?? string.Empty,
            SecretKey = section["SecretKey"] ?? string.Empty,
            MaxUploadMb = int.TryParse(section["MaxUploadMb"], out var maxUploadMb) ? maxUploadMb : 5,
            CacheMaxAgeSeconds = int.TryParse(section["CacheMaxAgeSeconds"], out var cacheTtlSeconds) ? cacheTtlSeconds : 86400,
            ModelPath = section["ModelPath"]
        };
    }

    private static string NormalizeEndpoint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return absolute.IsDefaultPort
                ? absolute.Host
                : $"{absolute.Host}:{absolute.Port}";
        }

        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            value = value[(schemeIndex + 3)..];
        }

        var slashIndex = value.IndexOf('/');
        if (slashIndex >= 0)
        {
            value = value[..slashIndex];
        }

        return value;
    }
}
