namespace ThreeDLoads.Domain.Entities;

// Performance snapshot captured before and after a single solver phase executes.
public class PhaseMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SolutionId { get; set; }
    public Solution? Solution { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public double BeforeVolumeUtilizationPercent { get; set; }
    public double AfterVolumeUtilizationPercent { get; set; }
    public int BeforeUnpackedCount { get; set; }
    public int AfterUnpackedCount { get; set; }
    public string TelemetryJson { get; set; } = "{}";
}