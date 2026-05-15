using LKvitai.MES.BuildingBlocks.ObjectStorage;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Media;

public sealed record FabricPhotoUrls(string PhotoUrl, string ThumbnailUrl);

public sealed record FabricPhotoStreamResult(Stream Stream, string ContentType, string ETag);

public interface IFabricPhotoService
{
    int CacheMaxAgeSeconds { get; }
    Task<FabricPhotoUrls?> GetPrimaryUrlsAsync(string fabricCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, FabricPhotoUrls>> GetPrimaryUrlsAsync(
        IEnumerable<string> fabricCodes,
        CancellationToken cancellationToken = default);
    Task<FabricPhotoStreamResult?> GetPhotoAsync(
        string fabricCode,
        string size,
        CancellationToken cancellationToken = default);
}

public sealed class FabricPhotoService : IFabricPhotoService
{
    private readonly FabricPhotoDbContext _dbContext;
    private readonly IObjectStorageService _storage;

    public FabricPhotoService(FabricPhotoDbContext dbContext, IObjectStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    public int CacheMaxAgeSeconds => Math.Max(1, _storage.Options.CacheMaxAgeSeconds);

    public async Task<FabricPhotoUrls?> GetPrimaryUrlsAsync(string fabricCode, CancellationToken cancellationToken = default)
    {
        var map = await GetPrimaryUrlsAsync(new[] { fabricCode }, cancellationToken);
        return map.TryGetValue(FabricPhotoKeyBuilder.NormalizeFabricCode(fabricCode), out var urls) ? urls : null;
    }

    public async Task<IReadOnlyDictionary<string, FabricPhotoUrls>> GetPrimaryUrlsAsync(
        IEnumerable<string> fabricCodes,
        CancellationToken cancellationToken = default)
    {
        var codes = fabricCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(FabricPhotoKeyBuilder.NormalizeFabricCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (codes.Length == 0)
        {
            return new Dictionary<string, FabricPhotoUrls>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await _dbContext.FabricPhotos
            .AsNoTracking()
            .Where(x => codes.Contains(x.FabricCode))
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new { x.FabricCode })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.FabricCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => BuildUrls(x.Key),
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<FabricPhotoStreamResult?> GetPhotoAsync(
        string fabricCode,
        string size,
        CancellationToken cancellationToken = default)
    {
        var code = FabricPhotoKeyBuilder.NormalizeFabricCode(fabricCode);
        var photo = await _dbContext.FabricPhotos
            .AsNoTracking()
            .Where(x => x.FabricCode == code)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new { x.OriginalObjectKey, x.ThumbObjectKey })
            .FirstOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return null;
        }

        var useThumb = string.Equals(size, "thumb", StringComparison.OrdinalIgnoreCase);
        var objectKey = useThumb ? photo.ThumbObjectKey : photo.OriginalObjectKey;
        var etag = await _storage.TryGetObjectEtagAsync(objectKey, cancellationToken);
        if (etag is null)
        {
            return null;
        }

        var stream = await _storage.GetObjectAsync(objectKey, cancellationToken);
        return new FabricPhotoStreamResult(stream, useThumb ? "image/webp" : GuessContentType(objectKey), etag);
    }

    private static FabricPhotoUrls BuildUrls(string fabricCode)
    {
        var escaped = Uri.EscapeDataString(fabricCode);
        return new FabricPhotoUrls(
            PhotoUrl: $"/api/frontline/fabric/{escaped}/photo?size=original",
            ThumbnailUrl: $"/api/frontline/fabric/{escaped}/photo?size=thumb");
    }

    private static string GuessContentType(string objectKey)
    {
        var ext = Path.GetExtension(objectKey).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
