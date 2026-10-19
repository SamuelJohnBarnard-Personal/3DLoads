using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Services.Phases.Brkga;

public sealed partial class BrkgaPhase
{
    private SolutionState Decode(
        Chromosome chromosome,
        Scenario scenario,
        IReadOnlyList<List<PackageItem>> stopGroups,
        IReadOnlyDictionary<Guid, int> packageIndex,
        IReadOnlyDictionary<Guid, IReadOnlyList<Dimensions3D>> orientationMap,
        SolveExecutionContext context)
    {
        var decoded = new SolutionState { Scenario = scenario };
        var depthBoundary = scenario.Van!.Length;

        foreach (var group in stopGroups)
        {
            var ordered = group
                .OrderBy(item => chromosome.PriorityKeys[packageIndex[item.Id]])
                .ToList();
            var minDepthUsedInGroup = depthBoundary; //limits how far back in depth items in this group can be placed

            foreach (var item in ordered)
            {
                var itemIndex = packageIndex[item.Id];
                var orientations = orientationMap[item.Id];
                if (orientations.Count == 0)
                {
                    decoded.UnpackedItems.Add(item);
                    continue;
                }

                var selectedIndex = SelectOrientationIndex(chromosome.OrientationKeys[itemIndex], orientations.Count);
                var orientationOrder = Enumerable.Range(0, orientations.Count)
                    .OrderBy(index => index == selectedIndex ? 0 : 1)
                    .ThenBy(index => index)
                    .ToList();

                var placed = false;
                foreach (var orientationIndex in orientationOrder)
                {
                    if (!TryPlace(
                        decoded,
                        item,
                        orientations[orientationIndex],
                        depthBoundary,
                        context,
                        elevatedAnchorsOnly: true,
                        out var placementX))
                    {
                        continue;
                    }

                    minDepthUsedInGroup = Math.Min(minDepthUsedInGroup, placementX);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    foreach (var orientationIndex in orientationOrder) //attempt alt orientations
                    {
                        if (!TryPlace(
                            decoded,
                            item,
                            orientations[orientationIndex],
                            depthBoundary,
                            context,
                            elevatedAnchorsOnly: false,
                            out var placementX,
                            nonElevatedAnchorsOnly: true))
                        {
                            continue;
                        }

                        minDepthUsedInGroup = Math.Min(minDepthUsedInGroup, placementX);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    decoded.UnpackedItems.Add(item);
                }
            }

            depthBoundary = Math.Min(depthBoundary, minDepthUsedInGroup);
        }

        return decoded;
    }

    private bool TryPlace(
        SolutionState state,
        PackageItem item,
        Dimensions3D orientation,
        double depthBoundary,
        SolveExecutionContext context,
        bool elevatedAnchorsOnly,
        out double placedX,
        bool nonElevatedAnchorsOnly = false)
    {
        var anchors = anchorEngine.GetAnchors(state, depthBoundary)
            .Where(anchor => !elevatedAnchorsOnly || anchor.Z > Epsilon)
            .Where(anchor => !nonElevatedAnchorsOnly || anchor.Z <= Epsilon)
            .OrderByDescending(anchor => anchor.Z);

        foreach (var anchor in anchors)
        {
            var candidateXs = new[] { depthBoundary - orientation.Length, anchor.X }
                .Distinct()
                .OrderByDescending(x => x);

            foreach (var candidateX in candidateXs)
            {
                if (candidateX < -Epsilon || candidateX + orientation.Length > depthBoundary + Epsilon)
                {
                    continue;
                }

                var candidate = new CandidatePlacement
                {
                    PackageItem = item,
                    Position = new Position3D(candidateX, anchor.Y, anchor.Z),
                    OrientedSize = orientation
                };

                if (!checker.IsPlacementFeasible(state, candidate, context))
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
                    OrientedLength = orientation.Length,
                    OrientedWidth = orientation.Width,
                    OrientedHeight = orientation.Height
                });

                placedX = candidate.Position.X;
                return true;
            }
        }

        placedX = 0;
        return false;
    }

    private static int SelectOrientationIndex(double key, int orientationCount)
    {
        var bounded = Math.Clamp(key, 0, 0.999999);
        return Math.Min(orientationCount - 1, (int)(bounded * orientationCount));
    }
}