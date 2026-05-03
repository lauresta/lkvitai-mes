using QRCoder;

namespace LKvitai.MES.Modules.Frontline.WebUI.Services;

/// <summary>
/// Builds the inline-SVG QR code that the desktop FabricLookup sidebar
/// uses for the "Open on phone" panel and the (planned) wall-printable
/// per-fabric deep link.
///
/// Implementation choice: <see cref="SvgQRCode"/> renders a self-contained
/// vector image with no external assets, no JS, no client-side library and
/// no extra HTTP round-trip. The SVG is interpolated straight into the
/// markup via <c>MarkupString</c>, which keeps the QR crisp at any zoom
/// level and survives Blazor's prerender → interactive boundary intact.
///
/// Registered as a singleton in <c>Program.cs</c> — the underlying
/// <see cref="QRCodeGenerator"/> is documented as thread-safe so reusing
/// one instance across circuits is cheaper than per-request creation.
/// </summary>
public sealed class FabricLookupQrCodeBuilder : IDisposable
{
    private readonly QRCodeGenerator _generator = new();

    /// <summary>
    /// Renders <paramref name="payload"/> (typically an absolute URL such
    /// as <c>https://mes.lauresta.com/frontline/fabric?code=R104</c>) into
    /// a square SVG string sized to <paramref name="sizePx"/> CSS pixels.
    /// Falls back to a tiny placeholder SVG if the payload is empty so the
    /// caller never has to branch on null in the markup.
    /// </summary>
    /// <remarks>
    /// <para>ECC level <c>Q</c> (≈25% recovery) is the sweet spot for screen
    /// QRs that may be scanned through phone cameras at an angle: it tolerates
    /// glare and mild occlusion without bloating the matrix density past
    /// what looks comfortable in a 180 px sidebar tile.</para>
    /// <para>The SVG is emitted with a 2-module quiet zone, no logo and no
    /// background fill so the surrounding panel colour shows through —
    /// keeps the visual consistent with the Frontline neutral surface.</para>
    /// </remarks>
    public string BuildSvg(string payload, int sizePx = 180)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            // Same shape as a real QR but explicitly blank — easier to spot
            // an empty payload bug than rendering nothing at all.
            return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\" width=\"{sizePx}\" height=\"{sizePx}\" />";
        }

        using var data = _generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var svgQr = new SvgQRCode(data);

        // pixelsPerModule: 4 keeps the matrix sharp at the 180 px sidebar
        // size and still scans cleanly when scaled down to the inline
        // 28 px hint that lives inside the result card. QRCoder emits the
        // SVG at modules * pixelsPerModule, then we let CSS resize it via
        // the wrapping element so it stays crisp in either spot.
        return svgQr.GetGraphic(pixelsPerModule: 4);
    }

    public void Dispose() => _generator.Dispose();
}
