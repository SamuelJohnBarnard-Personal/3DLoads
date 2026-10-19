namespace ThreeDLoads.Api.Contracts;

// Packing result returned directly by POST /api/v1/solve.
public sealed class SolutionResponse
{
    public Guid Id { get; init; }
    public Guid ScenarioId { get; init; }
    public double VolumeUtilizationPercent { get; init; }
    public int UnpackedItemCount { get; init; }
    public double UnpackedVolume { get; init; }
    public bool IsValid { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    public IReadOnlyList<PlacementResponse> Placements { get; init; } = [];
}

// Position and oriented dimensions of one packed box.
public sealed class PlacementResponse
{
    public Guid PackageItemId { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public double OrientedLength { get; init; }
    public double OrientedWidth { get; init; }
    public double OrientedHeight { get; init; }
}