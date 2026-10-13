namespace ThreeDLoads.Domain.Entities;

// A committed position and orientation for a package inside a Solution's van.
public class Placement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SolutionId { get; set; }
    public Solution? Solution { get; set; }
    public Guid PackageItemId { get; set; }
    public PackageItem? PackageItem { get; set; }

    // Corner closest to the van rear (origin). X = depth, Y = width, Z = height.
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    // Dimensions after rotation is applied.
    public double OrientedLength { get; set; }
    public double OrientedWidth { get; set; }
    public double OrientedHeight { get; set; }
}