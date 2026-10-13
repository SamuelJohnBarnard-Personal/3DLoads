using ThreeDLoads.Application.Models;

namespace ThreeDLoads.Application.Abstractions;

// Validates that a candidate placement fits inside the van and does not overlap any existing box.
public interface IGeometryFeasibilityChecker
{
    bool IsPlacementFeasible(SolutionState state, CandidatePlacement candidate, SolveExecutionContext? context = null);
}