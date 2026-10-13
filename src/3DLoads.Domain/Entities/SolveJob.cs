using ThreeDLoads.Domain.Enums;

namespace ThreeDLoads.Domain.Entities;

// An async work item that triggers and tracks a solve run for a scenario.
public class SolveJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScenarioId { get; set; }
    public Scenario? Scenario { get; set; }
    public SolveJobStatus Status { get; set; } = SolveJobStatus.Queued;
    // Comma-separated list of phase names to execute in order.
    public string RequestedPhases { get; set; } = "InitialGreedy,Brkga,Cleanup,FinalValidation";
    public int? RandomSeed { get; set; }
    public int TimeBudgetSeconds { get; set; } = 20;
    public double MinSupportFraction { get; set; } = 1.0;
    public int? BrkgaPopulationSize { get; set; }
    public int? BrkgaMaxGenerations { get; set; }
    public double? BrkgaEliteRate { get; set; }
    public double? BrkgaMutantRate { get; set; }
    public double? BrkgaEliteInheritanceProbability { get; set; }
    public double? BrkgaPhaseBudgetRatio { get; set; }
    public Guid? SolutionId { get; set; }
    public Solution? Solution { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}