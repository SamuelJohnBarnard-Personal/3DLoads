using ThreeDLoads.Application.Models;

namespace ThreeDLoads.Application.Abstractions;

// Generates candidate anchor points using the extreme-point heuristic:
// every corner projected from placed boxes (plus the origin) is a potential
// lower-left-front corner for the next item.
public interface IExtremePointAnchorEngine
{
    IReadOnlyList<AnchorPoint> GetAnchors(SolutionState state, double maxDepthBoundaryInclusive);
}