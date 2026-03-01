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
        var rawEndpoint = ReadValue(section, "Endpoint", "ITEMIMAGES__ENDPOINT");

        return new ItemImageOptions
        {
            Endpoint = NormalizeEndpoint(rawEndpoint),
            BucketName = ReadValue(section, "BucketName", "ITEMIMAGES__BUCKET", section["Bucket"] ?? string.Empty),
            UseSsl = bool.TryParse(ReadValue(section, "UseSsl", "ITEMIMAGES__USESSL"), out var useSsl) && useSsl,
            AccessKey = ReadValue(section, "AccessKey", "ITEMIMAGES__ACCESSKEY"),
            SecretKey = ReadValue(section, "SecretKey", "ITEMIMAGES__SECRETKEY"),
            MaxUploadMb = int.TryParse(ReadValue(section, "MaxUploadMb", "ITEMIMAGES__MAXUPLOADMB"), out var maxUploadMb) ? maxUploadMb : 5,
            CacheMaxAgeSeconds = int.TryParse(ReadValue(section, "CacheMaxAgeSeconds", "ITEMIMAGES__CACHEMAXAGESECONDS"), out var cacheTtlSeconds) ? cacheTtlSeconds : 86400,
            ModelPath = ReadValue(section, "ModelPath", "ITEMIMAGES__MODEL_PATH")
        };
    }

    private static string ReadValue(
        IConfigurationSection section,
        string key,
        string envKey,
        string defaultValue = "")
    {
        var fromSection = section[key];
        if (!string.IsNullOrWhiteSpace(fromSection))
        {
            return fromSection;
        }

        var fromEnv = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        return defaultValue;
    }

    private static string NormalizeEndpoint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();

        if (value.Contains("://", StringComparison.Ordinal) &&
            Uri.TryCreate(value, UriKind.Absolute, out var absolute))
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
