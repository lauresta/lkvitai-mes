using System.Text.RegularExpressions;

namespace LKvitai.MES.Api.Security;

public static partial class SensitiveDataMasker
{
    [GeneratedRegex("(?i)(password|passwd|pwd)\\s*[:=]\\s*[^,;\\s]+")]
    private static partial Regex PasswordPattern();

    [GeneratedRegex("(?i)(api[_-]?key|token|authorization)\\s*[:=]\\s*[^,;\\s]+")]
    private static partial Regex SecretPattern();

    [GeneratedRegex("(?i)([A-Z0-9._%+-]+)@([A-Z0-9.-]+\\.[A-Z]{2,})")]
    private static partial Regex EmailPattern();

    [GeneratedRegex("\\b(\\+?\\d[\\d\\- ()]{7,}\\d)\\b")]
    private static partial Regex PhonePattern();

    public static string MaskText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var masked = PasswordPattern().Replace(value, "$1=***");
        masked = SecretPattern().Replace(masked, "$1=***");
        masked = EmailPattern().Replace(masked, "***@$2");
        masked = PhonePattern().Replace(masked, "***");
        return masked;
    }

    public static string MaskToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        if (token.Length <= 8)
        {
            return "***";
        }

        return $"{token[..4]}...{token[^4..]}";
    }
}
