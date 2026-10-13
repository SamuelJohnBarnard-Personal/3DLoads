namespace ThreeDLoads.Domain.ValueObjects;

// Immutable box size in the van's coordinate space (all values in the same unit, e.g. cm).
public record Dimensions3D(double Length, double Width, double Height)
{
    public double Volume => Length * Width * Height;
}