namespace ThreeDLoads.Domain.ValueObjects;

// Immutable 3-D coordinate: X = depth into van, Y = width, Z = height.
public record Position3D(double X, double Y, double Z);