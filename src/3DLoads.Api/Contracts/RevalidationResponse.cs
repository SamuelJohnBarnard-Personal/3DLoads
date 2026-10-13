namespace ThreeDLoads.Api.Contracts;

// Response returned by on-demand revalidation of a stored solution.
public sealed class RevalidationResponse
{
    public Guid SolutionId { get; init; }
    public bool IsValid { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    public DateTimeOffset ValidatedAtUtc { get; init; }
}