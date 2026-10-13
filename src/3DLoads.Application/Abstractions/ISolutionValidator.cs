using ThreeDLoads.Application.Models;

namespace ThreeDLoads.Application.Abstractions;

public interface ISolutionValidator
{
    ValidationReport Validate(SolutionState state, CancellationToken cancellationToken, SolveExecutionContext? context = null);
}