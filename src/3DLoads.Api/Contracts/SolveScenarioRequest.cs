namespace ThreeDLoads.Api.Contracts;

// Optional overrides when dispatching a solve job for a scenario.
public sealed class SolveScenarioRequest
{
    // When null the default pipeline (InitialGreedy,Brkga,Cleanup) is used.
    public IReadOnlyList<string>? Phases { get; init; }
    public int? RandomSeed { get; init; }
    public int TimeBudgetSeconds { get; init; } = 20;
    public BrkgaOptions? Brkga { get; init; }
    public ConstraintOptions? Constraints { get; init; }
}

// Optional BRKGA tuning knobs for one solve run.
public sealed class BrkgaOptions
{
    public int? PopulationSize { get; init; }
    public int? MaxGenerations { get; init; }
    public double? EliteRate { get; init; }
    public double? MutantRate { get; init; }
    public double? EliteInheritanceProbability { get; init; }
    public double? PhaseBudgetRatio { get; init; }
}

// Optional geometry/stacking knobs for one solve run.
public sealed class ConstraintOptions
{
    public double? MinSupportFraction { get; init; }
}