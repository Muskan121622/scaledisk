namespace ScaleDesk.Application.Exceptions;

/// <summary>Base for expected, mappable application failures (never a raw 500).</summary>
public abstract class AppException : Exception
{
    public string ErrorCode { get; protected set; } = "error";

    protected AppException(string message) : base(message) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) => ErrorCode = "conflict";
}

public sealed class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation failures occurred.")
    {
        ErrorCode = "validation";
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) => ErrorCode = "not_found";
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message) => ErrorCode = "forbidden";
}

/// <summary>Raised when a tenant-owned operation runs without a bound tenant context.</summary>
public sealed class TenantNotResolvedException : AppException
{
    public TenantNotResolvedException()
        : base("No tenant is bound to the current request.") => ErrorCode = "tenant_not_resolved";
}

/// <summary>Raised on an optimistic-concurrency conflict (two writers, same row).</summary>
public sealed class ConcurrencyException : AppException
{
    public ConcurrencyException(string message) : base(message) => ErrorCode = "conflict_concurrency";
}
