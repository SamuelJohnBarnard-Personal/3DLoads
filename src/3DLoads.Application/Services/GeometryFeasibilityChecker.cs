using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Application.Services;

// Checks whether a candidate placement is feasible under geometry and stacking rules:
// - inside van bounds
// - no 3D overlap
// - sufficient support under the base footprint
// - load-bearing capacity respected through the full support chain
public sealed class GeometryFeasibilityChecker : IGeometryFeasibilityChecker
{
    private const double Epsilon = 1e-6;

    public bool IsPlacementFeasible(SolutionState state, CandidatePlacement candidate, SolveExecutionContext? context = null)
    {
        if (state.Scenario.Van is null)
        {
            return false;
        }

        var van = state.Scenario.Van;

        if (candidate.Position.X < -Epsilon || candidate.Position.Y < -Epsilon || candidate.Position.Z < -Epsilon)
        {
            return false;
        }

        if (candidate.Position.X + candidate.OrientedSize.Length > van.Length + Epsilon ||
            candidate.Position.Y + candidate.OrientedSize.Width > van.Width + Epsilon ||
            candidate.Position.Z + candidate.OrientedSize.Height > van.Height + Epsilon)
        {
            return false;
        }

        foreach (var existing in state.Placements)
        {
            if (Overlaps(existing, candidate))
            {
                return false;
            }
        }

        // Floor placements are always fully supported and do not transfer load to other items.
        if (candidate.Position.Z <= Epsilon)
        {
            return true;
        }

        var supports = GetSupportingContacts(state, candidate);
        if (supports.Count == 0)
        {
            return false;
        }

        var baseArea = candidate.OrientedSize.Length * candidate.OrientedSize.Width;
        if (baseArea <= Epsilon)
        {
            return false;
        }

        var supportedArea = supports.Sum(x => x.OverlapArea);
        var supportFraction = supportedArea / baseArea;
        var minSupportFraction = Math.Clamp(context?.MinSupportFraction ?? 1.0, 0.1, 1.0);
        if (supportFraction + Epsilon < minSupportFraction)
        {
            return false;
        }

        var currentLoadMemo = new Dictionary<Guid, double>();
        foreach (var support in supports)
        {
            var share = support.OverlapArea / supportedArea;
            var addedLoad = candidate.PackageItem.Weight * share;

            // Added load must fit on direct support and every support below it.
            if (!CanAcceptAddedLoad(state, support.Placement, addedLoad, currentLoadMemo))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanAcceptAddedLoad(
        SolutionState state,
        Placement placement,
        double addedLoad,
        Dictionary<Guid, double> currentLoadMemo)
    {
        if (addedLoad <= Epsilon)
        {
            return true;
        }

        var capacity = placement.PackageItem?.MaxLoadBearingWeight ?? 0;
        var currentLoad = ComputeCurrentLoadOnPlacement(state, placement, currentLoadMemo);

        if (currentLoad + addedLoad > capacity + Epsilon)
        {
            return false;
        }

        // Propagate the newly added load to supports beneath this placement.
        foreach (var supportBelow in GetSupportsForPlacement(state, placement))
        {
            if (supportBelow.TotalSupportArea <= Epsilon)
            {
                continue;
            }

            var propagatedLoad = addedLoad * (supportBelow.OverlapArea / supportBelow.TotalSupportArea);
            if (!CanAcceptAddedLoad(state, supportBelow.Placement, propagatedLoad, currentLoadMemo))
            {
                return false;
            }
        }

        return true;
    }

    private double ComputeCurrentLoadOnPlacement(
        SolutionState state,
        Placement target,
        Dictionary<Guid, double> memo)
    {
        if (memo.TryGetValue(target.Id, out var cached))
        {
            return cached;
        }

        var targetTop = target.Z + target.OrientedHeight;
        var currentLoad = 0.0;

        foreach (var upper in state.Placements)
        {
            if (Math.Abs(upper.Z - targetTop) > Epsilon)
            {
                continue;
            }

            var contactArea = OverlapArea2D(
                target.X,
                target.Y,
                target.OrientedLength,
                target.OrientedWidth,
                upper.X,
                upper.Y,
                upper.OrientedLength,
                upper.OrientedWidth);

            if (contactArea <= Epsilon)
            {
                continue;
            }

            var supportsForUpper = GetSupportsForPlacement(state, upper);
            if (supportsForUpper.Count == 0)
            {
                continue;
            }

            var totalSupportArea = supportsForUpper[0].TotalSupportArea;
            if (totalSupportArea <= Epsilon)
            {
                continue;
            }

            var upperWeight = upper.PackageItem?.Weight ?? 0;
            var upperCarriedLoad = ComputeCurrentLoadOnPlacement(state, upper, memo);
            var transmittedLoad = (upperWeight + upperCarriedLoad) * (contactArea / totalSupportArea);
            currentLoad += transmittedLoad;
        }

        memo[target.Id] = currentLoad;
        return currentLoad;
    }

    private List<SupportContact> GetSupportingContacts(SolutionState state, CandidatePlacement candidate)
    {
        var contacts = new List<SupportContact>();
        var totalArea = 0.0;

        foreach (var support in state.Placements)
        {
            var supportTop = support.Z + support.OrientedHeight;
            if (Math.Abs(supportTop - candidate.Position.Z) > Epsilon)
            {
                continue;
            }

            var overlapArea = OverlapArea2D(
                candidate.Position.X,
                candidate.Position.Y,
                candidate.OrientedSize.Length,
                candidate.OrientedSize.Width,
                support.X,
                support.Y,
                support.OrientedLength,
                support.OrientedWidth);

            if (overlapArea <= Epsilon)
            {
                continue;
            }

            totalArea += overlapArea;
            contacts.Add(new SupportContact(support, overlapArea, 0));
        }

        for (var i = 0; i < contacts.Count; i++)
        {
            contacts[i] = contacts[i] with { TotalSupportArea = totalArea };
        }

        return contacts;
    }

    private List<SupportContact> GetSupportsForPlacement(SolutionState state, Placement placement)
    {
        var contacts = new List<SupportContact>();
        if (placement.Z <= Epsilon)
        {
            return contacts;
        }

        var totalArea = 0.0;
        foreach (var support in state.Placements)
        {
            var supportTop = support.Z + support.OrientedHeight;
            if (Math.Abs(supportTop - placement.Z) > Epsilon)
            {
                continue;
            }

            var overlapArea = OverlapArea2D(
                placement.X,
                placement.Y,
                placement.OrientedLength,
                placement.OrientedWidth,
                support.X,
                support.Y,
                support.OrientedLength,
                support.OrientedWidth);

            if (overlapArea <= Epsilon)
            {
                continue;
            }

            totalArea += overlapArea;
            contacts.Add(new SupportContact(support, overlapArea, 0));
        }

        for (var i = 0; i < contacts.Count; i++)
        {
            contacts[i] = contacts[i] with { TotalSupportArea = totalArea };
        }

        return contacts;
    }

    private static double OverlapArea2D(
        double x1,
        double y1,
        double l1,
        double w1,
        double x2,
        double y2,
        double l2,
        double w2)
    {
        var overlapX = Math.Max(0, Math.Min(x1 + l1, x2 + l2) - Math.Max(x1, x2));
        var overlapY = Math.Max(0, Math.Min(y1 + w1, y2 + w2) - Math.Max(y1, y2));
        return overlapX * overlapY;
    }

    // Two AABBs overlap only when they overlap on all three axes simultaneously.
    private static bool Overlaps(Placement existing, CandidatePlacement candidate)
    {
        var xOverlap = existing.X < candidate.Position.X + candidate.OrientedSize.Length &&
                       existing.X + existing.OrientedLength > candidate.Position.X;
        var yOverlap = existing.Y < candidate.Position.Y + candidate.OrientedSize.Width &&
                       existing.Y + existing.OrientedWidth > candidate.Position.Y;
        var zOverlap = existing.Z < candidate.Position.Z + candidate.OrientedSize.Height &&
                       existing.Z + existing.OrientedHeight > candidate.Position.Z;

        return xOverlap && yOverlap && zOverlap;
    }

    private readonly record struct SupportContact(Placement Placement, double OverlapArea, double TotalSupportArea);
}