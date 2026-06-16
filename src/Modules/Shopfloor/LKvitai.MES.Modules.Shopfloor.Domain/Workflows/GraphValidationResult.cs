namespace LKvitai.MES.Modules.Shopfloor.Domain.Workflows;

/// <summary>Outcome of a workflow graph validation pass.</summary>
public sealed record GraphValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static readonly GraphValidationResult Valid = new(Array.Empty<string>());

    public static GraphValidationResult Invalid(IEnumerable<string> errors)
        => new(errors.ToList());
}
