using ThreeDLoads.Application.Models;

namespace ThreeDLoads.Application.Services.Phases.Brkga;

public sealed partial class BrkgaPhase
{
    private static bool ShouldSkip(SolutionState state)
    {
        return state.Scenario.Van is null || state.Scenario.Packages.Count == 0;
    }

    private static int GetPopulationSize(SolveExecutionContext context, int packageCount)
    {
        return Math.Clamp(context.BrkgaPopulationSize ?? packageCount * 6, 30, 400);
    }

    private static double GetEliteRate(SolveExecutionContext context)
    {
        return Math.Clamp(context.BrkgaEliteRate ?? 0.20, 0.05, 0.60);
    }

    private static double GetMutantRate(SolveExecutionContext context)
    {
        return Math.Clamp(context.BrkgaMutantRate ?? 0.10, 0.02, 0.40);
    }

    private static double GetEliteInheritanceProbability(SolveExecutionContext context)
    {
        return Math.Clamp(context.BrkgaEliteInheritanceProbability ?? 0.70, 0.50, 0.95);
    }

    private static int GetEliteCount(int populationSize, double eliteRate)
    {
        return Math.Max(1, (int)Math.Ceiling(populationSize * eliteRate));
    }

    private static int GetMutantCount(int populationSize, double mutantRate)
    {
        return Math.Max(1, (int)Math.Ceiling(populationSize * mutantRate));
    }

    private static int GetMaxGenerations(SolveExecutionContext context)
    {
        return Math.Clamp(context.BrkgaMaxGenerations ?? 80, 5, 1000);
    }

    private static (DateTimeOffset PhaseStart, int UnpackedCount, double VolumeUtilizationPercent)
        CapturePhaseStartMetrics(SolutionState state)
    {
        return (
            DateTimeOffset.UtcNow,
            state.UnpackedItems.Count,
            CalculateVolumeUtilizationPercent(state));
    }

    private static double CalculateVolumeUtilizationPercent(SolutionState state)
    {
        if (state.Scenario.Van is null)
        {
            return 0;
        }

        var vanVolume = state.Scenario.Van.Length * state.Scenario.Van.Width * state.Scenario.Van.Height;
        if (vanVolume <= 0)
        {
            return 0;
        }

        var packedVolume = state.Placements.Sum(p => p.OrientedLength * p.OrientedWidth * p.OrientedHeight);
        return (packedVolume / vanVolume) * 100.0;
    }
}