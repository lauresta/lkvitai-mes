using System.Text;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

/// <summary>
/// Decodes the body of an Agnum read response. HttpContent.ReadAsStringAsync follows the charset of the response header and,
/// when that charset is unknown to the runtime (a Windows code page), silently decodes as UTF-8, which turns every
/// Lithuanian letter into the replacement character. The body is JSON, so UTF-8 is tried first and strictly; bytes that are
/// not valid UTF-8 are read with the Windows Baltic code page.
/// </summary>
public static class AgnumResponseText
{
    private const int BalticCodePage = 1257;

    public static async Task<string> ReadAsync(HttpContent content, CancellationToken ct)
        => Decode(await content.ReadAsByteArrayAsync(ct));

    public static string Decode(byte[] bytes)
    {
        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return Baltic().GetString(bytes, offset, bytes.Length - offset);
        }
    }

    private static Encoding Baltic()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(BalticCodePage);
    }
}
