namespace PlannerPro.Shared.Exceptions;

public sealed record ErrorResponse(
    string Title,
    int Status,
    string? Detail,
    string? ErrorCode,
    IReadOnlyDictionary<string, string[]>? Errors);
