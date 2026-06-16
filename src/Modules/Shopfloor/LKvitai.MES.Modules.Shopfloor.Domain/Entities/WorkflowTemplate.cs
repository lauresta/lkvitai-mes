namespace LKvitai.MES.Modules.Shopfloor.Domain.Entities;

/// <summary>
/// A production family: a named, coded workflow template whose graph is stored
/// as JSON in <c>graph_json</c>. The graph shape itself is owned by the
/// Contracts/Application layers; the Domain entity only guards the header
/// fields and lifecycle.
/// </summary>
public sealed class WorkflowTemplate
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public string GraphJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private WorkflowTemplate() { }

    public WorkflowTemplate(
        Guid id,
        string code,
        string name,
        string? description,
        string graphJson,
        DateTimeOffset createdAt)
    {
        Id = id;
        Code = NormalizeRequired(code, nameof(code));
        Name = NormalizeRequired(name, nameof(name));
        Description = NormalizeOptional(description);
        Status = WorkflowStatus.Draft;
        GraphJson = string.IsNullOrWhiteSpace(graphJson)
            ? throw new ArgumentException("graphJson is required.", nameof(graphJson))
            : graphJson;
        CreatedAt = createdAt;
    }

    public void UpdateHeader(string code, string name, string? description, DateTimeOffset now)
    {
        Code = NormalizeRequired(code, nameof(code));
        Name = NormalizeRequired(name, nameof(name));
        Description = NormalizeOptional(description);
        UpdatedAt = now;
    }

    public void SaveGraph(string graphJson, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            throw new ArgumentException("graphJson is required.", nameof(graphJson));
        }

        GraphJson = graphJson;
        UpdatedAt = now;
    }

    public void Publish(DateTimeOffset now)
    {
        Status = WorkflowStatus.Published;
        UpdatedAt = now;
    }

    private static string NormalizeRequired(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} is required.", field);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
