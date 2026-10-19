using System.Diagnostics;
using System.Text.Json;
using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Application.Services.Phases.Brkga;

// BRKGA orchestration. Supporting configuration, chromosome, decoding, and scoring logic
// is separated into the other BrkgaPhase partial files.
public sealed partial class BrkgaPhase(
    IOrientationEnumerator orientationEnumerator,
    IGeometryFeasibilityChecker checker,
    IExtremePointAnchorEngine anchorEngine) : IPhase
{
    private const double Epsilon = 1e-6;

    public string Name => "Brkga";

    public Task<SolutionState> ExecuteAsync(SolutionState state, SolveExecutionContext context, CancellationToken cancellationToken)
    {
        // Skip scenarios that cannot produce a meaningful packing solution.
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        // Capture the incoming state so the phase can report its effect later.
        var (phaseStart, beforeUnpacked, beforeUtilization) = CapturePhaseStartMetrics(state);

        // Create stable package indexes and cache every legal orientation once.
        var packageList = state.Scenario.Packages.ToList();

        // Map each package ID to its array position so its chromosome keys can be retrieved.
        var packageIndex = packageList
            .Select((p, i) => new { p.Id, Index = i })
            .ToDictionary(x => x.Id, x => x.Index);

        // creates a dictionary mapping each package ID to its list of legal orientations.
        var orientationMap = packageList.ToDictionary(
            p => p.Id,
            p => orientationEnumerator.Enumerate(p));

        // Group stops from deepest to nearest the doors. BRKGA may reorder items
        // inside a group, but this outer order remains fixed.
        var stopGroups = packageList
            .GroupBy(p => p.DeliveryStopIndex)
            .OrderByDescending(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        // Set genetic algorithm parameters.
        var seed = context.RandomSeed ?? Environment.TickCount;
        var random = new Random(seed);

        var populationSize = GetPopulationSize(context, packageList.Count); // Get the population size for the genetic algorithm i.e. number of chromosomes in the population.
        var eliteRate = GetEliteRate(context); // Proportion of the population considered elite.
        var mutantRate = GetMutantRate(context); // Proportion of the population replaced by mutants.
        var eliteInheritanceProbability = GetEliteInheritanceProbability(context); // Probability that an offspring inherits a gene from an elite parent.
        var eliteCount = GetEliteCount(populationSize, eliteRate); // Number of elite chromosomes in the population.
        var mutantCount = GetMutantCount(populationSize, mutantRate); // Number of mutant chromosomes in the population.
        var maxGenerations = GetMaxGenerations(context); // Maximum number of generations to evolve.

        // Keep BRKGA bounded to a fixed 10-second search.
        var phaseBudget = TimeSpan.FromSeconds(10);
        var stopwatch = Stopwatch.StartNew();

        // Creates two heuristic chromosomes, and the rest of the population size is filled with random chromosomes.
        var population = BuildInitialPopulation(
            state,
            packageList,
            packageIndex,
            orientationMap,
            populationSize,
            random);

        var baselineScore = Score(state); //sets score equal to that of greedy algorithm, if exists
        var bestState = CloneState(state);
        var bestScore = baselineScore;
        var noImprovementStreak = 0;
        var generationTelemetry = new List<GenerationTelemetry>(capacity: Math.Min(maxGenerations, 500));

        // repeatedly evaluate and evolve the population until the generation limit,
        // time budget, or cancellation token stops the search.
        for (var generation = 0; generation < maxGenerations; generation++) //essentially the number of iterations
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stopwatch.Elapsed >= phaseBudget)
            {
                break;
            }

            // decode every chromosome into a complete feasible placement attempt,
            // then rank candidates by their score.
            var evaluated = population
                .Select(chromosome =>
                {
                    var decoded = Decode(chromosome, state.Scenario, stopGroups, packageIndex, orientationMap, context);
                    var score = Score(decoded); //calculate score based on various parameters
                    return new CandidateSolution(chromosome, decoded, score);
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            var generationBestScore = evaluated[0].Score;
            var generationAverageScore = evaluated.Average(x => x.Score);
            var generationDiversity = ComputeDiversity(evaluated.Select(x => x.Chromosome));

            // retain the best state found across all generations, not only this one.
            if (generationBestScore > bestScore)
            {
                bestScore = generationBestScore;
                bestState = evaluated[0].State;
                noImprovementStreak = 0;
            }
            else
            {
                noImprovementStreak++;
            }

            generationTelemetry.Add(new GenerationTelemetry(
                Generation: generation,
                BestScore: generationBestScore,
                AverageScore: generationAverageScore,
                Diversity: generationDiversity,
                NoImprovementStreak: noImprovementStreak));

            // preserve the elite chromosomes, inject random mutants, and create
            // the rest of the next generation through elite-biased crossover.
            var elites = evaluated.Take(eliteCount).Select(x => x.Chromosome).ToList();
            var nonElites = evaluated.Skip(eliteCount).Select(x => x.Chromosome).ToList();
            if (nonElites.Count == 0)
            {
                nonElites = elites;
            }

            var nextPopulation = new List<Chromosome>(populationSize);

            // Elite chromosomes stay the same.
            foreach (var elite in elites)
            {
                nextPopulation.Add(elite);
            }

            // (new chromosomes) Mutants inject diversity and help avoid premature convergence.
            for (var i = 0; i < mutantCount && nextPopulation.Count < populationSize; i++)
            {
                nextPopulation.Add(CreateRandomChromosome(packageList.Count, random));
            }

            // Remaining slots are filled by biased crossover (elite parent dominates).
            while (nextPopulation.Count < populationSize)
            {
                var eliteParent = elites[random.Next(elites.Count)];
                var otherParent = nonElites[random.Next(nonElites.Count)];
                nextPopulation.Add(Crossover(eliteParent, otherParent, random, eliteInheritanceProbability));
            }

            population = nextPopulation;
        }

        // commit the search result only when it beats the incoming state. This
        // prevents a time-limited or unlucky run from making the solution worse.
        if (bestScore > baselineScore)
        {
            state.Placements.Clear();
            state.Placements.AddRange(bestState.Placements);

            state.UnpackedItems.Clear();
            state.UnpackedItems.AddRange(bestState.UnpackedItems);
        }

        // record summary statistics and per-generation telemetry for later tuning.
        state.PhaseMetrics.Add(new PhaseMetric
        {
            PhaseName = Name,
            ElapsedMs = (long)(DateTimeOffset.UtcNow - phaseStart).TotalMilliseconds,
            BeforeUnpackedCount = beforeUnpacked,
            AfterUnpackedCount = state.UnpackedItems.Count,
            BeforeVolumeUtilizationPercent = beforeUtilization,
            AfterVolumeUtilizationPercent = CalculateVolumeUtilizationPercent(state),
            TelemetryJson = JsonSerializer.Serialize(new BrkgaPhaseTelemetry(
                Seed: seed,
                PopulationSize: populationSize,
                EliteRate: eliteRate,
                MutantRate: mutantRate,
                EliteInheritanceProbability: eliteInheritanceProbability,
                MaxGenerations: maxGenerations,
                ExecutedGenerations: generationTelemetry.Count,
                PhaseBudgetSeconds: phaseBudget.TotalSeconds,
                FinalBestScore: bestScore,
                Generations: generationTelemetry))
        });

        return Task.FromResult(state);
    }
}