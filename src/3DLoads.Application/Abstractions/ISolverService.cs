using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Application.Abstractions;

// Runs a full solve pipeline for a scenario and returns a completed Solution.
public interface ISolverService
{
    Task<Solution> SolveAsync(Scenario scenario, SolveExecutionContext context, CancellationToken cancellationToken);
}