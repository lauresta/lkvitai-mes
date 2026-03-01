using System.Security.Cryptography;
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

        var bytes = new byte[pixels.Length * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 4;
            bytes[offset] = pixels[i].R;
            bytes[offset + 1] = pixels[i].G;
            bytes[offset + 2] = pixels[i].B;
            bytes[offset + 3] = pixels[i].A;
        }

        var digest = SHA512.HashData(bytes);
        var embedding = new float[512];
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (digest[i % digest.Length] / 255f) * 2f - 1f;
        }

        NormalizeInPlace(embedding);
        return embedding;
    }

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
