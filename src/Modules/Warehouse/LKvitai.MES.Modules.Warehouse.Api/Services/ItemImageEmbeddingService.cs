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
    private const int HistogramBinsRed = 4;
    private const int HistogramBinsGreen = 4;
    private const int HistogramBinsBlue = 8;
    private const int SpatialCellsX = 2;
    private const int SpatialCellsY = 2;
    private const int HistogramBinsPerCell = HistogramBinsRed * HistogramBinsGreen * HistogramBinsBlue;
    private const int EmbeddingLength = SpatialCellsX * SpatialCellsY * HistogramBinsPerCell;

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

        var embedding = new float[EmbeddingLength];
        var width = image.Width;
        var height = image.Height;
        var cellWidth = Math.Max(1, width / SpatialCellsX);
        var cellHeight = Math.Max(1, height / SpatialCellsY);

        for (var y = 0; y < height; y++)
        {
            var cellY = Math.Min(SpatialCellsY - 1, y / cellHeight);
            for (var x = 0; x < width; x++)
            {
                var cellX = Math.Min(SpatialCellsX - 1, x / cellWidth);
                var pixel = image[x, y];
                var alpha = pixel.A / 255f;

                var redBin = ToHistogramBin(pixel.R, HistogramBinsRed);
                var greenBin = ToHistogramBin(pixel.G, HistogramBinsGreen);
                var blueBin = ToHistogramBin(pixel.B, HistogramBinsBlue);

                var cellIndex = (cellY * SpatialCellsX) + cellX;
                var localBucket = (redBin * HistogramBinsGreen * HistogramBinsBlue) +
                                  (greenBin * HistogramBinsBlue) +
                                  blueBin;
                var globalBucket = (cellIndex * HistogramBinsPerCell) + localBucket;

                embedding[globalBucket] += MathF.Max(0.05f, alpha);
            }
        }

        NormalizeInPlace(embedding);
        return embedding;
    }

    private static int ToHistogramBin(byte channel, int bins)
        => Math.Min(bins - 1, channel * bins / 256);

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
