using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;
using System.Text.Json;

namespace ThreeDLoads.Application.Services;

// Orchestrates the solver pipeline: runs each requested phase in sequence, then
// assembles the final Solution with volume statistics and unplaced-item records.
public sealed class PhasePipelineSolver(IEnumerable<IPhase> phases) : ISolverService
{
    public async Task<Solution> SolveAsync(Scenario scenario, SolveExecutionContext context, CancellationToken cancellationToken)
    {
        // Build a name-keyed map so phases can be looked up by the strings in context.PhaseNames.
        var phaseMap = phases.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var activePhases = context.PhaseNames
            .Where(phaseMap.ContainsKey)
            .Select(name => phaseMap[name])
            .ToList();

        var state = new SolutionState
        {
            Scenario = scenario
        };

        foreach (var phase in activePhases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state = await phase.ExecuteAsync(state, context, cancellationToken);
        }

        var vanVolume = scenario.Van!.Length * scenario.Van.Width * scenario.Van.Height;
        var packedVolume = state.Placements.Sum(p => p.OrientedLength * p.OrientedWidth * p.OrientedHeight);
        var unpackedVolume = state.UnpackedItems.Sum(p => p.Length * p.Width * p.Height);

        var solution = new Solution
        {
            ScenarioId = scenario.Id,
            Scenario = scenario,
            VolumeUtilizationPercent = vanVolume <= 0 ? 0 : (packedVolume / vanVolume) * 100,
            UnpackedItemCount = state.UnpackedItems.Count,
            UnpackedVolume = unpackedVolume,
            IsValid = state.IsValid,
            ValidationErrorsJson = JsonSerializer.Serialize(state.ValidationErrors)
        };

        foreach (var placement in state.Placements)
        {
            placement.SolutionId = solution.Id;
            solution.Placements.Add(placement);
        }

        foreach (var unpacked in state.UnpackedItems)
        {
            solution.UnpackedItems.Add(new UnpackedItem
            {
                SolutionId = solution.Id,
                PackageItemId = unpacked.Id,
                Reason = "No feasible placement found"
            });
        }

        foreach (var metric in state.PhaseMetrics)
        {
            metric.SolutionId = solution.Id;
            solution.PhaseMetrics.Add(metric);
        }

        return solution;
    }
}