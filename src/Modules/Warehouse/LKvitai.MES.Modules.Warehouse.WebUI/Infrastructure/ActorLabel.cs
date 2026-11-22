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

        if (trimmed.Length >= 20 && trimmed.All(c => char.IsAsciiHexDigit(c) || c == '-' || c == '_'))
        {
            return true;
        }

        // Subject ids of other providers (base64url style): long, one token, letters and at least one digit.
        return trimmed.Length >= 24
            && trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')
            && trimmed.Any(char.IsAsciiDigit);
    }

    /// <summary>The actor as shown in a table: a readable name as stored, an opaque id shortened through <paramref name="unknown"/>.</summary>
    public static string Display(string? stored, Func<string, string> unknown)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return "-";
        }

        var value = stored.Trim();
        return LooksLikeOpaqueId(value) ? unknown(Short(value)) : value;
    }

    /// <summary>Last six characters of an opaque id, lower-case.</summary>
    public static string Short(string value)
    {
        var trimmed = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal);
        var tail = trimmed.Length <= 6 ? trimmed : trimmed[^6..];
        return tail.ToLower(CultureInfo.InvariantCulture);
    }
}
