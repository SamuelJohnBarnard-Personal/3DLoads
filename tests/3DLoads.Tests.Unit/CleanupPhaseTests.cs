using ThreeDLoads.Application.Models;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Application.Services.Phases;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Tests.Unit;

public class CleanupPhaseTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesUnpackedAndPlacesItem_WhenSpaceExists()
    {
        var phase = new CleanupPhase(
            new OrientationEnumerator(),
            new GeometryFeasibilityChecker(),
            new ExtremePointAnchorEngine());

        var packed = new PackageItem
        {
            ExternalReference = "packed",
            Length = 2,
            Width = 2,
            Height = 2,
            Weight = 5,
            DeliveryStopIndex = 1,
            OrientationPolicy = OrientationPolicy.Fixed
        };

        var unpacked = new PackageItem
        {
            ExternalReference = "unpacked",
            Length = 2,
            Width = 2,
            Height = 2,
            Weight = 5,
            DeliveryStopIndex = 1,
            OrientationPolicy = OrientationPolicy.Fixed
        };

        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "scenario",
                Van = new Van { Name = "van", Length = 6, Width = 2, Height = 2 },
                Packages = new List<PackageItem> { packed, unpacked }
            }
        };

        state.Placements.Add(new Placement
        {
            PackageItemId = packed.Id,
            PackageItem = packed,
            X = 0,
            Y = 0,
            Z = 0,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        state.UnpackedItems.Add(unpacked);

        await phase.ExecuteAsync(
            state,
            new SolveExecutionContext
            {
                PhaseNames = ["Cleanup"],
                TimeBudget = TimeSpan.FromSeconds(1)
            },
            CancellationToken.None);

        Assert.DoesNotContain(state.UnpackedItems, x => x.Id == unpacked.Id);
        Assert.Contains(state.Placements, x => x.PackageItemId == unpacked.Id);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotBreakLifoDepthBoundary_AfterCompaction()
    {
        var phase = new CleanupPhase(
            new OrientationEnumerator(),
            new GeometryFeasibilityChecker(),
            new ExtremePointAnchorEngine());

        var deeperStopA = new PackageItem
        {
            ExternalReference = "deep-a",
            Length = 3,
            Width = 2,
            Height = 2,
            Weight = 5,
            DeliveryStopIndex = 2,
            OrientationPolicy = OrientationPolicy.Fixed,
            MaxLoadBearingWeight = 100
        };

        var deeperStopB = new PackageItem
        {
            ExternalReference = "deep-b",
            Length = 3,
            Width = 2,
            Height = 2,
            Weight = 5,
            DeliveryStopIndex = 2,
            OrientationPolicy = OrientationPolicy.Fixed,
            MaxLoadBearingWeight = 100
        };

        var earlierStop = new PackageItem
        {
            ExternalReference = "early",
            Length = 2,
            Width = 2,
            Height = 2,
            Weight = 5,
            DeliveryStopIndex = 1,
            OrientationPolicy = OrientationPolicy.Fixed,
            MaxLoadBearingWeight = 100
        };

        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "lifo-compaction",
                Van = new Van { Name = "van", Length = 10, Width = 2, Height = 2 },
                Packages = new List<PackageItem> { deeperStopA, deeperStopB, earlierStop }
            }
        };

        // Initial state is valid: stop-2 items occupy deeper half, stop-1 item near doors.
        state.Placements.Add(new Placement
        {
            PackageItemId = deeperStopA.Id,
            PackageItem = deeperStopA,
            X = 4,
            Y = 0,
            Z = 0,
            OrientedLength = 3,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        state.Placements.Add(new Placement
        {
            PackageItemId = deeperStopB.Id,
            PackageItem = deeperStopB,
            X = 7,
            Y = 0,
            Z = 0,
            OrientedLength = 3,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        state.Placements.Add(new Placement
        {
            PackageItemId = earlierStop.Id,
            PackageItem = earlierStop,
            X = 0,
            Y = 0,
            Z = 0,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        await phase.ExecuteAsync(
            state,
            new SolveExecutionContext
            {
                PhaseNames = ["Cleanup"],
                TimeBudget = TimeSpan.FromSeconds(1)
            },
            CancellationToken.None);

        var minXStop2 = state.Placements
            .Where(x => x.PackageItem?.DeliveryStopIndex == 2)
            .Min(x => x.X);

        var maxRightFaceStop1 = state.Placements
            .Where(x => x.PackageItem?.DeliveryStopIndex == 1)
            .Max(x => x.X + x.OrientedLength);

        // Stop-1 items must remain entirely in front of the stop-2 claimed boundary.
        Assert.True(maxRightFaceStop1 <= minXStop2 + 1e-6);
    }
}