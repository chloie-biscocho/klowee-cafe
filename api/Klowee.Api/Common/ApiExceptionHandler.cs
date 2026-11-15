using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Common;

/// <summary>
/// Translates <see cref="ApiException"/> into an RFC 7807 ProblemDetails response.
/// Anything else is left to the default handler, which also emits ProblemDetails
/// (see AddProblemDetails in Program.cs) but as an opaque 500.
/// </summary>
public class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ApiException apiException)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, Services.TabLinks.Redact(httpContext.Request.Path)); // never a tab token
            return false;
        }

        httpContext.Response.StatusCode = apiException.StatusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = apiException,
            ProblemDetails = apiException is FieldValidationException fields
                ? new HttpValidationProblemDetails(fields.Errors.ToDictionary(e => e.Key, e => e.Value))
                {
                    Status = apiException.StatusCode,
                    Title = apiException.Title,
                    Detail = apiException.Message
                }
                : new ProblemDetails
                {
                    Status = apiException.StatusCode,
                    Title = apiException.Title,
                    Detail = apiException.Message
                }
        });
    }
}
