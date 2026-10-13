using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Api.Contracts;

// Current status snapshot of a solve job, used for polling from the client.
public sealed class SolveJobResponse
{
    public Guid Id { get; init; }
    public Guid ScenarioId { get; init; }
    public SolveJobStatus Status { get; init; }
    public Guid? SolutionId { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}