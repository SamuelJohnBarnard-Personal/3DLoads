namespace ThreeDLoads.Domain.Enums;

// Controls how many 90-degree rotations the solver may try for a package.
public enum OrientationPolicy
{
    Fixed = 1,        // Only the declared orientation is allowed.
    ThisSideUp = 2,   // Rotate freely in the horizontal plane (Z stays up).
    Free90 = 3        // Any axis-aligned rotation is allowed.
}