using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Services;

// Produces the set of distinct Dimensions3D rotations allowed for a package.
// Free90 yields up to 6 axis-aligned orientations; duplicates (e.g. a cube) are removed via Distinct().
public sealed class OrientationEnumerator : IOrientationEnumerator
{
    public IReadOnlyList<Dimensions3D> Enumerate(PackageItem packageItem)
    {
        var dims = new Dimensions3D(packageItem.Length, packageItem.Width, packageItem.Height);

        return packageItem.OrientationPolicy switch
        {
            OrientationPolicy.Fixed => new[] { dims },
            // Only swap Length and Width; Height (the up dimension) is kept fixed.
            OrientationPolicy.ThisSideUp =>
                new[]
                {
                    dims,
                    new Dimensions3D(packageItem.Width, packageItem.Length, packageItem.Height)
                },
            // All six 90-degree rotations around every axis.
            OrientationPolicy.Free90 =>
                new[]
                {
                    dims,
                    new Dimensions3D(packageItem.Length, packageItem.Height, packageItem.Width),
                    new Dimensions3D(packageItem.Width, packageItem.Length, packageItem.Height),
                    new Dimensions3D(packageItem.Width, packageItem.Height, packageItem.Length),
                    new Dimensions3D(packageItem.Height, packageItem.Length, packageItem.Width),
                    new Dimensions3D(packageItem.Height, packageItem.Width, packageItem.Length)
                }
                .Distinct()
                .ToArray(),
            _ => new[] { dims }
        };
    }
}