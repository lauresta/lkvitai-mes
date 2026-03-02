using LKvitai.MES.Modules.Warehouse.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ItemImageEmbeddingServiceTests
{
    [Fact]
    public async Task ComputeEmbeddingAsync_ForSimilarImages_ShouldProduceHigherCosineSimilarityThanDifferentImages()
    {
        var sut = new ItemImageEmbeddingService();
        await using var baseImage = BuildSolidImage(new Rgba32(20, 180, 200));
        await using var similarImage = BuildSolidImage(new Rgba32(25, 170, 205));
        await using var differentImage = BuildSolidImage(new Rgba32(210, 30, 30));

        var baseEmbedding = await sut.ComputeEmbeddingAsync(baseImage);
        var similarEmbedding = await sut.ComputeEmbeddingAsync(similarImage);
        var differentEmbedding = await sut.ComputeEmbeddingAsync(differentImage);

        var similarScore = CosineSimilarity(baseEmbedding, similarEmbedding);
        var differentScore = CosineSimilarity(baseEmbedding, differentEmbedding);

        Assert.True(similarScore > differentScore, $"Expected similar ({similarScore}) > different ({differentScore})");
    }

    [Fact]
    public async Task ComputeEmbeddingAsync_WhenColorLayoutDiffers_ShouldLowerSimilarity()
    {
        var sut = new ItemImageEmbeddingService();
        await using var reference = BuildSplitImage(left: new Rgba32(25, 25, 25), right: new Rgba32(220, 220, 220));
        await using var sameLayout = BuildSplitImage(left: new Rgba32(30, 30, 30), right: new Rgba32(215, 215, 215));
        await using var swappedLayout = BuildSplitImage(left: new Rgba32(220, 220, 220), right: new Rgba32(25, 25, 25));

        var referenceEmbedding = await sut.ComputeEmbeddingAsync(reference);
        var sameLayoutEmbedding = await sut.ComputeEmbeddingAsync(sameLayout);
        var swappedLayoutEmbedding = await sut.ComputeEmbeddingAsync(swappedLayout);

        var sameLayoutScore = CosineSimilarity(referenceEmbedding, sameLayoutEmbedding);
        var swappedLayoutScore = CosineSimilarity(referenceEmbedding, swappedLayoutEmbedding);

        Assert.True(
            sameLayoutScore > swappedLayoutScore,
            $"Expected same layout ({sameLayoutScore}) > swapped layout ({swappedLayoutScore})");
    }

    private static async Task<MemoryStream> BuildSolidImage(Rgba32 color)
    {
        using var image = new Image<Rgba32>(32, 32, color);
        var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildSplitImage(Rgba32 left, Rgba32 right)
    {
        using var image = new Image<Rgba32>(32, 32);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                image[x, y] = x < image.Width / 2 ? left : right;
            }
        }

        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        return dot;
    }
}
