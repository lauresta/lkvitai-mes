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

    private static async Task<MemoryStream> BuildSolidImage(Rgba32 color)
    {
        using var image = new Image<Rgba32>(32, 32, color);
        var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);
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
