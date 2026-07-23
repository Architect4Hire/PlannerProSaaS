using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace PlannerPro.Shared.Exceptions;

public sealed class SharedExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var response = exception switch
        {
            ValidationException validationException => new ErrorResponse(
                "One or more validation errors occurred.",
                StatusCodes.Status400BadRequest,
                validationException.Message,
                null,
                validationException.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(f => f.ErrorMessage).ToArray())),
            DomainException domainException => new ErrorResponse(
                "The request could not be completed.",
                domainException.StatusCode,
                domainException.Message,
                domainException.ErrorCode,
                null),
            _ => new ErrorResponse(
                "An unexpected error occurred.",
                StatusCodes.Status500InternalServerError,
                null,
                null,
                null),
        };

        httpContext.Response.StatusCode = response.Status;
        await httpContext.Response.WriteAsJsonAsync(response, ct);
        return true;
    }
}
