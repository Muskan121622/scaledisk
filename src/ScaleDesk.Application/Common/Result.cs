namespace ScaleDesk.Application.Common;

/// <summary>
/// Lightweight operation result carrying success/failure + error code. Keeps services free
/// of throwing for expected business failures, which map cleanly to ProblemDetails in the API.
/// </summary>
public sealed record Result
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? Message { get; }

    private Result(bool isSuccess, string? errorCode, string? message)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Message = message;
    }

    public static Result Ok() => new(true, null, null);
    public static Result Fail(string errorCode, string message) => new(false, errorCode, message);

    public static Result<T> Ok<T>(T value) => new(true, value, null, null);
    public static Result<T> Fail<T>(string errorCode, string message) => new(false, default!, errorCode, message);
}

public sealed record Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string? ErrorCode { get; }
    public string? Message { get; }

    internal Result(bool isSuccess, T value, string? errorCode, string? message)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        Message = message;
    }

    public static implicit operator Result(Result<T> r) =>
        r.IsSuccess ? Result.Ok() : Result.Fail(r.ErrorCode!, r.Message!);
}
