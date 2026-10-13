namespace ThreeDLoads.Domain.Entities;

// A named packing problem: one van plus all packages that must be loaded.
public class Scenario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid VanId { get; set; }
    public Van? Van { get; set; }
    public ICollection<PackageItem> Packages { get; set; } = new List<PackageItem>();
}