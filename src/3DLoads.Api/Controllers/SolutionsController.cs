using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ThreeDLoads.Api.Contracts;
using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Infrastructure.Persistence;

namespace ThreeDLoads.Api.Controllers;

// Endpoints scoped to persisted solutions.
[ApiController]
[Route("api/v1/solutions")]
public sealed class SolutionsController(LoadsDbContext dbContext, ISolutionValidator validator) : ControllerBase
{
    [HttpPost("{solutionId:guid}/revalidate")]
    [Authorize]
    public async Task<ActionResult<RevalidationResponse>> Revalidate(Guid solutionId, CancellationToken cancellationToken)
    {
        var solution = await dbContext.Solutions
            .Include(x => x.Scenario)
                .ThenInclude(x => x!.Van)
            .Include(x => x.Scenario)
                .ThenInclude(x => x!.Packages)
            .Include(x => x.Placements)
                .ThenInclude(x => x.PackageItem)
            .Include(x => x.UnpackedItems)
                .ThenInclude(x => x.PackageItem)
            .SingleOrDefaultAsync(x => x.Id == solutionId, cancellationToken);

        if (solution is null || solution.Scenario is null || solution.Scenario.Van is null)
        {
            return NotFound();
        }

        var state = BuildStateFromSolution(solution);
        var report = validator.Validate(state, cancellationToken);

        solution.IsValid = report.IsValid;
        solution.ValidationErrorsJson = JsonSerializer.Serialize(report.Errors);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new RevalidationResponse
        {
            SolutionId = solution.Id,
            IsValid = report.IsValid,
            ValidationErrors = report.Errors,
            ValidatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static SolutionState BuildStateFromSolution(Solution solution)
    {
        var state = new SolutionState
        {
            Scenario = solution.Scenario!
        };

        foreach (var placement in solution.Placements)
        {
            state.Placements.Add(new Placement
            {
                Id = placement.Id,
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

        foreach (var unpacked in solution.UnpackedItems)
        {
            var package = unpacked.PackageItem
                          ?? solution.Scenario!.Packages.FirstOrDefault(x => x.Id == unpacked.PackageItemId);
            if (package is not null)
            {
                state.UnpackedItems.Add(package);
            }
        }

        return state;
    }
}