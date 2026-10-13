using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Application.Services.Phases;

// Placeholder for a BRKGA (Biased Random-Key Genetic Algorithm) metaheuristic phase.
// Not yet implemented; currently passes state through unchanged and records a zero-time metric.
public sealed class BrkgaPhase : IPhase
{
    public string Name => "Brkga";

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