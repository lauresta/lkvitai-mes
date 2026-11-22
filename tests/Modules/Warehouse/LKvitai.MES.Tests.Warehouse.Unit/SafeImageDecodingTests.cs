using LKvitai.MES.Modules.Warehouse.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class SafeImageDecodingTests
{
    [Fact]
    public async Task Tiff_data_is_rejected_before_the_tiff_decoder_runs()
    {
        using var tiff = new MemoryStream();
        using (var source = new Image<Rgba32>(2, 2)) await source.SaveAsTiffAsync(tiff);
        tiff.Position = 0;
        await Assert.ThrowsAsync<UnknownImageFormatException>(() => Image.LoadAsync(SafeImageDecoding.Options, tiff));
    }

    [Fact]
    public async Task Png_jpeg_and_webp_still_decode()
    {
        foreach (var save in new Func<Image, Stream, Task>[] { (i, s) => i.SaveAsPngAsync(s), (i, s) => i.SaveAsJpegAsync(s), (i, s) => i.SaveAsWebpAsync(s) })
        {
            using var data = new MemoryStream();
            using (var source = new Image<Rgba32>(2, 2)) await save(source, data);
            data.Position = 0;
            using var image = await Image.LoadAsync(SafeImageDecoding.Options, data);
            Assert.Equal(2, image.Width);
        }
    }
}
