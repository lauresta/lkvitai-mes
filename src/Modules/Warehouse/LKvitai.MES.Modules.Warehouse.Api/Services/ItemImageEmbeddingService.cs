using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface IItemImageEmbeddingService
{
    Task<float[]> ComputeEmbeddingAsync(Stream imageStream, CancellationToken cancellationToken = default);
}

public sealed class ItemImageEmbeddingService : IItemImageEmbeddingService
{
    private const int HistogramBinsPerChannel = 8;
    private const int EmbeddingLength = HistogramBinsPerChannel * HistogramBinsPerChannel * HistogramBinsPerChannel;

    public async Task<float[]> ComputeEmbeddingAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        image.Mutate(x =>
            x.AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(32, 32),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

        var pixels = new Rgba32[32 * 32];
        image.CopyPixelDataTo(pixels);

        var embedding = new float[EmbeddingLength];
        foreach (var pixel in pixels)
        {
            var alpha = pixel.A / 255f;
            var redBin = ToHistogramBin(pixel.R);
            var greenBin = ToHistogramBin(pixel.G);
            var blueBin = ToHistogramBin(pixel.B);
            var bucket = (redBin * HistogramBinsPerChannel * HistogramBinsPerChannel) +
                         (greenBin * HistogramBinsPerChannel) +
                         blueBin;
            embedding[bucket] += MathF.Max(0.05f, alpha);
        }

        NormalizeInPlace(embedding);
        return embedding;
    }

    private static int ToHistogramBin(byte channel)
        => Math.Min(HistogramBinsPerChannel - 1, channel * HistogramBinsPerChannel / 256);

    private static void NormalizeInPlace(float[] values)
    {
        var sum = 0d;
        for (var i = 0; i < values.Length; i++)
        {
            sum += values[i] * values[i];
        }

        if (sum <= 0)
        {
            return;
        }

        var magnitude = (float)Math.Sqrt(sum);
        for (var i = 0; i < values.Length; i++)
        {
            values[i] /= magnitude;
        }
    }
}
