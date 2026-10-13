using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Application.Services.Phases;

// Defensive final gate: verifies geometry, stacking rules, LIFO depth boundaries,
// and package accounting invariants before results are returned.
public sealed class FinalValidationPhase(ISolutionValidator solutionValidator) : IPhase
{
    public string Name => "FinalValidation";

    public Task<SolutionState> ExecuteAsync(SolutionState state, SolveExecutionContext context, CancellationToken cancellationToken)
    {
        var phaseStart = DateTimeOffset.UtcNow;
        var beforeUnpacked = state.UnpackedItems.Count;
        var beforeUtilization = CalculateVolumeUtilizationPercent(state);

        var report = solutionValidator.Validate(state, cancellationToken, context);
        state.ValidationErrors.Clear();
        state.ValidationErrors.AddRange(report.Errors);
        state.IsValid = report.IsValid;

        state.PhaseMetrics.Add(new PhaseMetric
        {
            PhaseName = Name,
            ElapsedMs = (long)(DateTimeOffset.UtcNow - phaseStart).TotalMilliseconds,
            BeforeUnpackedCount = beforeUnpacked,
            AfterUnpackedCount = state.UnpackedItems.Count,
            BeforeVolumeUtilizationPercent = beforeUtilization,
            AfterVolumeUtilizationPercent = CalculateVolumeUtilizationPercent(state)
        });

        return Task.FromResult(state);
    }

    private static double CalculateVolumeUtilizationPercent(SolutionState state)
    {
        if (state.Scenario.Van is null)
        {
            return 0;
        }

        var vanVolume = state.Scenario.Van.Length * state.Scenario.Van.Width * state.Scenario.Van.Height;
        if (vanVolume <= 0)
        {
            return 0;
        }

        var packedVolume = state.Placements.Sum(p => p.OrientedLength * p.OrientedWidth * p.OrientedHeight);
        return (packedVolume / vanVolume) * 100.0;
    }
}