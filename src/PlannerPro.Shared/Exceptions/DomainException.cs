namespace PlannerPro.Shared.Exceptions;

public abstract class DomainException(string message, int statusCode, string? errorCode = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string? ErrorCode { get; } = errorCode;
}
