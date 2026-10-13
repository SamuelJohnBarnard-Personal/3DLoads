using ThreeDLoads.Application.Models;

namespace ThreeDLoads.Application.Abstractions;

// A single step in the solver pipeline. Each phase receives the current state, improves it, and returns the updated state.
public interface IPhase
{
    string Name { get; }
    Task<SolutionState> ExecuteAsync(SolutionState state, SolveExecutionContext context, CancellationToken cancellationToken);
}