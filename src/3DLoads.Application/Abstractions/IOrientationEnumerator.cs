using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Abstractions;

// Returns all distinct axis-aligned orientations allowed for a package by its OrientationPolicy.
public interface IOrientationEnumerator
{
    IReadOnlyList<Dimensions3D> Enumerate(PackageItem packageItem);
}