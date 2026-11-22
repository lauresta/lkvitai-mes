using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>
/// Decoder options for uploaded images: only JPEG, PNG and WebP are recognised.
/// ImageSharp detects the format from the bytes, not from the declared content type,
/// so without this an upload could reach the TIFF decoder (GHSA-wmxv-xphr-5c9g, BigTIFF
/// loop, fixed only in 3.2.0, which needs a commercial licence key at build time).
/// </summary>
public static class SafeImageDecoding
{
    public static DecoderOptions Options { get; } = new()
    {
        Configuration = new SixLabors.ImageSharp.Configuration(
            new JpegConfigurationModule(),
            new PngConfigurationModule(),
            new WebpConfigurationModule())
    };
}
