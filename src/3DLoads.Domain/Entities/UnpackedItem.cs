namespace ThreeDLoads.Domain.Entities;

// Records a package that could not be placed in the van, with the reason why.
public class UnpackedItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SolutionId { get; set; }
    public Solution? Solution { get; set; }
    public Guid PackageItemId { get; set; }
    public PackageItem? PackageItem { get; set; }
    public string Reason { get; set; } = string.Empty;
}