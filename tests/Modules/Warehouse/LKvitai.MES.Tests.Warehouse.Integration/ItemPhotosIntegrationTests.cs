using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class ItemPhotosIntegrationTests
{
    [Fact]
    public async Task Upload_Get_Delete_Photo_Flow_ShouldWork()
    {
        await using var db = CreateDbContext();
        await SeedMinimalItemAsync(db, 1001);

        var storage = new InMemoryItemImageStorageService();
        var capability = new ItemImageSearchCapabilityService(
            storage,
            db,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<ItemImageSearchCapabilityService>());
        var photoService = new ItemPhotoService(
            db,
            storage,
            capability,
            new ItemImageEmbeddingService(),
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<ItemPhotoService>());

        var controller = new ItemsController(db, new StubSkuGenerationService(), photoService, capability)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var upload = await controller.UploadPhotoAsync(1001, BuildFormFile("a.png", BuildPngBytes(), "image/png"));
        var uploadOk = upload.Should().BeOfType<OkObjectResult>().Subject;
        var uploaded = uploadOk.Value.Should().BeOfType<ItemPhotoDto>().Subject;

        var list = await controller.ListPhotosAsync(1001);
        var listOk = list.Should().BeOfType<OkObjectResult>().Subject;
        var listPayload = listOk.Value.Should().BeOfType<ItemsController.ItemPhotosResponse>().Subject;
        listPayload.Photos.Should().ContainSingle();

        var get = await controller.GetPhotoAsync(1001, uploaded.Id, "thumb");
        get.Should().BeOfType<FileStreamResult>();
        controller.Response.Headers.Should().ContainKey("ETag");
        controller.Response.Headers.Should().ContainKey("Cache-Control");

        var ifNoneMatch = controller.Response.Headers.ETag.ToString();
        controller.ControllerContext.HttpContext = new DefaultHttpContext();
        controller.ControllerContext.HttpContext.Request.Headers.IfNoneMatch = ifNoneMatch;
        var notModified = await controller.GetPhotoAsync(1001, uploaded.Id, "thumb");
        var notModifiedResult = notModified.Should().BeOfType<StatusCodeResult>().Subject;
        notModifiedResult.StatusCode.Should().Be(StatusCodes.Status304NotModified);

        var delete = await controller.DeletePhotoAsync(1001, uploaded.Id);
        delete.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SearchByImage_WhenCapabilityMissing_ShouldReturn503()
    {
        await using var db = CreateDbContext();
        await SeedMinimalItemAsync(db, 1002);

        var storage = new InMemoryItemImageStorageService();
        var capability = new ItemImageSearchCapabilityService(
            storage,
            db,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<ItemImageSearchCapabilityService>());
        var photoService = new ItemPhotoService(
            db,
            storage,
            capability,
            new ItemImageEmbeddingService(),
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<ItemPhotoService>());

        var controller = new ItemsController(db, new StubSkuGenerationService(), photoService, capability)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SearchByImageAsync(BuildFormFile("query.png", BuildPngBytes(), "image/png"));
        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"item-photos-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }

    private static async Task SeedMinimalItemAsync(WarehouseDbContext db, int itemId)
    {
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "RAW", Name = "Raw" });
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        db.Items.Add(new Item
        {
            Id = itemId,
            InternalSKU = $"SKU-{itemId}",
            Name = $"Item-{itemId}",
            CategoryId = 1,
            BaseUoM = "PCS",
            Status = "Active"
        });
        await db.SaveChangesAsync();
    }

    private static IFormFile BuildFormFile(string fileName, byte[] bytes, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] BuildPngBytes()
    {
        using var image = new Image<Rgba32>(1, 1, new Rgba32(255, 0, 0));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private sealed class StubSkuGenerationService : ISkuGenerationService
    {
        public Task<string> GenerateNextSkuAsync(int categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult($"SKU-{categoryId:D4}");
    }

    private sealed class InMemoryItemImageStorageService : IItemImageStorageService
    {
        private readonly Dictionary<string, (byte[] Bytes, string ContentType, string ETag)> _store = new(StringComparer.Ordinal);

        public ItemImageOptions Options { get; } = new()
        {
            Endpoint = "localhost:9000",
            BucketName = "lkvitai-test",
            AccessKey = "x",
            SecretKey = "y",
            MaxUploadMb = 5,
            CacheMaxAgeSeconds = 86400,
            ModelPath = string.Empty
        };

        public Task<ItemImageAvailability> EnsureAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ItemImageAvailability(true, null));

        public Task PutObjectAsync(string objectKey, Stream stream, long size, string contentType, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            var etag = ItemPhotoService.ComputeDeterministicEtag(bytes);
            _store[objectKey] = (bytes, contentType, etag);
            return Task.CompletedTask;
        }

        public Task<MemoryStream> GetObjectAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            if (!_store.TryGetValue(objectKey, out var value))
            {
                throw new FileNotFoundException(objectKey);
            }

            return Task.FromResult(new MemoryStream(value.Bytes, writable: false));
        }

        public Task<string?> TryGetObjectEtagAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.TryGetValue(objectKey, out var value) ? value.ETag : null);
        }

        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            _store.Remove(objectKey);
            return Task.CompletedTask;
        }
    }
}
