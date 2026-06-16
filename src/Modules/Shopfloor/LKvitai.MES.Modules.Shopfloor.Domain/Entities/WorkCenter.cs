namespace LKvitai.MES.Modules.Shopfloor.Domain.Entities;

/// <summary>A production work center grouping one or more work stations.</summary>
public sealed class WorkCenter
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    private WorkCenter() { }

    public WorkCenter(Guid id, string code, string name)
    {
        Id = id;
        Rename(name);
        Recode(code);
    }

    public void Recode(string code)
    {
        Code = NormalizeRequired(code, nameof(code));
    }

    public void Rename(string name)
    {
        Name = NormalizeRequired(name, nameof(name));
    }

    private static string NormalizeRequired(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} is required.", field);
        }

        return value.Trim();
    }
}
