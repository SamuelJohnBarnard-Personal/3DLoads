namespace ThreeDLoads.Domain.Entities;

// The cargo space being packed. Length = depth from rear to cab, Width = side to side, Height = floor to roof.
public class Van
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public double Length { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double MaxPayloadWeight { get; set; }
}