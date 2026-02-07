namespace LKvitai.MES.SharedKernel;

/// <summary>
/// Base exception for domain rule violations.
/// Domain exceptions are NOT retried — they represent business logic failures.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
