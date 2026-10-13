using ThreeDLoads.Application.Models;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Application.Services.Phases;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Tests.Unit;

public class FinalValidationPhaseTests
{
    [Fact]
    public async Task ExecuteAsync_MarksStateInvalid_WhenLifoDepthIsBroken()
    {
        var phase = new FinalValidationPhase(new SolutionValidator(new GeometryFeasibilityChecker()));

        var deepStop = new PackageItem
        {
            ExternalReference = "deep",
            Length = 3,
            Width = 2,
            Height = 2,
            Weight = 2,
            DeliveryStopIndex = 2,
            OrientationPolicy = OrientationPolicy.Fixed,
            MaxLoadBearingWeight = 100
        };

        var nearStop = new PackageItem
        {
            ExternalReference = "near",
            Length = 2,
            Width = 2,
            Height = 2,
            Weight = 2,
            DeliveryStopIndex = 1,
            OrientationPolicy = OrientationPolicy.Fixed,
            MaxLoadBearingWeight = 100
        };

        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "s",
                Van = new Van { Name = "v", Length = 10, Width = 2, Height = 2 },
                Packages = new List<PackageItem> { deepStop, nearStop }
            }
        };

        state.Placements.Add(new Placement
        {
            PackageItemId = deepStop.Id,
            PackageItem = deepStop,
            X = 2,
            Y = 0,
            Z = 0,
            OrientedLength = 3,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        // This intentionally violates the stop-1 boundary that should be <= minX of stop-2 (=2).
        state.Placements.Add(new Placement
        {
            PackageItemId = nearStop.Id,
            PackageItem = nearStop,
            X = 1,
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
                PhaseNames = ["FinalValidation"],
                TimeBudget = TimeSpan.FromSeconds(1)
            },
            CancellationToken.None);

        Assert.False(state.IsValid);
        Assert.Contains(state.ValidationErrors, x => x.Contains("LIFO depth violation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_MarksStateValid_WhenStateIsConsistent()
    {
        var phase = new FinalValidationPhase(new SolutionValidator(new GeometryFeasibilityChecker()));

        var item = new PackageItem
        {
            ExternalReference = "p1",
            Length = 2,
            Width = 2,
            Height = 2,
            Weight = 2,
            DeliveryStopIndex = 1,
            OrientationPolicy = OrientationPolicy.Fixed,
            MaxLoadBearingWeight = 100
        };

        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "s",
                Van = new Van { Name = "v", Length = 6, Width = 2, Height = 2 },
                Packages = new List<PackageItem> { item }
            }
        };

        state.Placements.Add(new Placement
        {
            PackageItemId = item.Id,
            PackageItem = item,
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
                PhaseNames = ["FinalValidation"],
                TimeBudget = TimeSpan.FromSeconds(1)
            },
            CancellationToken.None);

        Assert.True(state.IsValid);
        Assert.Empty(state.ValidationErrors);
    }
}