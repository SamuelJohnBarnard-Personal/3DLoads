using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Services.Phases;

// Greedy first-fit heuristic. Packages are grouped by delivery stop in descending order
// (highest stop index first), placing those groups deepest in the van. Within each stop,
// largest-volume items are placed first. A rolling depth boundary keeps lower-index
// groups closer to the rear doors.
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

        // Order by stop index in descending order. This ensures that packages to be delivered later are closer the back of the van.
        var groupedByStop = state.Scenario.Packages
            .GroupBy(p => p.DeliveryStopIndex)
            .OrderByDescending(g => g.Key)
            .ToList();

        // depthBoundary is the maximum X value a new item's front face may reach.
        // x coord = depth into van, y coord = side-to-side, z coord = height
        // front face is the side of the item pointing towards the front of the van (the doors are at the back of the van)
        var depthBoundary = state.Scenario.Van.Length;

        foreach (var stopGroup in groupedByStop)
        {
            // Enables stopping if solve cancelled, client disconnects or server shuts down etc.
            cancellationToken.ThrowIfCancellationRequested();

            // Within each stop, order the items by volume, with the largest items first..
            var orderedItems = stopGroup
                .OrderByDescending(x => x.Length * x.Width * x.Height)
                .ToList();

            //how far into the van you have to "reach" to touch the face of the item in this group closest to the doors of the van
            // Smallest X coordinate of this stop group's placed packages, i.e. its nearest edge to the rear doors.
            var minDepthUsedInGroup = depthBoundary;

            foreach (var item in orderedItems)
            {
                //attempts to place items one by one, largest first
                if (TryPlace(state, item, depthBoundary, out var placementX))
                {
                    minDepthUsedInGroup = Math.Min(minDepthUsedInGroup, placementX);
                }
                else
                {
                    state.UnpackedItems.Add(item); //adds to a list of items that could not be placed
                }
            }

            // Each processed group claims depth, ensuring later groups are closer to doors.
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
        //enumerate all possible orientations for the item
        //some items have to be placed with a particular side up, for example, while others can be rotated freely
        var orientations = orientationEnumerator.Enumerate(item);

        //get all potential anchor points for the item at the current depth boundary
        //anchor points are potential positions where the item could be placed at the current depth boundary
        //this is a list of possible starting corners for the package
        var anchors = anchorEngine.GetAnchors(state, depthBoundary)
            .OrderByDescending(anchor => anchor.Z); //prioritise stacking items higher up first

        //Try every orientation × anchor combination, stopping as soon as a feasible placement is found.
        foreach (var orientation in orientations)
        {
            foreach (var anchor in anchors)
            {
                var candidateXs = new[]
                {
                    depthBoundary - orientation.Length, // flush with the depth boundary
                    anchor.X // at the anchor's X position
                }
                .Distinct()
                .OrderByDescending(x => x)
                .ToArray();

                foreach (var candidateX in candidateXs)
                {
                    if (candidateX < -Epsilon)
                    {
                        continue; // continue if negative
                    }

                    if (candidateX + orientation.Length > depthBoundary + Epsilon)
                    {
                        continue; //continue if package would extend beyond the depth boundary.
                    }

                    var candidate = new CandidatePlacement
                    {
                        PackageItem = item,
                        Position = new Position3D(candidateX, anchor.Y, anchor.Z), //minimum coord corner - represents the lowest X, Y, Z coordinates of the placement.
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