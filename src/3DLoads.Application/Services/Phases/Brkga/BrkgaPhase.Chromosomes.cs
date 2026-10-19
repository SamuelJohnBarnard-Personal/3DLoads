using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Services.Phases.Brkga;

public sealed partial class BrkgaPhase
{
    private sealed record Chromosome(double[] PriorityKeys, double[] OrientationKeys);

    private List<Chromosome> BuildInitialPopulation(
        SolutionState inputState,
        IReadOnlyList<PackageItem> packages,
        IReadOnlyDictionary<Guid, int> packageIndex,
        IReadOnlyDictionary<Guid, IReadOnlyList<Dimensions3D>> orientationMap,
        int populationSize,
        Random random)
    {
        var heuristicSeed = BuildSeedChromosome(inputState, packages, packageIndex, orientationMap); //creates one seed based on greedy
        var population = new List<Chromosome>(populationSize)
        {
            heuristicSeed,
            CreateRandomOrientationSeed(heuristicSeed, random) //keeps greedy insertion order while randomizing orientations
        };

        while (population.Count < populationSize)
        {
            population.Add(CreateRandomChromosome(packages.Count, random));
        }

        return population;
    }

    private static Chromosome CreateRandomOrientationSeed(Chromosome seed, Random random)
    {
        var orientationKeys = new double[seed.OrientationKeys.Length];
        for (var i = 0; i < orientationKeys.Length; i++)
        {
            orientationKeys[i] = random.NextDouble();
        }

        return new Chromosome(seed.PriorityKeys.ToArray(), orientationKeys);
    }

    private static Chromosome CreateRandomChromosome(int geneCount, Random random)
    {
        var priorityKeys = new double[geneCount];
        var orientationKeys = new double[geneCount];

        for (var i = 0; i < geneCount; i++)
        {
            priorityKeys[i] = random.NextDouble();
            orientationKeys[i] = random.NextDouble();
        }

        return new Chromosome(priorityKeys, orientationKeys);
    }

    private static Chromosome Crossover(
        Chromosome eliteParent,
        Chromosome otherParent,
        Random random,
        double eliteInheritanceProbability)
    {
        var priorityKeys = new double[eliteParent.PriorityKeys.Length];
        var orientationKeys = new double[eliteParent.OrientationKeys.Length];

        for (var i = 0; i < priorityKeys.Length; i++)
        {
            priorityKeys[i] = random.NextDouble() <= eliteInheritanceProbability
                ? eliteParent.PriorityKeys[i]
                : otherParent.PriorityKeys[i];

            orientationKeys[i] = random.NextDouble() <= eliteInheritanceProbability
                ? eliteParent.OrientationKeys[i]
                : otherParent.OrientationKeys[i];
        }

        return new Chromosome(priorityKeys, orientationKeys);
    }

    private static Chromosome BuildSeedChromosome(
        SolutionState inputState,
        IReadOnlyList<PackageItem> packages,
        IReadOnlyDictionary<Guid, int> packageIndex,
        IReadOnlyDictionary<Guid, IReadOnlyList<Dimensions3D>> orientationMap)
    {
        var priorityKeys = new double[packages.Count];
        var orientationKeys = new double[packages.Count];
        var groups = packages
            .GroupBy(p => p.DeliveryStopIndex)
            .OrderByDescending(g => g.Key);

        foreach (var group in groups)
        {
            var ordered = group
                .OrderByDescending(p => p.Length * p.Width * p.Height)
                .ToList();

            for (var rank = 0; rank < ordered.Count; rank++)
            {
                var item = ordered[rank];
                var index = packageIndex[item.Id];
                priorityKeys[index] = (double)rank / Math.Max(1, ordered.Count - 1);

                var placed = inputState.Placements.FirstOrDefault(p => p.PackageItemId == item.Id);
                if (placed?.PackageItem is null)
                {
                    orientationKeys[index] = 0;
                    continue;
                }

                var orientations = orientationMap[item.Id];
                var matchedIndex = IndexOfOrientation(
                    orientations,
                    placed.OrientedLength,
                    placed.OrientedWidth,
                    placed.OrientedHeight);
                orientationKeys[index] = orientations.Count == 0
                    ? 0
                    : (double)matchedIndex / Math.Max(1, orientations.Count - 1);
            }
        }

        return new Chromosome(priorityKeys, orientationKeys);
    }

    private static int IndexOfOrientation(
        IReadOnlyList<Dimensions3D> orientations,
        double length,
        double width,
        double height)
    {
        for (var i = 0; i < orientations.Count; i++)
        {
            var candidate = orientations[i];
            if (Math.Abs(candidate.Length - length) < Epsilon &&
                Math.Abs(candidate.Width - width) < Epsilon &&
                Math.Abs(candidate.Height - height) < Epsilon)
            {
                return i;
            }
        }

        return 0;
    }
}