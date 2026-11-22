namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>
/// A stock movement has one free-text Reason. An issue to production keeps three things in it:
/// the production task or order it is for, who issued it, and the note. Each part sits on its own line
/// with a fixed prefix, so the list of issues can show them apart.
/// </summary>
public static class ProductionIssueReason
{
    private const string ReferencePrefix = "Ref: ";
    private const string ByPrefix = "By: ";

    public static string? Compose(string? reference, string? issuedBy, string? notes)
    {
        var lines = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(reference)) lines.Add(ReferencePrefix + OneLine(reference));
        if (!string.IsNullOrWhiteSpace(issuedBy)) lines.Add(ByPrefix + OneLine(issuedBy));
        if (!string.IsNullOrWhiteSpace(notes)) lines.Add(notes.Trim());
        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    public static (string? Reference, string? IssuedBy, string? Notes) Parse(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (null, null, null);
        string? reference = null, issuedBy = null;
        var notes = new List<string>();
        foreach (var line in reason.Split('\n'))
        {
            if (reference is null && notes.Count == 0 && line.StartsWith(ReferencePrefix, StringComparison.Ordinal)) reference = line[ReferencePrefix.Length..].Trim();
            else if (issuedBy is null && notes.Count == 0 && line.StartsWith(ByPrefix, StringComparison.Ordinal)) issuedBy = line[ByPrefix.Length..].Trim();
            else notes.Add(line);
        }
        var text = string.Join('\n', notes).Trim();
        return (reference, issuedBy, text.Length == 0 ? null : text);
    }

    private static string OneLine(string value) => value.Trim().Replace('\r', ' ').Replace('\n', ' ');
}
