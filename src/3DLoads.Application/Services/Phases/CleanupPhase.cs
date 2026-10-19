using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Services.Phases;

// Attempts to load any unloaded packages from previous phases.
public sealed class CleanupPhase(
    IOrientationEnumerator orientationEnumerator,
    IGeometryFeasibilityChecker checker,
    IExtremePointAnchorEngine anchorEngine) : IPhase
{
    private const double Epsilon = 1e-6;

    public string Name => "Cleanup";

    public Task<SolutionState> ExecuteAsync(SolutionState state, SolveExecutionContext context, CancellationToken cancellationToken)
    {
        if (state.Scenario.Van is null || state.UnpackedItems.Count == 0)
        {
            return Task.FromResult(state);
        }

        var phaseStart = DateTimeOffset.UtcNow;
        var beforeUnpacked = state.UnpackedItems.Count;
        var beforeUtilization = CalculateVolumeUtilizationPercent(state);
        var packageStops = state.Scenario.Packages.ToDictionary(item => item.Id, item => item.DeliveryStopIndex);

        var groups = state.UnpackedItems
            .GroupBy(item => item.DeliveryStopIndex)
            .OrderByDescending(group => group.Key)
            .ToList();

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var depthBoundary = state.Placements
                .Where(placement => packageStops.GetValueOrDefault(placement.PackageItemId, int.MinValue) > group.Key)
                .Select(placement => placement.X)
                .DefaultIfEmpty(state.Scenario.Van.Length)
                .Min();

            var items = group
                .OrderByDescending(item => item.Length * item.Width * item.Height)
                .ToList();

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryPlace(state, item, depthBoundary, context, out _))
                {
                    state.UnpackedItems.Remove(item);
                }
            }
        }

        state.PhaseMetrics.Add(new PhaseMetric
        {
            PhaseName = Name,
            ElapsedMs = (long)(DateTimeOffset.UtcNow - phaseStart).TotalMilliseconds,
            BeforeUnpackedCount = beforeUnpacked,
            AfterUnpackedCount = state.UnpackedItems.Count,
            BeforeVolumeUtilizationPercent = beforeUtilization,
            AfterVolumeUtilizationPercent = CalculateVolumeUtilizationPercent(state)
        });

        return Task.FromResult(state);
    }

    private bool TryPlace(
        SolutionState state,
        PackageItem item,
        double depthBoundary,
        SolveExecutionContext context,
        out double placedX)
    {
        var orientations = orientationEnumerator.Enumerate(item);
        var anchors = anchorEngine.GetAnchors(state, depthBoundary)
            .OrderByDescending(anchor => anchor.Z);

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

                    placedX = candidateX;
                    return true;
                }
            }
        }

        placedX = 0;
        return false;
    }

    private static double CalculateVolumeUtilizationPercent(SolutionState state)
    {
        var van = state.Scenario.Van;
        if (van is null)
        {
            return 0;
        }

        var vanVolume = van.Length * van.Width * van.Height;
        if (vanVolume <= 0)
        {
            return 0;
        }

        var packedVolume = state.Placements.Sum(placement =>
            placement.OrientedLength * placement.OrientedWidth * placement.OrientedHeight);
        return packedVolume / vanVolume * 100;
    }
}