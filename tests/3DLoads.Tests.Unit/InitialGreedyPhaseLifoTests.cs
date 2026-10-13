using ThreeDLoads.Application.Models;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Application.Services.Phases;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Tests.Unit;

public class InitialGreedyPhaseLifoTests
{
    [Fact]
    public async Task ExecuteAsync_ClaimsDepthForLaterGroups()
    {
        var phase = new InitialGreedyPhase(
            new OrientationEnumerator(),
            new GeometryFeasibilityChecker(),
            new ExtremePointAnchorEngine());

        var deeperStopItem = new PackageItem
        {
            ExternalReference = "later-stop",
            Length = 6,
            Width = 2,
            Height = 2,
            OrientationPolicy = OrientationPolicy.Fixed,
            DeliveryStopIndex = 2
        };

        var earlierStopItem = new PackageItem
        {
            ExternalReference = "earlier-stop",
            Length = 4,
            Width = 2,
            Height = 2,
            OrientationPolicy = OrientationPolicy.Fixed,
            DeliveryStopIndex = 1
        };

        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "scenario",
                Van = new Van
                {
                    Name = "van",
                    Length = 10,
                    Width = 4,
                    Height = 4
                },
                Packages = new List<PackageItem> { deeperStopItem, earlierStopItem }
            }
        };

        await phase.ExecuteAsync(
            state,
            new SolveExecutionContext
            {
                PhaseNames = ["InitialGreedy"],
                TimeBudget = TimeSpan.FromSeconds(5)
            },
            CancellationToken.None);

        var deeperPlacement = state.Placements.Single(p => p.PackageItemId == deeperStopItem.Id);
        var earlierPlacement = state.Placements.Single(p => p.PackageItemId == earlierStopItem.Id);

        Assert.True(earlierPlacement.X + earlierPlacement.OrientedLength <= deeperPlacement.X + 1e-6);
    }
}