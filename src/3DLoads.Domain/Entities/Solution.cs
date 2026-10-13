namespace ThreeDLoads.Domain.Entities;

// The result produced by a completed solve job, including all package positions and summary statistics.
public class Solution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScenarioId { get; set; }
    public Scenario? Scenario { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public double VolumeUtilizationPercent { get; set; }
    public int UnpackedItemCount { get; set; }
    public double UnpackedVolume { get; set; }
    public bool IsValid { get; set; } = true;
    public string ValidationErrorsJson { get; set; } = "[]";
    public ICollection<Placement> Placements { get; set; } = new List<Placement>();
    public ICollection<UnpackedItem> UnpackedItems { get; set; } = new List<UnpackedItem>();
    public ICollection<PhaseMetric> PhaseMetrics { get; set; } = new List<PhaseMetric>();
}