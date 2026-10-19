using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Application.Services.Phases.Brkga;

public sealed partial class BrkgaPhase
{
    private static double Score(SolutionState state)
    {
        if (state.Scenario.Van is null)
        {
            return double.NegativeInfinity;
        }

        var unpackedVolume = state.UnpackedItems.Sum(p => p.Length * p.Width * p.Height);
        var unpackedCount = state.UnpackedItems.Count;
        var packedVolume = state.Placements.Sum(p => p.OrientedLength * p.OrientedWidth * p.OrientedHeight);
        var van = state.Scenario.Van;
        var targetX = van.Length / 2.0;
        var targetY = van.Width / 2.0;
        var totalWeight = 0.0;
        var weightedX = 0.0;
        var weightedY = 0.0;

        foreach (var placement in state.Placements)
        {
            var weight = placement.PackageItem?.Weight ?? 0;
            if (weight <= 0)
            {
                continue;
            }

            weightedX += (placement.X + placement.OrientedLength / 2.0) * weight;
            weightedY += (placement.Y + placement.OrientedWidth / 2.0) * weight;
            totalWeight += weight;
        }

        var balancePenalty = 0.0;
        if (totalWeight > 0)
        {
            var meanX = weightedX / totalWeight;
            var meanY = weightedY / totalWeight;
            balancePenalty = Math.Abs(meanX - targetX) + Math.Abs(meanY - targetY);
        }

        return packedVolume - unpackedVolume * 1_000_000.0 - unpackedCount * 100_000.0 - balancePenalty * 0.01;
    }

    private static SolutionState CloneState(SolutionState source)
    {
        var clone = new SolutionState { Scenario = source.Scenario };
        foreach (var placement in source.Placements)
        {
            clone.Placements.Add(new Placement
            {
                PackageItemId = placement.PackageItemId,
                PackageItem = placement.PackageItem,
                X = placement.X,
                Y = placement.Y,
                Z = placement.Z,
                OrientedLength = placement.OrientedLength,
                OrientedWidth = placement.OrientedWidth,
                OrientedHeight = placement.OrientedHeight
            });
        }

        clone.UnpackedItems.AddRange(source.UnpackedItems);
        return clone;
    }

    private static double ComputeDiversity(IEnumerable<Chromosome> chromosomes)
    {
        var values = chromosomes
            .SelectMany(chromosome => chromosome.PriorityKeys.Concat(chromosome.OrientationKeys))
            .ToArray();
        if (values.Length == 0)
        {
            return 0;
        }

        var mean = values.Average();
        var variance = values.Select(value => (value - mean) * (value - mean)).Average();
        return Math.Sqrt(variance);
    }

    private sealed record CandidateSolution(Chromosome Chromosome, SolutionState State, double Score);

    private sealed record GenerationTelemetry(
        int Generation,
        double BestScore,
        double AverageScore,
        double Diversity,
        int NoImprovementStreak);

    private sealed record BrkgaPhaseTelemetry(
        int Seed,
        int PopulationSize,
        double EliteRate,
        double MutantRate,
        double EliteInheritanceProbability,
        int MaxGenerations,
        int ExecutedGenerations,
        double PhaseBudgetSeconds,
        double FinalBestScore,
        IReadOnlyList<GenerationTelemetry> Generations);
}