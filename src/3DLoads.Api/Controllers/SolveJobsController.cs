using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ThreeDLoads.Api.Contracts;
using ThreeDLoads.Infrastructure.Persistence;

namespace ThreeDLoads.Api.Controllers;

// Polling endpoints: GET status of a job and GET the full solution once it's ready.
[ApiController]
[Route("api/v1/solve-jobs")]
public sealed class SolveJobsController(LoadsDbContext dbContext) : ControllerBase
{
    [HttpGet("{jobId:guid}")]
    [Authorize]
    public async Task<ActionResult<SolveJobResponse>> GetJob(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.SolveJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        return Ok(new SolveJobResponse
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

    [HttpGet("{jobId:guid}/result")]
    [Authorize]
    public async Task<ActionResult<SolutionResponse>> GetResult(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.SolveJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        if (job.SolutionId is null)
        {
            return Accepted(new { jobId = job.Id, status = job.Status.ToString() });
        }

        var solution = await dbContext.Solutions
            .AsNoTracking()
            .Include(x => x.Placements)
            .SingleOrDefaultAsync(x => x.Id == job.SolutionId.Value, cancellationToken);

        if (solution is null)
        {
            return NotFound();
        }

        return Ok(new SolutionResponse
        {
            Id = solution.Id,
            ScenarioId = solution.ScenarioId,
            VolumeUtilizationPercent = solution.VolumeUtilizationPercent,
            UnpackedItemCount = solution.UnpackedItemCount,
            UnpackedVolume = solution.UnpackedVolume,
            IsValid = solution.IsValid,
            ValidationErrors = ParseValidationErrors(solution.ValidationErrorsJson),
            Placements = solution.Placements.Select(p => new PlacementResponse
            {
                PackageItemId = p.PackageItemId,
                X = p.X,
                Y = p.Y,
                Z = p.Z,
                OrientedLength = p.OrientedLength,
                OrientedWidth = p.OrientedWidth,
                OrientedHeight = p.OrientedHeight
            }).ToList()
        });
    }

    private static IReadOnlyList<string> ParseValidationErrors(string? validationErrorsJson)
    {
        if (string.IsNullOrWhiteSpace(validationErrorsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(validationErrorsJson) ?? [];
        }
        catch
        {
            return ["Validation error payload could not be parsed."];
        }
    }
}