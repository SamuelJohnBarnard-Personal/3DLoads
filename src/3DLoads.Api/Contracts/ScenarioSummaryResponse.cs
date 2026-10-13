namespace ThreeDLoads.Api.Contracts;

// Lightweight list item returned by GET /api/v1/scenarios.
public sealed class ScenarioSummaryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string VanName { get; init; } = string.Empty;
    public int PackageCount { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}