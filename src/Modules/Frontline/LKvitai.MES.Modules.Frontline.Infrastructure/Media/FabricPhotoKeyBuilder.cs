using System.Security.Cryptography;
using System.Text;

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Media;

public static class FabricPhotoKeyBuilder
{
    public const string Prefix = "fabric-photos";

    public static Guid BuildStablePhotoId(string fabricCode, string? sha256, string? originalFile)
    {
        var code = NormalizeFabricCode(fabricCode);
        var fingerprint = !string.IsNullOrWhiteSpace(sha256)
            ? sha256.Trim().ToLowerInvariant()
            : (originalFile ?? string.Empty).Trim().ToLowerInvariant();
        var input = $"{code}|{fingerprint}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes[..16]);
    }

    public static string NormalizeFabricCode(string fabricCode) => fabricCode.Trim().ToUpperInvariant();

    public static string BuildOriginalKey(string fabricCode, Guid photoId, string originalFile)
    {
        var extension = Path.GetExtension(originalFile);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        return $"{Prefix}/{NormalizeFabricCode(fabricCode)}/{ToShortId(photoId)}/original{extension}";
    }

    public static string BuildThumbKey(string fabricCode, Guid photoId)
        => $"{Prefix}/{NormalizeFabricCode(fabricCode)}/{ToShortId(photoId)}/thumb.webp";

    public static string ToShortId(Guid photoId) => photoId.ToString("N")[..8];
}
