namespace ThreeDLoads.Application.Models;

// A 3-D coordinate that a package's lower-left-front corner may be placed at.
public readonly record struct AnchorPoint(double X, double Y, double Z);