namespace ThreeDLoads.Application.Models;

public sealed class ValidationReport
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
}