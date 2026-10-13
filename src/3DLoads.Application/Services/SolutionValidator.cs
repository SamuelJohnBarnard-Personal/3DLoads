using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Models;
using ThreeDLoads.Domain.Entities;
using ThreeDLoads.Domain.ValueObjects;

namespace ThreeDLoads.Application.Services;

// Shared validator used by the final pipeline phase and API revalidation endpoint.
public sealed class SolutionValidator(IGeometryFeasibilityChecker checker) : ISolutionValidator
{
    private const double Epsilon = 1e-6;

    public ValidationReport Validate(SolutionState state, CancellationToken cancellationToken, SolveExecutionContext? context = null)
    {
        var errors = new List<string>();

        ValidateAccounting(state, errors);
        ValidateLifoDepthBoundaries(state, errors);
        ValidatePlacementFeasibility(state, errors, cancellationToken, context);

        return new ValidationReport
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static void ValidateAccounting(SolutionState state, ICollection<string> errors)
    {
        var scenarioIds = state.Scenario.Packages.Select(x => x.Id).ToHashSet();

        var duplicatePlaced = state.Placements
            .GroupBy(x => x.PackageItemId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        foreach (var id in duplicatePlaced)
        {
            errors.Add($"Package {id} appears multiple times in placements.");
        }

        var placedIds = state.Placements.Select(x => x.PackageItemId).ToHashSet();
        var duplicateUnpacked = state.UnpackedItems
            .GroupBy(x => x.Id)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        foreach (var id in duplicateUnpacked)
        {
            errors.Add($"Package {id} appears multiple times in unpacked items.");
        }

        foreach (var id in placedIds)
        {
            if (!scenarioIds.Contains(id))
            {
                errors.Add($"Placed package {id} is not part of the scenario.");
            }
        }

        foreach (var package in state.UnpackedItems)
        {
            if (!scenarioIds.Contains(package.Id))
            {
                errors.Add($"Unpacked package {package.Id} is not part of the scenario.");
            }

            if (placedIds.Contains(package.Id))
            {
                errors.Add($"Package {package.Id} is both placed and unpacked.");
            }
        }

        var represented = new HashSet<Guid>(placedIds);
        represented.UnionWith(state.UnpackedItems.Select(x => x.Id));
        foreach (var package in state.Scenario.Packages)
        {
            if (!represented.Contains(package.Id))
            {
                errors.Add($"Package {package.Id} is missing from both placed and unpacked collections.");
            }
        }
    }

    private static void ValidateLifoDepthBoundaries(SolutionState state, ICollection<string> errors)
    {
        if (state.Scenario.Van is null)
        {
            errors.Add("Scenario van is missing.");
            return;
        }

        var currentBoundary = state.Scenario.Van.Length;
        var stops = state.Scenario.Packages
            .Select(x => x.DeliveryStopIndex)
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

        foreach (var stop in stops)
        {
            var placements = state.Placements
                .Where(x => x.PackageItem?.DeliveryStopIndex == stop)
                .ToList();

            foreach (var placement in placements)
            {
                var rightFace = placement.X + placement.OrientedLength;
                if (rightFace > currentBoundary + Epsilon)
                {
                    errors.Add(
                        $"LIFO depth violation for stop {stop}: package {placement.PackageItemId} exceeds boundary {currentBoundary:F3}.");
                }
            }

            var minX = placements.Select(x => x.X).DefaultIfEmpty(currentBoundary).Min();
            currentBoundary = Math.Min(currentBoundary, minX);
        }
    }

    private void ValidatePlacementFeasibility(
        SolutionState state,
        ICollection<string> errors,
        CancellationToken cancellationToken,
        SolveExecutionContext? context)
    {
        var replay = new SolutionState
        {
            Scenario = state.Scenario
        };

        var ordered = state.Placements
            .OrderBy(x => x.Z)
            .ThenBy(x => x.X)
            .ThenBy(x => x.Y)
            .ToList();

        foreach (var placement in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var package = placement.PackageItem
                          ?? state.Scenario.Packages.FirstOrDefault(x => x.Id == placement.PackageItemId);

            if (package is null)
            {
                errors.Add($"Placement references unknown package {placement.PackageItemId}.");
                continue;
            }

            var candidate = new CandidatePlacement
            {
                PackageItem = package,
                Position = new Position3D(placement.X, placement.Y, placement.Z),
                OrientedSize = new Dimensions3D(placement.OrientedLength, placement.OrientedWidth, placement.OrientedHeight)
            };

            if (!checker.IsPlacementFeasible(replay, candidate, context))
            {
                errors.Add($"Placement for package {placement.PackageItemId} failed feasibility replay.");
                continue;
            }

            replay.Placements.Add(new Placement
            {
                PackageItemId = placement.PackageItemId,
                PackageItem = package,
                X = placement.X,
                Y = placement.Y,
                Z = placement.Z,
                OrientedLength = placement.OrientedLength,
                OrientedWidth = placement.OrientedWidth,
                OrientedHeight = placement.OrientedHeight
            });
        }
    }
}