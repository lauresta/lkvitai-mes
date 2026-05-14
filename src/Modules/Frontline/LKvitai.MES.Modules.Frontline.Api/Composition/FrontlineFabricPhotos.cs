using LKvitai.MES.BuildingBlocks.ObjectStorage;
using LKvitai.MES.Modules.Frontline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Minio;
using Npgsql;

namespace LKvitai.MES.Modules.Frontline.Api.Composition;

public static class FrontlineFabricPhotos
{
    public static IServiceCollection AddFrontlineFabricPhotos(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("WarehouseDb")
            ?? configuration.GetConnectionString("FabricPhotosDb");

        if (!string.IsNullOrWhiteSpace(rawConnectionString))
        {
            var builder = new NpgsqlConnectionStringBuilder(rawConnectionString)
            {
                MinPoolSize = 1,
                MaxPoolSize = 20,
                Timeout = 30
            };

            services.AddDbContext<FabricPhotoDbContext>(options =>
            {
                options.UseNpgsql(builder.ConnectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
            });
        }
        else
        {
            services.AddDbContext<FabricPhotoDbContext>(options =>
                options.UseInMemoryDatabase("frontline-fabric-photos-disabled"));
        }

        var storageOptions = ReadObjectStorageOptions(configuration);
        services.AddSingleton(storageOptions);
        if (storageOptions.HasRequiredConfiguration)
        {
            services.AddSingleton<IMinioClient>(_ => new MinioClient()
                .WithEndpoint(storageOptions.Endpoint)
                .WithCredentials(storageOptions.AccessKey, storageOptions.SecretKey)
                .WithSSL(storageOptions.UseSsl)
                .Build());
            services.AddSingleton<IObjectStorageService, MinioObjectStorageService>();
        }
        else
        {
            services.AddSingleton<IObjectStorageService>(sp =>
                new UnavailableObjectStorageService(
                    sp.GetRequiredService<ObjectStorageOptions>(),
                    "Fabric photo object storage is not configured."));
        }
        services.AddScoped<IFabricPhotoService, FabricPhotoService>();
        return services;
    }

    private static ObjectStorageOptions ReadObjectStorageOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("ItemImages");
        var rawEndpoint = ReadValue(section, "Endpoint", "ITEMIMAGES__ENDPOINT");

        return new ObjectStorageOptions
        {
            Endpoint = NormalizeEndpoint(rawEndpoint),
            BucketName = ReadValue(section, "BucketName", "ITEMIMAGES__BUCKET", section["Bucket"] ?? string.Empty),
            UseSsl = bool.TryParse(ReadValue(section, "UseSsl", "ITEMIMAGES__USESSL"), out var useSsl) && useSsl,
            AccessKey = ReadValue(section, "AccessKey", "ITEMIMAGES__ACCESSKEY"),
            SecretKey = ReadValue(section, "SecretKey", "ITEMIMAGES__SECRETKEY"),
            CacheMaxAgeSeconds = int.TryParse(ReadValue(section, "CacheMaxAgeSeconds", "ITEMIMAGES__CACHEMAXAGESECONDS"), out var ttl)
                ? ttl
                : 86400
        };
    }

    private static string ReadValue(IConfigurationSection section, string key, string envKey, string defaultValue = "")
    {
        var fromSection = section[key];
        if (!string.IsNullOrWhiteSpace(fromSection)) return fromSection;
        var fromEnv = Environment.GetEnvironmentVariable(envKey);
        return string.IsNullOrWhiteSpace(fromEnv) ? defaultValue : fromEnv;
    }

    private static string NormalizeEndpoint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var value = raw.Trim();
        if (value.Contains("://", StringComparison.Ordinal) &&
            Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return absolute.IsDefaultPort ? absolute.Host : $"{absolute.Host}:{absolute.Port}";
        }

        var slashIndex = value.IndexOf('/');
        return slashIndex >= 0 ? value[..slashIndex] : value;
    }
}
