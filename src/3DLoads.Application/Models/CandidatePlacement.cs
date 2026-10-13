using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Models;

// Transient working model used to test whether a package can be placed before committing it to state.
public sealed class CandidatePlacement
{
    public required PackageItem PackageItem { get; init; }
    public required Position3D Position { get; init; }
    public required Dimensions3D OrientedSize { get; init; }
}