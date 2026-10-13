using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Api.Contracts;

// Request body for POST /api/v1/scenarios. A new Van is always created alongside the scenario.
public sealed class CreateScenarioRequest
{
    public required string Name { get; init; }
    public required VanRequest Van { get; init; }
    public required IReadOnlyList<PackageRequest> Packages { get; init; }
}

public sealed class VanRequest
{
    public required string Name { get; init; }
    public double Length { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double MaxPayloadWeight { get; init; }
}

public sealed class PackageRequest
{
    public required string ExternalReference { get; init; }
    public double Length { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Weight { get; init; }
    public int DeliveryStopIndex { get; init; }
    public OrientationPolicy OrientationPolicy { get; init; }
    public double MaxLoadBearingWeight { get; init; }
}