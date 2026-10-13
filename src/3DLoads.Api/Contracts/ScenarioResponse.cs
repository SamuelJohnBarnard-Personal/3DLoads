namespace ThreeDLoads.Api.Contracts;

public sealed class ScenarioResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid VanId { get; init; }
    public VanResponse Van { get; init; } = new();
    public IReadOnlyList<PackageItemResponse> Packages { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class VanResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public double Length { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double MaxPayloadWeight { get; init; }
}

public sealed class PackageItemResponse
{
    public Guid Id { get; init; }
    public string ExternalReference { get; init; } = string.Empty;
    public double Length { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Weight { get; init; }
    public int DeliveryStopIndex { get; init; }
    public int OrientationPolicy { get; init; }
    public double MaxLoadBearingWeight { get; init; }
}