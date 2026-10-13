using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Application.Models;

// Mutable accumulator threaded through every phase in the pipeline.
public sealed class SolutionState
{
    public required Scenario Scenario { get; init; }
    public List<Placement> Placements { get; } = new();
    public List<PackageItem> UnpackedItems { get; } = new();
    public List<PhaseMetric> PhaseMetrics { get; } = new();
    public bool IsValid { get; set; } = true;
    public List<string> ValidationErrors { get; } = new();
}