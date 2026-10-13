using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Application.Services.Phases;

// Placeholder for a post-processing phase (e.g. local search or compaction).
// Not yet implemented; currently passes state through unchanged and records a zero-time metric.
public sealed class CleanupPhase : IPhase
{
    public string Name => "Cleanup";

    public Task<SolutionState> ExecuteAsync(SolutionState state, SolveExecutionContext context, CancellationToken cancellationToken)
    {
        state.PhaseMetrics.Add(new PhaseMetric
        {
            PhaseName = Name,
            ElapsedMs = 0,
            BeforeUnpackedCount = state.UnpackedItems.Count,
            AfterUnpackedCount = state.UnpackedItems.Count,
            BeforeVolumeUtilizationPercent = 0,
            AfterVolumeUtilizationPercent = 0
        });

        return Task.FromResult(state);
    }
}