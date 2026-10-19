namespace ThreeDLoads.Application.Models;

// Read-only configuration for a single solve run, set by the caller when creating the job.
public sealed class SolveExecutionContext
{
    // Names of the phases to execute, in order.
    public required IReadOnlyList<string> PhaseNames { get; init; }
    public required TimeSpan TimeBudget { get; init; }
    public int? RandomSeed { get; init; }

    // Geometry / feasibility controls.
    public double MinSupportFraction { get; init; } = 1.0;

    // BRKGA controls.
    public int? BrkgaPopulationSize { get; init; }
    public int? BrkgaMaxGenerations { get; init; }
    public double? BrkgaEliteRate { get; init; }
    public double? BrkgaMutantRate { get; init; }
    public double? BrkgaEliteInheritanceProbability { get; init; }
}