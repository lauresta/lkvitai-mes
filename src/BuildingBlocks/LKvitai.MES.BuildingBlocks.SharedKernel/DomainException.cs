namespace LKvitai.MES.SharedKernel;

/// <summary>
/// Base exception for domain rule violations.
/// Domain exceptions are NOT retried — they represent business logic failures.
/// </summary>
public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message) : base(message)
    {
        ErrorCode = DomainErrorCodes.ValidationError;
    }

    public DomainException(string errorCode, string message) : base(message)
    {
        ErrorCode = NormalizeErrorCode(errorCode);
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = DomainErrorCodes.ValidationError;
    }

    public DomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = NormalizeErrorCode(errorCode);
    }

    private static string NormalizeErrorCode(string errorCode)
        => DomainErrorCodes.IsKnown(errorCode)
            ? errorCode
            : DomainErrorCodes.ValidationError;
}
