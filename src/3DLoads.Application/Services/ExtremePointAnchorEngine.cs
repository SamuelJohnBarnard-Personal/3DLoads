using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;

namespace ThreeDLoads.Application.Services;

// Implements the extreme-point method: each face corner of every placed box projects
// three new candidate anchor points (one per axis). Points outside the van or already
// inside an existing box are discarded. The remaining points are sorted depth-first
// (low Z, high X) to favour dense packing near the van walls.
public sealed class ExtremePointAnchorEngine : IExtremePointAnchorEngine
{
    private const double Epsilon = 1e-6;

    public IReadOnlyList<AnchorPoint> GetAnchors(SolutionState state, double maxDepthBoundaryInclusive)
    {
        if (state.Scenario.Van is null)
        {
            return [];
        }

        var van = state.Scenario.Van;
        var unique = new HashSet<string>(StringComparer.Ordinal);
        var points = new List<AnchorPoint>();

        // The van origin is always a valid anchor.
        AddIfUnique(points, unique, new AnchorPoint(0, 0, 0));

        // Project the three axis-aligned faces of each placed box plus three combined edges.
        foreach (var placement in state.Placements)
        {
            AddIfUnique(points, unique, new AnchorPoint(placement.X + placement.OrientedLength, placement.Y, placement.Z));
            AddIfUnique(points, unique, new AnchorPoint(placement.X, placement.Y + placement.OrientedWidth, placement.Z));
            AddIfUnique(points, unique, new AnchorPoint(placement.X, placement.Y, placement.Z + placement.OrientedHeight));

            AddIfUnique(points, unique, new AnchorPoint(placement.X + placement.OrientedLength, placement.Y + placement.OrientedWidth, placement.Z));
            AddIfUnique(points, unique, new AnchorPoint(placement.X + placement.OrientedLength, placement.Y, placement.Z + placement.OrientedHeight));
            AddIfUnique(points, unique, new AnchorPoint(placement.X, placement.Y + placement.OrientedWidth, placement.Z + placement.OrientedHeight));
        }

        return points
            .Where(p => IsInsideVanBounds(p, van, maxDepthBoundaryInclusive))
            .Where(p => !IsInsideAnyPlacedBox(p, state))
            .OrderBy(p => p.Z)
            .ThenByDescending(p => p.X)
            .ThenBy(p => p.Y)
            .ToList();
    }

    // Use a rounded string key to avoid floating-point hash collisions.
    private static void AddIfUnique(ICollection<AnchorPoint> points, ISet<string> unique, AnchorPoint point)
    {
        var key = $"{Math.Round(point.X, 6):F6}|{Math.Round(point.Y, 6):F6}|{Math.Round(point.Z, 6):F6}";
        if (unique.Add(key))
        {
            points.Add(point);
        }
    }

    private static bool IsInsideVanBounds(AnchorPoint point, Domain.Entities.Van van, double maxDepthBoundaryInclusive)
    {
        return point.X >= -Epsilon && point.Y >= -Epsilon && point.Z >= -Epsilon &&
               point.X <= van.Length + Epsilon &&
               point.Y <= van.Width + Epsilon &&
               point.Z <= van.Height + Epsilon &&
               point.X <= maxDepthBoundaryInclusive + Epsilon;
    }

    // A point strictly inside a box cannot be the origin of a new non-overlapping placement.
    private static bool IsInsideAnyPlacedBox(AnchorPoint point, SolutionState state)
    {
        foreach (var placement in state.Placements)
        {
            var insideX = point.X > placement.X + Epsilon && point.X < placement.X + placement.OrientedLength - Epsilon;
            var insideY = point.Y > placement.Y + Epsilon && point.Y < placement.Y + placement.OrientedWidth - Epsilon;
            var insideZ = point.Z > placement.Z + Epsilon && point.Z < placement.Z + placement.OrientedHeight - Epsilon;

            if (insideX && insideY && insideZ)
            {
                return true;
            }
        }

        return false;
    }
}