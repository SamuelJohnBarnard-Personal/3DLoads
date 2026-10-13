using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDLoads.Api.Contracts;
using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Infrastructure.Persistence;

namespace ThreeDLoads.Api.Controllers;

// CRUD for Scenarios plus the /solve action that queues an async solve job.
[ApiController]
[Route("api/v1/scenarios")]
public sealed class ScenariosController(LoadsDbContext dbContext, ISolveJobQueue queue) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ScenarioSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var scenarios = await dbContext.Scenarios
            .AsNoTracking()
            .Include(x => x.Van)
            .Include(x => x.Packages)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ScenarioSummaryResponse
            {
                Id = x.Id,
                Name = x.Name,
                VanName = x.Van!.Name,
                PackageCount = x.Packages.Count,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(scenarios);
    }

    [HttpGet("{scenarioId:guid}")]
    [Authorize]
    public async Task<ActionResult<ScenarioResponse>> Get(Guid scenarioId, CancellationToken cancellationToken)
    {
        var scenario = await dbContext.Scenarios
            .AsNoTracking()
            .Include(x => x.Van)
            .Include(x => x.Packages)
            .SingleOrDefaultAsync(x => x.Id == scenarioId, cancellationToken);

        return scenario is null ? NotFound() : Ok(MapScenario(scenario));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ScenarioSummaryResponse>> Create(CreateScenarioRequest request, CancellationToken cancellationToken)
    {
        if (request.Packages.Count == 0)
        {
            return BadRequest("At least one package is required.");
        }

        var van = new Van
        {
            Name = request.Van.Name,
            Length = request.Van.Length,
            Width = request.Van.Width,
            Height = request.Van.Height,
            MaxPayloadWeight = request.Van.MaxPayloadWeight
        };

        var scenario = new Scenario
        {
            Name = request.Name,
            Van = van,
            VanId = van.Id,
            Packages = request.Packages.Select(MapPackage).ToList()
        };

        dbContext.Scenarios.Add(scenario);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new ScenarioSummaryResponse
        {
            Id = scenario.Id,
            Name = scenario.Name,
            VanName = van.Name,
            PackageCount = scenario.Packages.Count,
            CreatedAtUtc = scenario.CreatedAtUtc
        };

        return CreatedAtAction(nameof(Get), new { scenarioId = scenario.Id }, response);
    }

    [HttpPut("{scenarioId:guid}")]
    [Authorize]
    public async Task<ActionResult<ScenarioSummaryResponse>> Update(
        Guid scenarioId,
        CreateScenarioRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Packages.Count == 0)
        {
            return BadRequest("At least one package is required.");
        }

        var scenario = await dbContext.Scenarios
            .Include(x => x.Van)
            .Include(x => x.Packages)
            .SingleOrDefaultAsync(x => x.Id == scenarioId, cancellationToken);

        if (scenario is null || scenario.Van is null)
        {
            return NotFound();
        }

        var hasSolveHistory = await dbContext.SolveJobs
            .AsNoTracking()
            .AnyAsync(x => x.ScenarioId == scenarioId, cancellationToken)
            || await dbContext.Solutions
                .AsNoTracking()
                .AnyAsync(x => x.ScenarioId == scenarioId, cancellationToken);

        if (hasSolveHistory)
        {
            var replacementVan = new Van
            {
                Name = request.Van.Name,
                Length = request.Van.Length,
                Width = request.Van.Width,
                Height = request.Van.Height,
                MaxPayloadWeight = request.Van.MaxPayloadWeight
            };

            var replacementScenario = new Scenario
            {
                Name = request.Name,
                Van = replacementVan,
                VanId = replacementVan.Id,
                Packages = request.Packages.Select(MapPackage).ToList()
            };

            dbContext.Scenarios.Add(replacementScenario);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new ScenarioSummaryResponse
            {
                Id = replacementScenario.Id,
                Name = replacementScenario.Name,
                VanName = replacementVan.Name,
                PackageCount = replacementScenario.Packages.Count,
                CreatedAtUtc = replacementScenario.CreatedAtUtc
            });
        }

        scenario.Name = request.Name;
        scenario.Van.Name = request.Van.Name;
        scenario.Van.Length = request.Van.Length;
        scenario.Van.Width = request.Van.Width;
        scenario.Van.Height = request.Van.Height;
        scenario.Van.MaxPayloadWeight = request.Van.MaxPayloadWeight;

        var existingPackages = scenario.Packages
            .OrderBy(x => x.DeliveryStopIndex)
            .ThenBy(x => x.ExternalReference)
            .ToList();

        var sharedCount = Math.Min(existingPackages.Count, request.Packages.Count);
        for (var index = 0; index < sharedCount; index++)
        {
            ApplyPackage(existingPackages[index], request.Packages[index]);
        }

        if (request.Packages.Count > existingPackages.Count)
        {
            foreach (var package in request.Packages.Skip(existingPackages.Count))
            {
                scenario.Packages.Add(MapPackage(package));
            }
        }

        if (existingPackages.Count > request.Packages.Count)
        {
            var packagesToRemove = existingPackages.Skip(request.Packages.Count).ToList();
            var packageIds = packagesToRemove.Select(x => x.Id).ToArray();

            var hasHistoricalReferences = await dbContext.Placements
                .AnyAsync(x => packageIds.Contains(x.PackageItemId), cancellationToken)
                || await dbContext.UnpackedItems
                    .AnyAsync(x => packageIds.Contains(x.PackageItemId), cancellationToken);

            if (hasHistoricalReferences)
            {
                return Conflict("Cannot remove packages from a scenario that already has solve history. Create a new scenario instead.");
            }

            dbContext.PackageItems.RemoveRange(packagesToRemove);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ScenarioSummaryResponse
        {
            Id = scenario.Id,
            Name = scenario.Name,
            VanName = scenario.Van.Name,
            PackageCount = request.Packages.Count,
            CreatedAtUtc = scenario.CreatedAtUtc
        });
    }

    [HttpPost("{scenarioId:guid}/solve")]
    [Authorize]
    public async Task<ActionResult<SolveJobResponse>> Solve(Guid scenarioId, SolveScenarioRequest request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Scenarios.AnyAsync(x => x.Id == scenarioId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var phases = request.Phases is { Count: > 0 }
            ? string.Join(',', request.Phases)
            : "InitialGreedy,Brkga,Cleanup,FinalValidation";

        var job = new SolveJob
        {
            ScenarioId = scenarioId,
            RequestedPhases = phases,
            RandomSeed = request.RandomSeed,
            TimeBudgetSeconds = Math.Clamp(request.TimeBudgetSeconds, 1, 300),
            MinSupportFraction = Math.Clamp(request.Constraints?.MinSupportFraction ?? 1.0, 0.1, 1.0),
            BrkgaPopulationSize = request.Brkga?.PopulationSize,
            BrkgaMaxGenerations = request.Brkga?.MaxGenerations,
            BrkgaEliteRate = request.Brkga?.EliteRate,
            BrkgaMutantRate = request.Brkga?.MutantRate,
            BrkgaEliteInheritanceProbability = request.Brkga?.EliteInheritanceProbability,
            BrkgaPhaseBudgetRatio = request.Brkga?.PhaseBudgetRatio
        };

        dbContext.SolveJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        await queue.QueueAsync(job.Id, cancellationToken);

        return AcceptedAtAction(
            nameof(SolveJobsController.GetJob),
            "SolveJobs",
            new { jobId = job.Id },
            new SolveJobResponse
            {
                Id = job.Id,
                ScenarioId = job.ScenarioId,
                Status = job.Status,
                SolutionId = job.SolutionId,
                ErrorMessage = job.ErrorMessage,
                CreatedAtUtc = job.CreatedAtUtc,
                StartedAtUtc = job.StartedAtUtc,
                CompletedAtUtc = job.CompletedAtUtc
            });
    }

    private static PackageItem MapPackage(PackageRequest package)
    {
        return new PackageItem
        {
            ExternalReference = package.ExternalReference,
            Length = package.Length,
            Width = package.Width,
            Height = package.Height,
            Weight = package.Weight,
            DeliveryStopIndex = package.DeliveryStopIndex,
            OrientationPolicy = package.OrientationPolicy,
            MaxLoadBearingWeight = package.MaxLoadBearingWeight
        };
    }

    private static void ApplyPackage(PackageItem target, PackageRequest source)
    {
        target.ExternalReference = source.ExternalReference;
        target.Length = source.Length;
        target.Width = source.Width;
        target.Height = source.Height;
        target.Weight = source.Weight;
        target.DeliveryStopIndex = source.DeliveryStopIndex;
        target.OrientationPolicy = source.OrientationPolicy;
        target.MaxLoadBearingWeight = source.MaxLoadBearingWeight;
    }

    private static ScenarioResponse MapScenario(Scenario scenario)
    {
        return new ScenarioResponse
        {
            Id = scenario.Id,
            Name = scenario.Name,
            VanId = scenario.VanId,
            CreatedAtUtc = scenario.CreatedAtUtc,
            Van = scenario.Van is null
                ? new VanResponse()
                : new VanResponse
                {
                    Id = scenario.Van.Id,
                    Name = scenario.Van.Name,
                    Length = scenario.Van.Length,
                    Width = scenario.Van.Width,
                    Height = scenario.Van.Height,
                    MaxPayloadWeight = scenario.Van.MaxPayloadWeight
                },
            Packages = scenario.Packages
                .OrderBy(x => x.DeliveryStopIndex)
                .ThenBy(x => x.ExternalReference)
                .Select(x => new PackageItemResponse
                {
                    Id = x.Id,
                    ExternalReference = x.ExternalReference,
                    Length = x.Length,
                    Width = x.Width,
                    Height = x.Height,
                    Weight = x.Weight,
                    DeliveryStopIndex = x.DeliveryStopIndex,
                    OrientationPolicy = (int)x.OrientationPolicy,
                    MaxLoadBearingWeight = x.MaxLoadBearingWeight
                })
                .ToList()
        };
    }
}