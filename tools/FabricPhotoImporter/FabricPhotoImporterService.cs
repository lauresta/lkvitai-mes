using LKvitai.MES.BuildingBlocks.ObjectStorage;
using LKvitai.MES.Modules.Frontline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace FabricPhotoImporter;

public sealed record FabricPhotoImportResult(int TotalRows, int Imported, int Updated, int Skipped, int Failed);

public sealed class FabricPhotoImporterService
{
    private readonly FabricPhotoDbContext _dbContext;
    private readonly IObjectStorageService _storage;
    private readonly TextWriter _log;

    public FabricPhotoImporterService(
        FabricPhotoDbContext dbContext,
        IObjectStorageService storage,
        TextWriter? log = null)
    {
        _dbContext = dbContext;
        _storage = storage;
        _log = log ?? Console.Out;
    }

    public async Task<FabricPhotoImportResult> ImportAsync(
        string inputRoot,
        CancellationToken cancellationToken = default)
    {
        var csvPath = Path.Combine(inputRoot, "output", "fabric_photos.csv");
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException("Fabric photo CSV was not found.", csvPath);
        }

        var rows = FabricPhotoCsvReader.Read(csvPath);
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var row in rows)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(row.FabricCode) ||
                    string.IsNullOrWhiteSpace(row.OriginalFile) ||
                    string.IsNullOrWhiteSpace(row.ThumbFile))
                {
                    skipped++;
                    await _log.WriteLineAsync("Skipped row with missing FabricCode, OriginalFile, or ThumbFile.");
                    continue;
                }

                var code = FabricPhotoKeyBuilder.NormalizeFabricCode(row.FabricCode);
                var originalPath = ResolvePackagePath(inputRoot, row.OriginalFile);
                var thumbPath = ResolvePackagePath(inputRoot, row.ThumbFile);
                if (!File.Exists(originalPath) || !File.Exists(thumbPath))
                {
                    failed++;
                    await _log.WriteLineAsync($"Failed {code}: missing image file(s).");
                    continue;
                }

                var photoId = FabricPhotoKeyBuilder.BuildStablePhotoId(code, row.Sha256, row.OriginalFile);
                var originalKey = FabricPhotoKeyBuilder.BuildOriginalKey(code, photoId, row.OriginalFile);
                var thumbKey = FabricPhotoKeyBuilder.BuildThumbKey(code, photoId);

                await using (var originalStream = File.OpenRead(originalPath))
                {
                    await _storage.PutObjectAsync(
                        originalKey,
                        originalStream,
                        originalStream.Length,
                        GuessContentType(originalPath),
                        cancellationToken);
                }

                await using (var thumbStream = File.OpenRead(thumbPath))
                {
                    await _storage.PutObjectAsync(
                        thumbKey,
                        thumbStream,
                        thumbStream.Length,
                        "image/webp",
                        cancellationToken);
                }

                var existing = await FindExistingAsync(code, photoId, row.Sha256, cancellationToken);
                if (row.IsPrimary)
                {
                    await ClearOtherPrimariesAsync(code, photoId, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                if (existing is null)
                {
                    _dbContext.FabricPhotos.Add(new FabricPhoto
                    {
                        Id = photoId,
                        FabricCode = code,
                        OriginalObjectKey = originalKey,
                        ThumbObjectKey = thumbKey,
                        SourceImageUrl = NullIfWhiteSpace(row.SourceImageUrl),
                        SourcePageUrl = NullIfWhiteSpace(row.SourcePageUrl),
                        SourceImageFileName = Path.GetFileName(row.OriginalFile),
                        Sha256 = NullIfWhiteSpace(row.Sha256)?.ToLowerInvariant(),
                        ImageWidth = row.ImageWidth,
                        ImageHeight = row.ImageHeight,
                        FileSizeBytes = row.FileSizeBytes,
                        IsPrimary = row.IsPrimary,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    imported++;
                }
                else
                {
                    existing.OriginalObjectKey = originalKey;
                    existing.ThumbObjectKey = thumbKey;
                    existing.SourceImageUrl = NullIfWhiteSpace(row.SourceImageUrl);
                    existing.SourcePageUrl = NullIfWhiteSpace(row.SourcePageUrl);
                    existing.SourceImageFileName = Path.GetFileName(row.OriginalFile);
                    existing.Sha256 = NullIfWhiteSpace(row.Sha256)?.ToLowerInvariant();
                    existing.ImageWidth = row.ImageWidth;
                    existing.ImageHeight = row.ImageHeight;
                    existing.FileSizeBytes = row.FileSizeBytes;
                    existing.IsPrimary = row.IsPrimary;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                    updated++;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                await _log.WriteLineAsync($"Failed {row.FabricCode}: {ex.Message}");
                _dbContext.ChangeTracker.Clear();
            }
        }

        return new FabricPhotoImportResult(rows.Count, imported, updated, skipped, failed);
    }

    private async Task<FabricPhoto?> FindExistingAsync(
        string code,
        Guid photoId,
        string? sha256,
        CancellationToken cancellationToken)
    {
        var normalizedSha = NullIfWhiteSpace(sha256)?.ToLowerInvariant();
        return await _dbContext.FabricPhotos
            .FirstOrDefaultAsync(x =>
                    x.Id == photoId ||
                    (x.FabricCode == code && normalizedSha != null && x.Sha256 == normalizedSha),
                cancellationToken);
    }

    private async Task ClearOtherPrimariesAsync(string code, Guid photoId, CancellationToken cancellationToken)
    {
        var otherPrimaries = await _dbContext.FabricPhotos
            .Where(x => x.FabricCode == code && x.Id != photoId && x.IsPrimary)
            .ToListAsync(cancellationToken);

        foreach (var other in otherPrimaries)
        {
            other.IsPrimary = false;
            other.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string ResolvePackagePath(string inputRoot, string relativeOrAbsolute)
        => Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.GetFullPath(Path.Combine(inputRoot, relativeOrAbsolute));

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GuessContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
