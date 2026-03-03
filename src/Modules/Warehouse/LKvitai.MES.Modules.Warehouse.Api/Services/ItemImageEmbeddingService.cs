using System.Collections.Concurrent;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
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
    private const int ClipImageSize = 224;
    private const int ClipEmbeddingLength = 512;
    private static readonly float[] ClipMean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] ClipStd = [0.26862954f, 0.26130258f, 0.27577711f];
    private static readonly ConcurrentDictionary<string, InferenceSession> SessionCache = new(StringComparer.Ordinal);

    private const int HistogramBinsRed = 4;
    private const int HistogramBinsGreen = 4;
    private const int HistogramBinsBlue = 8;
    private const int SpatialCellsX = 2;
    private const int SpatialCellsY = 2;
    private const int HistogramBinsPerCell = HistogramBinsRed * HistogramBinsGreen * HistogramBinsBlue;
    private const int HistogramEmbeddingLength = SpatialCellsX * SpatialCellsY * HistogramBinsPerCell;

    private readonly IItemImageStorageService? _storageService;
    private readonly ILogger<ItemImageEmbeddingService>? _logger;

    public ItemImageEmbeddingService(
        IItemImageStorageService? storageService = null,
        ILogger<ItemImageEmbeddingService>? logger = null)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<float[]> ComputeEmbeddingAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        await using var copy = new MemoryStream();
        await imageStream.CopyToAsync(copy, cancellationToken);
        var bytes = copy.ToArray();

        var modelPath = _storageService?.Options.ModelPath?.Trim();
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            try
            {
                var clipEmbedding = await ComputeClipEmbeddingAsync(modelPath, bytes, cancellationToken);
                NormalizeInPlace(clipEmbedding);
                return clipEmbedding;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "CLIP embedding inference failed. Falling back to histogram embedding.");
            }
        }

        return await ComputeHistogramEmbeddingAsync(bytes, cancellationToken);
    }

    private static async Task<float[]> ComputeClipEmbeddingAsync(
        string modelPath,
        byte[] imageBytes,
        CancellationToken cancellationToken)
    {
        await using var clipStream = new MemoryStream(imageBytes, writable: false);
        using var image = await Image.LoadAsync<Rgb24>(clipStream, cancellationToken);
        image.Mutate(x =>
            x.AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(ClipImageSize, ClipImageSize),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

        var inputData = new float[3 * ClipImageSize * ClipImageSize];
        var area = ClipImageSize * ClipImageSize;
        for (var y = 0; y < ClipImageSize; y++)
        {
            for (var x = 0; x < ClipImageSize; x++)
            {
                var pixel = image[x, y];
                var idx = (y * ClipImageSize) + x;

                inputData[idx] = (pixel.R / 255f - ClipMean[0]) / ClipStd[0];
                inputData[area + idx] = (pixel.G / 255f - ClipMean[1]) / ClipStd[1];
                inputData[(2 * area) + idx] = (pixel.B / 255f - ClipMean[2]) / ClipStd[2];
            }
        }

        var session = SessionCache.GetOrAdd(
            modelPath,
            static path => new InferenceSession(path));

        var inputName = session.InputMetadata.Keys.First();
        var outputName = session.OutputMetadata.Keys.First();
        var tensor = new DenseTensor<float>(inputData, [1, 3, ClipImageSize, ClipImageSize]);
        var input = NamedOnnxValue.CreateFromTensor(inputName, tensor);
        using var results = session.Run([input], [outputName]);

        var outputTensor = results.First().AsTensor<float>();
        var values = outputTensor.ToArray();
        if (values.Length != ClipEmbeddingLength)
        {
            throw new InvalidOperationException(
                $"Unsupported CLIP output length {values.Length}. Expected {ClipEmbeddingLength}.");
        }

        return values;
    }

    private static async Task<float[]> ComputeHistogramEmbeddingAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken)
    {
        await using var histogramStream = new MemoryStream(imageBytes, writable: false);
        using var image = await Image.LoadAsync<Rgba32>(histogramStream, cancellationToken);
        image.Mutate(x =>
            x.AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(32, 32),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

        var embedding = new float[HistogramEmbeddingLength];
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
