using ThreeDLoads.Application.Models;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Tests.Unit;

public class ExtremePointAnchorEngineTests
{
    [Fact]
    public void GetAnchors_ReturnsOrigin_WhenNoPlacements()
    {
        var sut = new ExtremePointAnchorEngine();
        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "s",
                Van = new Van { Name = "v", Length = 10, Width = 5, Height = 4 }
            }
        };

        var anchors = sut.GetAnchors(state, 10);

        Assert.Contains(anchors, a => a.X == 0 && a.Y == 0 && a.Z == 0);
    }

    [Fact]
    public void GetAnchors_RespectsDepthBoundary()
    {
        var sut = new ExtremePointAnchorEngine();
        var state = new SolutionState
        {
            Scenario = new Scenario
            {
                Name = "s",
                Van = new Van { Name = "v", Length = 12, Width = 6, Height = 6 }
            }
        };

        state.Placements.Add(new Placement
        {
            X = 8,
            Y = 0,
            Z = 0,
            OrientedLength = 2,
            OrientedWidth = 2,
            OrientedHeight = 2
        });

        var anchors = sut.GetAnchors(state, 7.5);

        Assert.DoesNotContain(anchors, a => a.X > 7.5);
    }
}