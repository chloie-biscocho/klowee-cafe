namespace Klowee.Api.Common;

/// <summary>
/// Base type for errors that map to a specific HTTP status code. Services throw
/// these; <see cref="ApiExceptionHandler"/> turns them into ProblemDetails so
/// controllers stay free of error-shaping code.
/// </summary>
public abstract class ApiException : Exception
{
    protected ApiException(int statusCode, string title, string message) : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }

    /// <summary>Short, stable summary used as the ProblemDetails title.</summary>
    public string Title { get; }
}

/// <summary>404 — the requested resource does not exist (or is soft-deleted).</summary>
public class NotFoundException : ApiException
{
    public NotFoundException(string message)
        : base(StatusCodes.Status404NotFound, "Not found", message)
    {
    }

    public static NotFoundException For(string resource, Guid id) =>
        new($"{resource} '{id}' was not found.");
}

/// <summary>409 — the request is well-formed but breaks a business rule.</summary>
public class ConflictException : ApiException
{
    public ConflictException(string message)
        : base(StatusCodes.Status409Conflict, "Conflict", message)
    {
    }
}

/// <summary>
/// 401 — authentication failed. The message is deliberately generic so the
/// response never reveals whether the email or the password was wrong.
/// </summary>
public class InvalidCredentialsException : ApiException
{
    public InvalidCredentialsException()
        : base(StatusCodes.Status401Unauthorized, "Unauthorized", "Invalid email or password.")
    {
    }
}

/// <summary>400 — the request is syntactically valid but semantically wrong.</summary>
public class ValidationFailedException : ApiException
{
    public ValidationFailedException(string message)
        : base(StatusCodes.Status400BadRequest, "Validation failed", message)
    {
    }
}
