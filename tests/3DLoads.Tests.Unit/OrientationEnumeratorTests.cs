using ThreeDLoads.Application.Services;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Tests.Unit;

public class OrientationEnumeratorTests
{
    [Fact]
    public void Enumerate_ReturnsOneOrientation_ForFixed()
    {
        var item = new PackageItem
        {
            Length = 4,
            Width = 3,
            Height = 2,
            OrientationPolicy = OrientationPolicy.Fixed
        };

        var sut = new OrientationEnumerator();
        var result = sut.Enumerate(item);

        Assert.Single(result);
    }

    [Fact]
    public void Enumerate_ReturnsTwoOrientations_ForThisSideUp()
    {
        var item = new PackageItem
        {
            Length = 4,
            Width = 3,
            Height = 2,
            OrientationPolicy = OrientationPolicy.ThisSideUp
        };

        var sut = new OrientationEnumerator();
        var result = sut.Enumerate(item);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Enumerate_ReturnsSixOrientations_ForFree90()
    {
        var item = new PackageItem
        {
            Length = 4,
            Width = 3,
            Height = 2,
            OrientationPolicy = OrientationPolicy.Free90
        };

        var sut = new OrientationEnumerator();
        var result = sut.Enumerate(item);

        Assert.Equal(6, result.Count);
    }
}