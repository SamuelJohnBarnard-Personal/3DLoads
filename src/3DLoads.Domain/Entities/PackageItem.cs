using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Domain.Entities;

// A single box to be loaded. DeliveryStopIndex determines unloading order (higher index = unloaded first).
public class PackageItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScenarioId { get; set; }
    public Scenario? Scenario { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public double Length { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Weight { get; set; }
    public int DeliveryStopIndex { get; set; }
    public OrientationPolicy OrientationPolicy { get; set; } = OrientationPolicy.Free90;
    public double MaxLoadBearingWeight { get; set; }
}