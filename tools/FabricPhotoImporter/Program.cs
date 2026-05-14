using FabricPhotoImporter;
using LKvitai.MES.BuildingBlocks.ObjectStorage;
using LKvitai.MES.Modules.Frontline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Minio;

var input = ReadArg(args, "--input");
if (string.IsNullOrWhiteSpace(input))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/FabricPhotoImporter -- --input <fabric-image-import-folder>");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__WarehouseDb")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__FabricPhotosDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Missing ConnectionStrings__WarehouseDb or ConnectionStrings__FabricPhotosDb.");
    return 2;
}

var storageOptions = ReadStorageOptions();
if (!storageOptions.HasRequiredConfiguration)
{
    Console.Error.WriteLine("Missing ITEMIMAGES__ENDPOINT, ITEMIMAGES__BUCKET, ITEMIMAGES__ACCESSKEY, or ITEMIMAGES__SECRETKEY.");
    return 2;
}

var dbOptions = new DbContextOptionsBuilder<FabricPhotoDbContext>()
    .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
    .Options;

await using var dbContext = new FabricPhotoDbContext(dbOptions);
await dbContext.Database.MigrateAsync();

var minio = new MinioClient()
    .WithEndpoint(storageOptions.Endpoint)
    .WithCredentials(storageOptions.AccessKey, storageOptions.SecretKey)
    .WithSSL(storageOptions.UseSsl)
    .Build();

var storage = new MinioObjectStorageService(
    minio,
    storageOptions,
    NullLogger<MinioObjectStorageService>.Instance);

var availability = await storage.EnsureAvailableAsync();
if (!availability.IsAvailable)
{
    Console.Error.WriteLine($"Object storage unavailable: {availability.Reason}");
    return 2;
}

var importer = new FabricPhotoImporterService(dbContext, storage);
var result = await importer.ImportAsync(Path.GetFullPath(input));
Console.WriteLine($"Rows={result.TotalRows}, imported={result.Imported}, updated={result.Updated}, skipped={result.Skipped}, failed={result.Failed}");
return result.Failed == 0 ? 0 : 1;

static string? ReadArg(string[] args, string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return null;
}

static ObjectStorageOptions ReadStorageOptions()
{
    return new ObjectStorageOptions
    {
        Endpoint = NormalizeEndpoint(Environment.GetEnvironmentVariable("ITEMIMAGES__ENDPOINT") ?? string.Empty),
        BucketName = Environment.GetEnvironmentVariable("ITEMIMAGES__BUCKET") ?? string.Empty,
        AccessKey = Environment.GetEnvironmentVariable("ITEMIMAGES__ACCESSKEY") ?? string.Empty,
        SecretKey = Environment.GetEnvironmentVariable("ITEMIMAGES__SECRETKEY") ?? string.Empty,
        UseSsl = bool.TryParse(Environment.GetEnvironmentVariable("ITEMIMAGES__USESSL"), out var useSsl) && useSsl,
        CacheMaxAgeSeconds = int.TryParse(Environment.GetEnvironmentVariable("ITEMIMAGES__CACHEMAXAGESECONDS"), out var ttl)
            ? ttl
            : 86400
    };
}

static string NormalizeEndpoint(string raw)
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
