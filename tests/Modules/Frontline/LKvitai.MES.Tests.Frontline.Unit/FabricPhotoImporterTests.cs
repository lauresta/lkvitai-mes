using FabricPhotoImporter;
using FluentAssertions;
using LKvitai.MES.BuildingBlocks.ObjectStorage;
using LKvitai.MES.Modules.Frontline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Frontline.Unit;

public class FabricPhotoImporterTests
{
    [Fact]
    public void CsvReader_ReadsPreparedPackageShape()
    {
        using var fixture = FabricImportFixture.Create();

        var rows = FabricPhotoCsvReader.Read(fixture.CsvPath);

        rows.Should().ContainSingle();
        rows[0].FabricCode.Should().Be("R85");
        rows[0].SourceImageUrl.Should().Be("https://example.test/R85.JPG");
        rows[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void KeyBuilder_UsesExpectedFabricPhotoPrefix()
    {
        var id = FabricPhotoKeyBuilder.BuildStablePhotoId("r85", "ABCDEF", "images/original/R85.JPG");

        FabricPhotoKeyBuilder.BuildOriginalKey("r85", id, "images/original/R85.JPG")
            .Should().Be($"fabric-photos/R85/{id:N}"[..26] + $"/original.JPG");
        FabricPhotoKeyBuilder.BuildThumbKey("r85", id)
            .Should().Be($"fabric-photos/R85/{id:N}"[..26] + $"/thumb.webp");
    }

    [Fact]
    public async Task ImportAsync_WhenRunTwice_UpdatesExistingFabricPhoto()
    {
        using var fixture = FabricImportFixture.Create();
        await using var db = CreateDbContext();
        var storage = new FakeObjectStorageService();
        var importer = new FabricPhotoImporterService(db, storage, TextWriter.Null);

        var first = await importer.ImportAsync(fixture.Root);
        var second = await importer.ImportAsync(fixture.Root);

        first.Imported.Should().Be(1);
        second.Updated.Should().Be(1);
        db.FabricPhotos.Should().ContainSingle(x => x.FabricCode == "R85");
        storage.UploadedKeys.Should().Contain(key => key.StartsWith("fabric-photos/R85/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_WhenPrimaryDuplicateIsFollowedByNonPrimary_KeepsPrimary()
    {
        using var fixture = FabricImportFixture.CreateWithDuplicateRows(primaryFirst: true);
        await using var db = CreateDbContext();
        var importer = new FabricPhotoImporterService(db, new FakeObjectStorageService(), TextWriter.Null);

        var result = await importer.ImportAsync(fixture.Root);

        result.Imported.Should().Be(1);
        result.Updated.Should().Be(1);
        db.FabricPhotos.Should().ContainSingle(x =>
            x.FabricCode == "R416" &&
            x.Sha256 == FabricImportFixture.DuplicateSha &&
            x.IsPrimary);
    }

    [Fact]
    public async Task ImportAsync_WhenNonPrimaryDuplicateIsFollowedByPrimary_PromotesToPrimary()
    {
        using var fixture = FabricImportFixture.CreateWithDuplicateRows(primaryFirst: false);
        await using var db = CreateDbContext();
        var importer = new FabricPhotoImporterService(db, new FakeObjectStorageService(), TextWriter.Null);

        var result = await importer.ImportAsync(fixture.Root);

        result.Imported.Should().Be(1);
        result.Updated.Should().Be(1);
        db.FabricPhotos.Should().ContainSingle(x =>
            x.FabricCode == "R416" &&
            x.Sha256 == FabricImportFixture.DuplicateSha &&
            x.IsPrimary);
    }

    [Fact]
    public async Task ImportAsync_WhenDuplicatePackageIsRunRepeatedly_RemainsIdempotent()
    {
        using var fixture = FabricImportFixture.CreateWithDuplicateRows(primaryFirst: true);
        await using var db = CreateDbContext();
        var importer = new FabricPhotoImporterService(db, new FakeObjectStorageService(), TextWriter.Null);

        await importer.ImportAsync(fixture.Root);
        var second = await importer.ImportAsync(fixture.Root);

        second.Imported.Should().Be(0);
        second.Updated.Should().Be(2);
        db.FabricPhotos.Should().ContainSingle(x =>
            x.FabricCode == "R416" &&
            x.Sha256 == FabricImportFixture.DuplicateSha &&
            x.IsPrimary);
    }

    [Fact]
    public async Task FabricPhotoService_MapsFabricCodeToPhotoUrlsWithoutWarehouseItemPhoto()
    {
        await using var db = CreateDbContext();
        var photoId = FabricPhotoKeyBuilder.BuildStablePhotoId("R85", "abc", "R85.JPG");
        db.FabricPhotos.Add(new FabricPhoto
        {
            Id = photoId,
            FabricCode = "R85",
            OriginalObjectKey = FabricPhotoKeyBuilder.BuildOriginalKey("R85", photoId, "R85.JPG"),
            ThumbObjectKey = FabricPhotoKeyBuilder.BuildThumbKey("R85", photoId),
            Sha256 = "abc",
            IsPrimary = true
        });
        await db.SaveChangesAsync();

        var sut = new FabricPhotoService(db, new FakeObjectStorageService());

        var urls = await sut.GetPrimaryUrlsAsync("R85");

        urls.Should().NotBeNull();
        urls!.ThumbnailUrl.Should().Be("/api/frontline/fabric/R85/photo?size=thumb");
        urls.PhotoUrl.Should().Be("/api/frontline/fabric/R85/photo?size=original");
    }

    private static FabricPhotoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FabricPhotoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new FabricPhotoDbContext(options);
    }
}

internal sealed class FakeObjectStorageService : IObjectStorageService
{
    private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public ObjectStorageOptions Options { get; } = new()
    {
        Endpoint = "localhost:9000",
        BucketName = "test",
        AccessKey = "test",
        SecretKey = "test",
        CacheMaxAgeSeconds = 60
    };

    public List<string> UploadedKeys { get; } = new();

    public Task<ObjectStorageAvailability> EnsureAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ObjectStorageAvailability(true, null));

    public async Task PutObjectAsync(
        string objectKey,
        Stream stream,
        long size,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        _objects[objectKey] = buffer.ToArray();
        UploadedKeys.Add(objectKey);
    }

    public Task<MemoryStream> GetObjectAsync(string objectKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new MemoryStream(_objects.GetValueOrDefault(objectKey, Array.Empty<byte>())));

    public Task<string?> TryGetObjectEtagAsync(string objectKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_objects.ContainsKey(objectKey) ? "etag" : null);

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _objects.Remove(objectKey);
        return Task.CompletedTask;
    }
}

internal sealed class FabricImportFixture : IDisposable
{
    public const string DuplicateSha = "c54f7dbf2179cc9fe2624bb43d69a181f80e6cf6516af6b7b5cde80f4c5fceaa";

    private FabricImportFixture(string root, string csvPath)
    {
        Root = root;
        CsvPath = csvPath;
    }

    public string Root { get; }
    public string CsvPath { get; }

    public static FabricImportFixture Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "fabric-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "output"));
        Directory.CreateDirectory(Path.Combine(root, "images", "original"));
        Directory.CreateDirectory(Path.Combine(root, "images", "thumb"));

        File.WriteAllBytes(Path.Combine(root, "images", "original", "R85.JPG"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(root, "images", "thumb", "R85.webp"), new byte[] { 4, 5, 6 });

        var csvPath = Path.Combine(root, "output", "fabric_photos.csv");
        File.WriteAllText(
            csvPath,
            """
            FabricCode,FabricGroup,SourceImageUrl,SourcePageUrl,OriginalFile,ThumbFile,ImageWidth,ImageHeight,FileSizeBytes,Sha256,IsPrimary,Notes
            R85,Group,https://example.test/R85.JPG,https://example.test/page,images/original/R85.JPG,images/thumb/R85.webp,10,12,3,abcdef,true,
            """);

        return new FabricImportFixture(root, csvPath);
    }

    public static FabricImportFixture CreateWithDuplicateRows(bool primaryFirst)
    {
        var root = Path.Combine(Path.GetTempPath(), "fabric-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "output"));
        Directory.CreateDirectory(Path.Combine(root, "images", "original"));
        Directory.CreateDirectory(Path.Combine(root, "images", "thumb"));

        File.WriteAllBytes(Path.Combine(root, "images", "original", "R416_A.JPG"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(root, "images", "original", "R416_B.JPG"), new byte[] { 4, 5, 6 });
        File.WriteAllBytes(Path.Combine(root, "images", "thumb", "R416.webp"), new byte[] { 7, 8, 9 });
        File.WriteAllBytes(Path.Combine(root, "images", "thumb", "R416_2.webp"), new byte[] { 10, 11, 12 });

        var primaryRow =
            $"R416,Group,https://example.test/R416_A.JPG,https://example.test/page,images/original/R416_A.JPG,images/thumb/R416.webp,10,12,3,{DuplicateSha},true,";
        var nonPrimaryRow =
            $"R416,Group,https://example.test/R416_B.JPG,https://example.test/page,images/original/R416_B.JPG,images/thumb/R416_2.webp,10,12,3,{DuplicateSha},false,";
        var rows = primaryFirst
            ? new[] { primaryRow, nonPrimaryRow }
            : new[] { nonPrimaryRow, primaryRow };

        var csvPath = Path.Combine(root, "output", "fabric_photos.csv");
        File.WriteAllText(
            csvPath,
            "FabricCode,FabricGroup,SourceImageUrl,SourcePageUrl,OriginalFile,ThumbFile,ImageWidth,ImageHeight,FileSizeBytes,Sha256,IsPrimary,Notes" +
            Environment.NewLine +
            string.Join(Environment.NewLine, rows) +
            Environment.NewLine);

        return new FabricImportFixture(root, csvPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
