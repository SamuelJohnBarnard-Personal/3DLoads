using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Enums;
using ThreeDLoads.Infrastructure.Persistence;

namespace ThreeDLoads.Infrastructure.Background;

// Long-running worker that dequeues solve job IDs, loads the scenario from the database,
// runs the solver pipeline, and persists the resulting Solution. Each job runs in its own
// DI scope so the scoped DbContext and ISolverService are isolated per job.
public sealed class SolveJobBackgroundService(
    IServiceScopeFactory scopeFactory,
    ISolveJobQueue queue,
    ILogger<SolveJobBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var jobId = await queue.DequeueAsync(stoppingToken);

            try
            {
                await ProcessJob(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled solve job processing failure for {JobId}", jobId);
            }
        }
    }

    private async Task ProcessJob(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LoadsDbContext>();
        var solver = scope.ServiceProvider.GetRequiredService<ISolverService>();

        var job = await db.SolveJobs
            .SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            return;
        }

        var scenario = await db.Scenarios
            .Include(x => x.Van)
            .Include(x => x.Packages)
            .SingleOrDefaultAsync(x => x.Id == job.ScenarioId, cancellationToken);

        if (scenario is null)
        {
            job.Status = SolveJobStatus.Failed;
            job.ErrorMessage = "Scenario not found";
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        job.Status = SolveJobStatus.Running;
        job.StartedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var context = new SolveExecutionContext
            {
                TimeBudget = TimeSpan.FromSeconds(job.TimeBudgetSeconds),
                RandomSeed = job.RandomSeed,
            // Parse the comma-separated phase list stored on the job.
                PhaseNames = job.RequestedPhases
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            };

            var solution = await solver.SolveAsync(scenario, context, cancellationToken);
            db.Solutions.Add(solution);
            await db.SaveChangesAsync(cancellationToken);

            job.SolutionId = solution.Id;
            job.Status = SolveJobStatus.Completed;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            job.Status = SolveJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}