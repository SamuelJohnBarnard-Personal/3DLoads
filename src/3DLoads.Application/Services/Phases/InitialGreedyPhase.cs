using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Services.Phases;

// Greedy first-fit heuristic. Packages are grouped by delivery stop in reverse order
// (highest stop index first) so that items unloaded last are loaded deepest into the van.
// Within each stop the largest-volume items are placed first. A rolling depth boundary
// prevents later stops from being buried behind earlier ones.
public sealed class InitialGreedyPhase(
    IOrientationEnumerator orientationEnumerator,
    IGeometryFeasibilityChecker checker,
    IExtremePointAnchorEngine anchorEngine) : IPhase
{
    private const double Epsilon = 1e-6;

    public string Name => "InitialGreedy";

    public Task<SolutionState> ExecuteAsync(SolutionState state, SolveExecutionContext context, CancellationToken cancellationToken)
    {
        if (state.Scenario.Van is null)
        {
            return Task.FromResult(state);
        }

        var phaseStart = DateTimeOffset.UtcNow;
        var beforeUnpacked = state.UnpackedItems.Count;

        // Process stops from furthest from the doors to closest (highest index = unloaded first = loaded deepest).
        var groupedByStop = state.Scenario.Packages
            .GroupBy(p => p.DeliveryStopIndex)
            .OrderByDescending(g => g.Key)
            .ToList();

        // depthBoundary is the maximum X value a new item's front face may reach.
        var depthBoundary = state.Scenario.Van.Length;

        foreach (var stopGroup in groupedByStop)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Largest items first to improve fill density.
            var orderedItems = stopGroup
                .OrderByDescending(x => x.Length * x.Width * x.Height)
                .ToList();

            var minDepthUsedInGroup = depthBoundary;

            foreach (var item in orderedItems)
            {
                if (TryPlace(state, item, depthBoundary, out var placementX))
                {
                    minDepthUsedInGroup = Math.Min(minDepthUsedInGroup, placementX);
                }
                else
                {
                    state.UnpackedItems.Add(item);
                }
            }

            // Each processed group claims depth, keeping later groups closer to doors.
            depthBoundary = Math.Min(depthBoundary, minDepthUsedInGroup);
        }

        state.PhaseMetrics.Add(new PhaseMetric
        {
            PhaseName = Name,
            ElapsedMs = (long)(DateTimeOffset.UtcNow - phaseStart).TotalMilliseconds,
            BeforeUnpackedCount = beforeUnpacked,
            AfterUnpackedCount = state.UnpackedItems.Count,
            BeforeVolumeUtilizationPercent = 0,
            AfterVolumeUtilizationPercent = 0
        });

        return Task.FromResult(state);
    }

    // Tries every orientation × anchor combination. For each anchor two candidate X
    // positions are tested: flush with the depth boundary and at the anchor's X.
    private bool TryPlace(SolutionState state, PackageItem item, double depthBoundary, out double placedX)
    {
        var orientations = orientationEnumerator.Enumerate(item);
        var anchors = anchorEngine.GetAnchors(state, depthBoundary);

        foreach (var orientation in orientations)
        {
            foreach (var anchor in anchors)
            {
                var candidateXs = new[]
                {
                    depthBoundary - orientation.Length,
                    anchor.X
                }
                .Distinct()
                .OrderByDescending(x => x)
                .ToArray();

                foreach (var candidateX in candidateXs)
                {
                    if (candidateX < -Epsilon)
                    {
                        continue;
                    }

                    if (candidateX + orientation.Length > depthBoundary + Epsilon)
                    {
                        continue;
                    }

                    var candidate = new CandidatePlacement
                    {
                        PackageItem = item,
                        Position = new Position3D(candidateX, anchor.Y, anchor.Z),
                        OrientedSize = orientation
                    };

                    if (!checker.IsPlacementFeasible(state, candidate))
                    {
                        continue;
                    }

                    state.Placements.Add(new Placement
                    {
                        PackageItemId = item.Id,
                        PackageItem = item,
                        X = candidate.Position.X,
                        Y = candidate.Position.Y,
                        Z = candidate.Position.Z,
                        OrientedLength = candidate.OrientedSize.Length,
                        OrientedWidth = candidate.OrientedSize.Width,
                        OrientedHeight = candidate.OrientedSize.Height
                    });

                    placedX = candidate.Position.X;
                    return true;
                }
            }
        }

        placedX = 0;
        return false;
    }
}