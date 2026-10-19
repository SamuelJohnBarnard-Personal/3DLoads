using ThreeDLoads.Application.Models;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Application.Services.Phases;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Tests.Integration;

public sealed class CleanupPhaseBehaviorTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesUnpackedPackageAndPlacesItOnExistingPackage()
    {
        var phase = CreatePhase();
        var packed = CreatePackage("packed");
        var unpacked = CreatePackage("unpacked");
        var state = CreateState(packed, unpacked, length: 10, width: 2, height: 4);
        state.Placements.Add(new Placement
        {
            PackageItemId = packed.Id,
            PackageItem = packed,
            X = 6,
            Y = 0,
            Z = 0,
            OrientedLength = 4,
            OrientedWidth = 2,
            OrientedHeight = 2
        });
        state.UnpackedItems.Add(unpacked);

        await phase.ExecuteAsync(state, CreateContext(), CancellationToken.None);

        var placement = Assert.Single(state.Placements, item => item.PackageItemId == unpacked.Id);
        Assert.Equal(2, placement.Z);
        Assert.DoesNotContain(state.UnpackedItems, item => item.Id == unpacked.Id);
    }

    [Fact]
    public async Task ExecuteAsync_KeepsRetriedPackageWithinHigherStopDepthBoundary()
    {
        var phase = CreatePhase();
        var deeperStopPackage = CreatePackage("later", stopIndex: 2);
        var unpackedEarlierStopPackage = CreatePackage("earlier", stopIndex: 1);
        var state = CreateState(deeperStopPackage, unpackedEarlierStopPackage, length: 10, width: 2, height: 2);
        state.Placements.Add(new Placement
        {
            PackageItemId = deeperStopPackage.Id,
            PackageItem = deeperStopPackage,
            X = 4,
            Y = 0,
            Z = 0,
            OrientedLength = 3,
            OrientedWidth = 2,
            OrientedHeight = 2
        });
        state.UnpackedItems.Add(unpackedEarlierStopPackage);

        await phase.ExecuteAsync(state, CreateContext(), CancellationToken.None);

        var placement = Assert.Single(state.Placements, item => item.PackageItemId == unpackedEarlierStopPackage.Id);
        Assert.True(placement.X + placement.OrientedLength <= 4 + 1e-6);
    }

    private static CleanupPhase CreatePhase() => new(
        new OrientationEnumerator(),
        new GeometryFeasibilityChecker(),
        new ExtremePointAnchorEngine());

    private static SolveExecutionContext CreateContext() => new()
    {
        PhaseNames = ["Cleanup"],
        TimeBudget = TimeSpan.FromSeconds(1)
    };

    private static PackageItem CreatePackage(string reference, int stopIndex = 1) => new()
    {
        ExternalReference = reference,
        Length = 2,
        Width = 2,
        Height = 2,
        Weight = 1,
        MaxLoadBearingWeight = 10,
        DeliveryStopIndex = stopIndex,
        OrientationPolicy = OrientationPolicy.Fixed
    };

    private static SolutionState CreateState(PackageItem first, PackageItem second, double length, double width, double height) => new()
    {
        Scenario = new Scenario
        {
            Name = "cleanup",
            Van = new Van { Name = "van", Length = length, Width = width, Height = height },
            Packages = [first, second]
        }
    };
}