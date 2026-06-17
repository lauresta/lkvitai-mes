using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

namespace LKvitai.MES.Modules.Shopfloor.Application.Exceptions;

/// <summary>Raised when a referenced resource does not exist (maps to 404).</summary>
public sealed class ShopfloorNotFoundException : Exception
{
    public ShopfloorNotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// Raised when an operation conflicts with the current state, e.g. deleting a
/// work center still referenced by stations (maps to 409).
/// </summary>
public sealed class ShopfloorConflictException : Exception
{
    public ShopfloorConflictException(string message) : base(message)
    {
    }
}

/// <summary>Raised for validation failures (maps to 400).</summary>
public sealed class ShopfloorValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public ShopfloorValidationException(string message) : this(message, new[] { message })
    {
    }

    public ShopfloorValidationException(string message, IReadOnlyList<string> errors) : base(message)
    {
        Errors = errors;
    }
}

/// <summary>
/// Raised when a workflow cannot be published because the smart validator found
/// blocking errors. Carries the full <see cref="ValidationReportDto"/> so the API
/// can return it (maps to 422).
/// </summary>
public sealed class ShopfloorWorkflowNotPublishableException : Exception
{
    public ShopfloorWorkflowNotPublishableException(ValidationReportDto report)
        : base($"Workflow cannot be published: {report.Summary.Errors} blocking error(s).")
    {
        Report = report;
    }

    public ValidationReportDto Report { get; }
}
