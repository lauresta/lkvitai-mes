using System.Globalization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

/// <summary>Helpers for showing stored actor ids (user GUIDs / hashes) in a readable way.</summary>
public static class ActorLabel
{
    /// <summary>True when the value is a GUID or a long hex/hash-like id rather than a human-readable name.</summary>
    public static bool LooksLikeOpaqueId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (Guid.TryParse(trimmed, out _))
        {
            return true;
        }

        return trimmed.Length >= 20
            && trimmed.All(c => char.IsAsciiHexDigit(c) || c == '-' || c == '_');
    }

    /// <summary>Last six characters of an opaque id, lower-case.</summary>
    public static string Short(string value)
    {
        var trimmed = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal);
        var tail = trimmed.Length <= 6 ? trimmed : trimmed[^6..];
        return tail.ToLower(CultureInfo.InvariantCulture);
    }
}
