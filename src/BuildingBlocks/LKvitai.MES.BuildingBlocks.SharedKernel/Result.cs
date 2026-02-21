namespace LKvitai.MES.BuildingBlocks.SharedKernel;

/// <summary>
/// Result pattern for operation outcomes
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string Error { get; }
    public string? ErrorCode { get; }
    public string? ErrorDetail { get; }

    protected Result(bool isSuccess, string error, string? errorCode, string? errorDetail)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
        ErrorDetail = errorDetail;
    }

    public static Result Ok() => new(true, string.Empty, null, null);

    /// <summary>
    /// Backward-compatible failure factory.
    /// If <paramref name="error"/> is a known code, it is recognized as ErrorCode.
    /// Otherwise it is treated as human-readable detail.
    /// </summary>
    public static Result Fail(string error)
    {
        var code = DomainErrorCodes.IsKnown(error) ? error : null;
        return new(false, error, code, error);
    }

    public static Result Fail(string errorCode, string errorDetail)
        => new(false, errorDetail, errorCode, errorDetail);
}

public class Result<T> : Result
{
    public T Value { get; }

    private Result(bool isSuccess, T value, string error, string? errorCode, string? errorDetail)
        : base(isSuccess, error, errorCode, errorDetail)
    {
        Value = value;
    }

    public static Result<T> Ok(T value) => new(true, value, string.Empty, null, null);

    public new static Result<T> Fail(string error)
    {
        var code = DomainErrorCodes.IsKnown(error) ? error : null;
        return new(false, default!, error, code, error);
    }

    public new static Result<T> Fail(string errorCode, string errorDetail)
        => new(false, default!, errorDetail, errorCode, errorDetail);
}
