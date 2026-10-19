using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ThreeDLoads.Api.Contracts;

namespace ThreeDLoads.Tests.Integration;

public sealed class SolveEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Solve_ReturnsValidatedPackingFromFreshInput()
    {
        using var client = factory.CreateClient();
        var scenarioId = Guid.NewGuid();
        var vanId = Guid.NewGuid();
        var firstPackageId = Guid.NewGuid();
        var secondPackageId = Guid.NewGuid();
        var request = new RunSolveRequest
        {
            Scenario = new ScenarioResponse
            {
                Id = scenarioId,
                Name = "demo",
                VanId = vanId,
                Van = new VanResponse
                {
                    Id = vanId,
                    Name = "test-van",
                    Length = 10,
                    Width = 4,
                    Height = 4,
                    MaxPayloadWeight = 100
                },
                Packages =
                [
                    new PackageItemResponse
                    {
                        Id = firstPackageId,
                        ExternalReference = "box-1",
                        Length = 2,
                        Width = 2,
                        Height = 2,
                        Weight = 10,
                        DeliveryStopIndex = 1,
                        OrientationPolicy = 1,
                        MaxLoadBearingWeight = 100
                    },
                    new PackageItemResponse
                    {
                        Id = secondPackageId,
                        ExternalReference = "box-2",
                        Length = 2,
                        Width = 2,
                        Height = 2,
                        Weight = 10,
                        DeliveryStopIndex = 1,
                        OrientationPolicy = 1,
                        MaxLoadBearingWeight = 100
                    }
                ]
            }
        };

        var response = await client.PostAsJsonAsync("/api/v1/solve", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SolutionResponse>();
        Assert.NotNull(result);
        Assert.Equal(scenarioId, result.ScenarioId);
        Assert.True(result.IsValid);
        Assert.Empty(result.ValidationErrors);
        Assert.Equal(2, result.Placements.Count);
        var stackedPackage = Assert.Single(result.Placements, placement => placement.PackageItemId == secondPackageId);
        Assert.Equal(2, stackedPackage.Z);
    }
}