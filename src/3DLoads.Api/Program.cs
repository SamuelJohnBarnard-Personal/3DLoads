using System.Text.Json;
using ThreeDLoads.Application;
using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Api.Contracts;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.Enums;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationServices();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ready" }));
app.MapPost("/api/v1/solve", async (
    RunSolveRequest request,
    ISolverService solver,
    CancellationToken cancellationToken) =>
{
    var input = request.Scenario;
    if (string.IsNullOrWhiteSpace(input.Name) ||
        string.IsNullOrWhiteSpace(input.Van.Name) ||
        input.Packages.Count == 0 ||
        input.Van.Length <= 0 || input.Van.Width <= 0 || input.Van.Height <= 0 ||
        input.Van.MaxPayloadWeight <= 0)
    {
        return Results.BadRequest("A named scenario, valid van, and at least one package are required.");
    }

    var van = new Van
    {
        Id = input.Van.Id,
        Name = input.Van.Name,
        Length = input.Van.Length,
        Width = input.Van.Width,
        Height = input.Van.Height,
        MaxPayloadWeight = input.Van.MaxPayloadWeight
    };
    var scenario = new Scenario
    {
        Id = input.Id,
        Name = input.Name,
        Van = van,
        VanId = van.Id
    };
    scenario.Packages = input.Packages.Select(package => new PackageItem
    {
        Id = package.Id,
        ScenarioId = scenario.Id,
        Scenario = scenario,
        ExternalReference = package.ExternalReference,
        Length = package.Length,
        Width = package.Width,
        Height = package.Height,
        Weight = package.Weight,
        DeliveryStopIndex = package.DeliveryStopIndex,
        OrientationPolicy = (OrientationPolicy)package.OrientationPolicy,
        MaxLoadBearingWeight = package.MaxLoadBearingWeight
    }).ToList();

    var solution = await solver.SolveAsync(scenario, new SolveExecutionContext
    {
        PhaseNames = ["InitialGreedy", "Brkga", "Cleanup", "FinalValidation"],
        TimeBudget = TimeSpan.FromSeconds(5)
    }, cancellationToken);

    return Results.Ok(new SolutionResponse
    {
        Id = solution.Id,
        ScenarioId = solution.ScenarioId,
        VolumeUtilizationPercent = solution.VolumeUtilizationPercent,
        UnpackedItemCount = solution.UnpackedItemCount,
        UnpackedVolume = solution.UnpackedVolume,
        IsValid = solution.IsValid,
        ValidationErrors = JsonSerializer.Deserialize<IReadOnlyList<string>>(solution.ValidationErrorsJson) ?? [],
        Placements = solution.Placements.Select(placement => new PlacementResponse
        {
            PackageItemId = placement.PackageItemId,
            X = placement.X,
            Y = placement.Y,
            Z = placement.Z,
            OrientedLength = placement.OrientedLength,
            OrientedWidth = placement.OrientedWidth,
            OrientedHeight = placement.OrientedHeight
        }).ToList()
    });
});

app.Run();

public partial class Program;
