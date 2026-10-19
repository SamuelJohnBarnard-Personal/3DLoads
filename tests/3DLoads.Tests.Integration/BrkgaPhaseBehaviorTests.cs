using ThreeDLoads.Application.Models;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Application.Services.Phases;
using ThreeDLoads.Application.Services.Phases.Brkga;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Tests.Integration;

public sealed class BrkgaPhaseBehaviorTests
{
    [Fact]
    public async Task ExecuteAsync_PrefersStackingBeforePlacingPackageTowardDoors()
    {
        var first = CreatePackage("first", length: 4);
        var second = CreatePackage("second", length: 2);
        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "stack-first",
                Van = new Van { Name = "van", Length = 4, Width = 2, Height = 4 },
                Packages = [first, second]
            }
        };
        var phase = new BrkgaPhase(
            new OrientationEnumerator(),
            new GeometryFeasibilityChecker(),
            new ExtremePointAnchorEngine());

        await phase.ExecuteAsync(
            state,
            new SolveExecutionContext
            {
                PhaseNames = ["Brkga"],
                TimeBudget = TimeSpan.FromSeconds(1),
                RandomSeed = 42,
                BrkgaPopulationSize = 30,
                BrkgaMaxGenerations = 5
            },
            CancellationToken.None);

        Assert.Equal(2, state.Placements.Count);
        Assert.Contains(state.Placements, placement => placement.Z == 2);
    }

    private static PackageItem CreatePackage(string reference, double length) => new()
    {
        ExternalReference = reference,
        Length = length,
        Width = 2,
        Height = 2,
        Weight = 1,
        MaxLoadBearingWeight = 10,
        DeliveryStopIndex = 1,
        OrientationPolicy = OrientationPolicy.Fixed
    };
}