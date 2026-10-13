using ThreeDLoads.Application.Models;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Tests.Unit;

public class GeometryFeasibilityCheckerTests
{
    [Fact]
    public void IsPlacementFeasible_ReturnsTrue_WhenInsideVanWithoutOverlap()
    {
        var checker = new GeometryFeasibilityChecker();
        var state = BuildState();

        var candidate = new CandidatePlacement
        {
            PackageItem = new PackageItem { Length = 2, Width = 2, Height = 2 },
            Position = new Position3D(0, 0, 0),
            OrientedSize = new Dimensions3D(2, 2, 2)
        };

        Assert.True(checker.IsPlacementFeasible(state, candidate));
    }

    [Fact]
    public void IsPlacementFeasible_ReturnsFalse_WhenOutOfBounds()
    {
        var checker = new GeometryFeasibilityChecker();
        var state = BuildState();

        var candidate = new CandidatePlacement
        {
            PackageItem = new PackageItem { Length = 2, Width = 2, Height = 2 },
            Position = new Position3D(9.5, 0, 0),
            OrientedSize = new Dimensions3D(2, 2, 2)
        };

        Assert.False(checker.IsPlacementFeasible(state, candidate));
    }

    [Fact]
    public void IsPlacementFeasible_ReturnsFalse_WhenOverlappingPlacement()
    {
        var checker = new GeometryFeasibilityChecker();
        var state = BuildState();
        state.Placements.Add(new Placement
        {
            X = 0,
            Y = 0,
            Z = 0,
            OrientedLength = 3,
            OrientedWidth = 3,
            OrientedHeight = 3
        });

        var candidate = new CandidatePlacement
        {
            PackageItem = new PackageItem { Length = 2, Width = 2, Height = 2 },
            Position = new Position3D(1, 1, 1),
            OrientedSize = new Dimensions3D(2, 2, 2)
        };

        Assert.False(checker.IsPlacementFeasible(state, candidate));
    }

    [Fact]
    public void IsPlacementFeasible_ReturnsFalse_WhenSupportFractionIsPartial()
    {
        var checker = new GeometryFeasibilityChecker();
        var state = BuildState();

        var supportItem = new PackageItem
        {
            Weight = 10,
            MaxLoadBearingWeight = 100
        };

        state.Placements.Add(new Placement
        {
            PackageItem = supportItem,
            PackageItemId = supportItem.Id,
            X = 0,
            Y = 0,
            Z = 0,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        var candidateItem = new PackageItem { Weight = 1 };
        var candidate = new CandidatePlacement
        {
            PackageItem = candidateItem,
            Position = new Position3D(1, 0, 2),
            OrientedSize = new Dimensions3D(2, 2, 2)
        };

        Assert.False(checker.IsPlacementFeasible(state, candidate));
    }

    [Fact]
    public void IsPlacementFeasible_ReturnsTrue_WhenSupportFractionThresholdIsReduced()
    {
        var checker = new GeometryFeasibilityChecker();
        var state = BuildState();

        var supportItem = new PackageItem
        {
            Weight = 10,
            MaxLoadBearingWeight = 100
        };

        state.Placements.Add(new Placement
        {
            PackageItem = supportItem,
            PackageItemId = supportItem.Id,
            X = 0,
            Y = 0,
            Z = 0,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        var candidateItem = new PackageItem { Weight = 1 };
        var candidate = new CandidatePlacement
        {
            PackageItem = candidateItem,
            Position = new Position3D(1, 0, 2),
            OrientedSize = new Dimensions3D(2, 2, 2)
        };

        var context = new SolveExecutionContext
        {
            PhaseNames = ["InitialGreedy"],
            TimeBudget = TimeSpan.FromSeconds(1),
            MinSupportFraction = 0.5
        };

        Assert.True(checker.IsPlacementFeasible(state, candidate, context));
    }

    [Fact]
    public void IsPlacementFeasible_ReturnsFalse_WhenDirectSupportLoadBearingWouldBeExceeded()
    {
        var checker = new GeometryFeasibilityChecker();
        var state = BuildState();

        var supportItem = new PackageItem
        {
            Weight = 10,
            MaxLoadBearingWeight = 5
        };

        state.Placements.Add(new Placement
        {
            PackageItem = supportItem,
            PackageItemId = supportItem.Id,
            X = 0,
            Y = 0,
            Z = 0,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        var candidateItem = new PackageItem { Weight = 6 };
        var candidate = new CandidatePlacement
        {
            PackageItem = candidateItem,
            Position = new Position3D(0, 0, 2),
            OrientedSize = new Dimensions3D(2, 2, 2)
        };

        Assert.False(checker.IsPlacementFeasible(state, candidate));
    }

    [Fact]
    public void IsPlacementFeasible_ReturnsFalse_WhenAncestorLoadBearingWouldBeExceeded()
    {
        var checker = new GeometryFeasibilityChecker();
        var state = BuildState();

        var bottom = new PackageItem
        {
            Weight = 8,
            MaxLoadBearingWeight = 8
        };

        var middle = new PackageItem
        {
            Weight = 6,
            MaxLoadBearingWeight = 100
        };

        state.Placements.Add(new Placement
        {
            Id = Guid.NewGuid(),
            PackageItem = bottom,
            PackageItemId = bottom.Id,
            X = 0,
            Y = 0,
            Z = 0,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        state.Placements.Add(new Placement
        {
            Id = Guid.NewGuid(),
            PackageItem = middle,
            PackageItemId = middle.Id,
            X = 0,
            Y = 0,
            Z = 2,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        var candidateItem = new PackageItem { Weight = 3 };
        var candidate = new CandidatePlacement
        {
            PackageItem = candidateItem,
            Position = new Position3D(0, 0, 4),
            OrientedSize = new Dimensions3D(2, 2, 2)
        };

        // Bottom already carries middle's weight (=6). Adding candidate (+3) through middle
        // would force bottom to carry 9, exceeding its max load-bearing of 8.
        Assert.False(checker.IsPlacementFeasible(state, candidate));
    }

    [Fact]
    public void IsPlacementFeasible_ReturnsTrue_WhenFullySupportedAndLoadBearingWithinLimits()
    {
        var checker = new GeometryFeasibilityChecker();
        var state = BuildState();

        var supportItem = new PackageItem
        {
            Weight = 10,
            MaxLoadBearingWeight = 50
        };

        state.Placements.Add(new Placement
        {
            PackageItem = supportItem,
            PackageItemId = supportItem.Id,
            X = 0,
            Y = 0,
            Z = 0,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        var candidateItem = new PackageItem { Weight = 6 };
        var candidate = new CandidatePlacement
        {
            PackageItem = candidateItem,
            Position = new Position3D(0, 0, 2),
            OrientedSize = new Dimensions3D(2, 2, 2)
        };

        Assert.True(checker.IsPlacementFeasible(state, candidate));
    }

    private static SolutionState BuildState()
    {
        return new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "test",
                Van = new Van
                {
                    Name = "van",
                    Length = 10,
                    Width = 10,
                    Height = 10
                }
            }
        };
    }
}