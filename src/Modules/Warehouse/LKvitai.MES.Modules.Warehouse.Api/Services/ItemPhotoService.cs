using System.Security.Cryptography;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface IItemPhotoService
{
    int CacheMaxAgeSeconds { get; }
    Task<ItemImageAvailability> EnsureImagesAvailableAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemPhotoDto>> ListAsync(int itemId, CancellationToken cancellationToken = default);
    Task<ItemPhotoDto> UploadAsync(
        int itemId,
        string fileName,
        string? declaredContentType,
        Stream input,
        long size,
        CancellationToken cancellationToken = default);
    Task<ItemPhotoStreamResult?> GetPhotoAsync(
        int itemId,
        Guid photoId,
        string size,
        CancellationToken cancellationToken = default);
    Task<bool> MakePrimaryAsync(int itemId, Guid photoId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int itemId, Guid photoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemImageSearchResultDto>> SearchByImageAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default);
}

public sealed record ItemPhotoDto(
    Guid Id,
    int ItemId,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    bool IsPrimary,
    string? Tags,
    string OriginalUrl,
    string ThumbUrl);

public sealed record ItemPhotoStreamResult(
    Stream Stream,
    string ContentType,
    string ETag,
    DateTimeOffset? LastModified);

public sealed record ItemImageSearchResultDto(
    int ItemId,
    string SKU,
    string Name,
    string? PrimaryThumbnailUrl,
    double Score);

public sealed class ItemPhotoService : IItemPhotoService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly WarehouseDbContext _dbContext;
    private readonly IItemImageStorageService _storageService;
    private readonly IItemImageSearchCapabilityService _capabilityService;
    private readonly IItemImageEmbeddingService _embeddingService;
    private readonly ILogger<ItemPhotoService> _logger;

    public ItemPhotoService(
        WarehouseDbContext dbContext,
        IItemImageStorageService storageService,
        IItemImageSearchCapabilityService capabilityService,
        IItemImageEmbeddingService embeddingService,
        ILogger<ItemPhotoService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _capabilityService = capabilityService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public Task<ItemImageAvailability> EnsureImagesAvailableAsync(CancellationToken cancellationToken = default)
        => _storageService.EnsureAvailableAsync(cancellationToken);

    public int CacheMaxAgeSeconds => Math.Max(1, _storageService.Options.CacheMaxAgeSeconds);

    public async Task<IReadOnlyList<ItemPhotoDto>> ListAsync(int itemId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ItemPhotos
            .AsNoTracking()
            .Where(x => x.ItemId == itemId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new ItemPhotoDto(
                x.Id,
                x.ItemId,
                x.ContentType,
                x.SizeBytes,
                x.CreatedAt,
                x.IsPrimary,
                x.Tags,
                BuildProxyUrl(itemId, x.Id, "original"),
                BuildProxyUrl(itemId, x.Id, "thumb")))
            .ToListAsync(cancellationToken);
    }

    public async Task<ItemPhotoDto> UploadAsync(
        int itemId,
        string fileName,
        string? declaredContentType,
        Stream input,
        long size,
        CancellationToken cancellationToken = default)
    {
        var availability = await _storageService.EnsureAvailableAsync(cancellationToken);
        if (!availability.IsAvailable)
        {
            throw new InvalidOperationException(availability.Reason ?? "Item image storage is unavailable.");
        }

        var itemExists = await _dbContext.Items
            .AsNoTracking()
            .AnyAsync(x => x.Id == itemId, cancellationToken);
        if (!itemExists)
        {
            throw new KeyNotFoundException($"Item with ID {itemId} does not exist.");
        }

        var maxBytes = (long)_storageService.Options.MaxUploadMb * 1024L * 1024L;
        if (size <= 0 || size > maxBytes)
        {
            throw new InvalidOperationException(
                $"Image size must be between 1 byte and {_storageService.Options.MaxUploadMb} MB.");
        }

        await using var originalBuffer = new MemoryStream();
        await input.CopyToAsync(originalBuffer, cancellationToken);

        var bytes = originalBuffer.ToArray();
        var detected = DetectImageType(bytes);
        if (detected is null)
        {
            throw new InvalidOperationException("Unsupported image content. Allowed formats: jpeg, png, webp.");
        }

        var (detectedContentType, extension) = detected.Value;
        if (!AllowedContentTypes.Contains(detectedContentType))
        {
            throw new InvalidOperationException("Unsupported image content type.");
        }

        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            !string.Equals(declaredContentType, detectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Declared content type does not match file content.");
        }

        await using var thumbStream = await BuildThumbnailAsync(bytes, cancellationToken);

        var photoId = Guid.NewGuid();
        var baseKey = $"{ItemImageOptions.ObjectKeyPrefix}/{itemId}/{photoId:N}";
        var originalKey = $"{baseKey}/original.{extension}";
        var thumbKey = $"{baseKey}/thumb.webp";

        await using var uploadOriginalStream = new MemoryStream(bytes, writable: false);
        await _storageService.PutObjectAsync(
            originalKey,
            uploadOriginalStream,
            uploadOriginalStream.Length,
            detectedContentType,
            cancellationToken);

        thumbStream.Position = 0;
        await _storageService.PutObjectAsync(
            thumbKey,
            thumbStream,
            thumbStream.Length,
            "image/webp",
            cancellationToken);

        var hasPrimary = await _dbContext.ItemPhotos
            .AsNoTracking()
            .AnyAsync(x => x.ItemId == itemId && x.IsPrimary, cancellationToken);

        var entity = new ItemPhoto
        {
            Id = photoId,
            ItemId = itemId,
            OriginalKey = originalKey,
            ThumbKey = thumbKey,
            ContentType = detectedContentType,
            SizeBytes = bytes.LongLength,
            CreatedAt = DateTimeOffset.UtcNow,
            IsPrimary = !hasPrimary,
            Tags = Path.GetFileName(fileName)
        };

        _dbContext.ItemPhotos.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var capability = await _capabilityService.GetCapabilityAsync(cancellationToken);
        if (capability.IsEnabled)
        {
            await using var forEmbedding = new MemoryStream(bytes, writable: false);
            var embedding = await _embeddingService.ComputeEmbeddingAsync(forEmbedding, cancellationToken);
            var embeddingLiteral = BuildQueryEmbeddingString(embedding);
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE public.item_photos
                   SET ""ImageEmbedding"" = CAST({embeddingLiteral} AS vector)
                   WHERE ""Id"" = {entity.Id}",
                cancellationToken);
        }

        return new ItemPhotoDto(
            entity.Id,
            entity.ItemId,
            entity.ContentType,
            entity.SizeBytes,
            entity.CreatedAt,
            entity.IsPrimary,
            entity.Tags,
            BuildProxyUrl(itemId, entity.Id, "original"),
            BuildProxyUrl(itemId, entity.Id, "thumb"));
    }

    public async Task<ItemPhotoStreamResult?> GetPhotoAsync(
        int itemId,
        Guid photoId,
        string size,
        CancellationToken cancellationToken = default)
    {
        var photo = await _dbContext.ItemPhotos
            .AsNoTracking()
            .Where(x => x.ItemId == itemId && x.Id == photoId)
            .Select(x => new
            {
                x.OriginalKey,
                x.ThumbKey,
                x.ContentType
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return null;
        }

        var useThumb = string.Equals(size, "thumb", StringComparison.OrdinalIgnoreCase);
        var objectKey = useThumb ? photo.ThumbKey : photo.OriginalKey;
        var contentType = useThumb ? "image/webp" : photo.ContentType;

        var etag = await _storageService.TryGetObjectEtagAsync(objectKey, cancellationToken);
        if (etag is null)
        {
            return null;
        }

        var stream = await _storageService.GetObjectAsync(objectKey, cancellationToken);
        return new ItemPhotoStreamResult(stream, contentType, etag, null);
    }

    public async Task<bool> MakePrimaryAsync(int itemId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photos = await _dbContext.ItemPhotos
            .Where(x => x.ItemId == itemId)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0 || photos.All(x => x.Id != photoId))
        {
            return false;
        }

        foreach (var photo in photos)
        {
            photo.IsPrimary = photo.Id == photoId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ItemImageSearchResultDto>> SearchByImageAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        var capability = await _capabilityService.GetCapabilityAsync(cancellationToken);
        if (!capability.IsEnabled)
        {
            throw new InvalidOperationException(capability.DisabledReason ?? "Image search is unavailable.");
        }

        var embedding = await _embeddingService.ComputeEmbeddingAsync(imageStream, cancellationToken);
        var embeddingLiteral = BuildQueryEmbeddingString(embedding);

        var results = new List<ItemImageSearchResultDto>();
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT i."Id",
                   i."InternalSKU",
                   i."Name",
                   pp."Id" AS "PrimaryPhotoId",
                   1.0 - (p."ImageEmbedding" <=> CAST(@embedding AS vector)) AS "Score"
            FROM public.item_photos p
            JOIN public.items i ON i."Id" = p."ItemId"
            LEFT JOIN public.item_photos pp ON pp."ItemId" = i."Id" AND pp."IsPrimary" = true
            WHERE p."ImageEmbedding" IS NOT NULL
            ORDER BY p."ImageEmbedding" <=> CAST(@embedding AS vector)
            LIMIT 20;
            """;

        var embeddingParam = command.CreateParameter();
        embeddingParam.ParameterName = "embedding";
        embeddingParam.Value = embeddingLiteral;
        if (embeddingParam is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;
        }

        command.Parameters.Add(embeddingParam);

        var perItem = new Dictionary<int, ItemImageSearchResultDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemId = reader.GetInt32(0);
            var sku = reader.GetString(1);
            var name = reader.GetString(2);
            var primaryPhotoId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);
            var score = reader.IsDBNull(4) ? 0d : reader.GetDouble(4);

            var candidate = new ItemImageSearchResultDto(
                itemId,
                sku,
                name,
                primaryPhotoId.HasValue
                    ? BuildProxyUrl(itemId, primaryPhotoId.Value, "thumb")
                    : null,
                score);

            if (perItem.TryGetValue(itemId, out var existing))
            {
                if (candidate.Score > existing.Score)
                {
                    perItem[itemId] = candidate;
                }
            }
            else
            {
                perItem[itemId] = candidate;
            }
        }

        results.AddRange(perItem.Values.OrderByDescending(x => x.Score).Take(20));
        return results;
    }

    public async Task<bool> DeleteAsync(int itemId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await _dbContext.ItemPhotos
            .FirstOrDefaultAsync(x => x.ItemId == itemId && x.Id == photoId, cancellationToken);
        if (photo is null)
        {
            return false;
        }

        _dbContext.ItemPhotos.Remove(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _storageService.DeleteObjectAsync(photo.OriginalKey, cancellationToken);
            await _storageService.DeleteObjectAsync(photo.ThumbKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete object(s) for item photo {PhotoId}.", photoId);
        }

        if (photo.IsPrimary)
        {
            var fallback = await _dbContext.ItemPhotos
                .Where(x => x.ItemId == itemId)
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (fallback is not null)
            {
                fallback.IsPrimary = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return true;
    }

    public static string BuildProxyUrl(int itemId, Guid photoId, string size)
        => $"/api/warehouse/v1/items/{itemId}/photos/{photoId}?size={size}";

    private static (string ContentType, string Extension)? DetectImageType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xD8 &&
            bytes[2] == 0xFF)
        {
            return ("image/jpeg", "jpg");
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A)
        {
            return ("image/png", "png");
        }

        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' &&
            bytes[1] == (byte)'I' &&
            bytes[2] == (byte)'F' &&
            bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' &&
            bytes[9] == (byte)'E' &&
            bytes[10] == (byte)'B' &&
            bytes[11] == (byte)'P')
        {
            return ("image/webp", "webp");
        }

        return null;
    }

    private static async Task<MemoryStream> BuildThumbnailAsync(byte[] originalBytes, CancellationToken cancellationToken)
    {
        await using var source = new MemoryStream(originalBytes, writable: false);
        using var image = await Image.LoadAsync(source, cancellationToken);
        image.Mutate(x =>
            x.AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(200, 200),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

        var thumbStream = new MemoryStream();
        await image.SaveAsWebpAsync(
            thumbStream,
            new WebpEncoder { Quality = 80 },
            cancellationToken);
        thumbStream.Position = 0;
        return thumbStream;
    }

    public static string BuildQueryEmbeddingString(ReadOnlySpan<float> values)
    {
        var parts = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            parts[i] = values[i].ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        return $"[{string.Join(',', parts)}]";
    }

    public static string ComputeDeterministicEtag(byte[] payload)
    {
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
