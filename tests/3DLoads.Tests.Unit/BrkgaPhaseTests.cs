using ThreeDLoads.Application.Models;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Application.Services.Phases;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;
using System.Text.Json;

namespace ThreeDLoads.Tests.Unit;

public class BrkgaPhaseTests
{
    [Fact]
    public async Task ExecuteAsync_PlacesSingleItem_WhenFeasible()
    {
        var phase = new BrkgaPhase(
            new OrientationEnumerator(),
            new GeometryFeasibilityChecker(),
            new ExtremePointAnchorEngine());

        var item = new PackageItem
        {
            ExternalReference = "item-1",
            Length = 2,
            Width = 2,
            Height = 2,
            Weight = 10,
            DeliveryStopIndex = 1,
            OrientationPolicy = OrientationPolicy.Fixed
        };

        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "s",
                Van = new Van { Name = "v", Length = 6, Width = 4, Height = 4 },
                Packages = new List<PackageItem> { item }
            }
        };

        await phase.ExecuteAsync(
            state,
            new SolveExecutionContext
            {
                PhaseNames = ["Brkga"],
                TimeBudget = TimeSpan.FromSeconds(1),
                RandomSeed = 42
            },
            CancellationToken.None);

        Assert.Single(state.Placements);
        Assert.Empty(state.UnpackedItems);

        var metric = state.PhaseMetrics.Single(x => x.PhaseName == "Brkga");
        using var doc = JsonDocument.Parse(metric.TelemetryJson);
        Assert.Equal(42, doc.RootElement.GetProperty("Seed").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_RespectsDepthClaimsAcrossStops()
    {
        var phase = new BrkgaPhase(
            new OrientationEnumerator(),
            new GeometryFeasibilityChecker(),
            new ExtremePointAnchorEngine());

        var deeperStopItem = new PackageItem
        {
            ExternalReference = "later-stop",
            Length = 6,
            Width = 2,
            Height = 2,
            Weight = 10,
            OrientationPolicy = OrientationPolicy.Fixed,
            DeliveryStopIndex = 2
        };

        var earlierStopItem = new PackageItem
        {
            ExternalReference = "earlier-stop",
            Length = 4,
            Width = 2,
            Height = 2,
            Weight = 10,
            OrientationPolicy = OrientationPolicy.Fixed,
            DeliveryStopIndex = 1
        };

        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "scenario",
                Van = new Van { Name = "van", Length = 10, Width = 4, Height = 4 },
                Packages = new List<PackageItem> { deeperStopItem, earlierStopItem }
            }
        };

        await phase.ExecuteAsync(
            state,
            new SolveExecutionContext
            {
                PhaseNames = ["Brkga"],
                TimeBudget = TimeSpan.FromSeconds(1),
                RandomSeed = 7
            },
            CancellationToken.None);

        var deeperPlacement = state.Placements.Single(p => p.PackageItemId == deeperStopItem.Id);
        var earlierPlacement = state.Placements.Single(p => p.PackageItemId == earlierStopItem.Id);

        Assert.True(earlierPlacement.X + earlierPlacement.OrientedLength <= deeperPlacement.X + 1e-6);
    }

    [Fact]
    public async Task ExecuteAsync_UsesBrkgaOverrides_InTelemetry()
    {
        var phase = new BrkgaPhase(
            new OrientationEnumerator(),
            new GeometryFeasibilityChecker(),
            new ExtremePointAnchorEngine());

        var items = new List<PackageItem>
        {
            new()
            {
                ExternalReference = "a",
                Length = 2,
                Width = 2,
                Height = 2,
                Weight = 5,
                OrientationPolicy = OrientationPolicy.Fixed,
                DeliveryStopIndex = 2,
                MaxLoadBearingWeight = 100
            },
            new()
            {
                ExternalReference = "b",
                Length = 2,
                Width = 2,
                Height = 2,
                Weight = 5,
                OrientationPolicy = OrientationPolicy.Fixed,
                DeliveryStopIndex = 1,
                MaxLoadBearingWeight = 100
            }
        };

        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "scenario",
                Van = new Van { Name = "van", Length = 10, Width = 4, Height = 4 },
                Packages = items
            }
        };

        var context = new SolveExecutionContext
        {
            PhaseNames = ["Brkga"],
            TimeBudget = TimeSpan.FromSeconds(1),
            RandomSeed = 99,
            BrkgaPopulationSize = 36,
            BrkgaMaxGenerations = 12,
            BrkgaEliteRate = 0.25,
            BrkgaMutantRate = 0.15,
            BrkgaEliteInheritanceProbability = 0.8,
            BrkgaPhaseBudgetRatio = 0.5
        };

        await phase.ExecuteAsync(state, context, CancellationToken.None);

        var metric = state.PhaseMetrics.Single(x => x.PhaseName == "Brkga");
        using var doc = JsonDocument.Parse(metric.TelemetryJson);

        Assert.Equal(36, doc.RootElement.GetProperty("PopulationSize").GetInt32());
        Assert.Equal(12, doc.RootElement.GetProperty("MaxGenerations").GetInt32());
        Assert.Equal(99, doc.RootElement.GetProperty("Seed").GetInt32());
        Assert.True(doc.RootElement.GetProperty("Generations").GetArrayLength() > 0);
    }
}