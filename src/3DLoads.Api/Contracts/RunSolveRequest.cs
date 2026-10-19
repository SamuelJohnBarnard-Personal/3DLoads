namespace ThreeDLoads.Api.Contracts;

public sealed class RunSolveRequest
{
    public required ScenarioResponse Scenario { get; init; }
}