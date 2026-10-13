using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreeDLoads.Api.Contracts;
using ThreeDLoads.Infrastructure.Persistence;

namespace ThreeDLoads.Tests.Integration;

public class AsyncSolveFlowTests : IClassFixture<AsyncSolveFlowTests.TestApiFactory>
{
    private readonly TestApiFactory factory;

    public AsyncSolveFlowTests(TestApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AuthCreateSolvePollAndFetchResult_WorksEndToEnd()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var tokenResponse = await client.PostAsJsonAsync("/api/v1/auth/token", new
        {
            username = "admin",
            password = "admin"
        });

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var token = tokenDoc.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createScenarioResponse = await client.PostAsJsonAsync("/api/v1/scenarios", new
        {
            name = "integration-scenario",
            van = new
            {
                name = "test-van",
                length = 10.0,
                width = 4.0,
                height = 4.0,
                maxPayloadWeight = 1000.0
            },
            packages = new object[]
            {
                new
                {
                    externalReference = "p1",
                    length = 2.0,
                    width = 2.0,
                    height = 2.0,
                    weight = 10.0,
                    deliveryStopIndex = 2,
                    orientationPolicy = 1,
                    maxLoadBearingWeight = 100.0
                },
                new
                {
                    externalReference = "p2",
                    length = 2.0,
                    width = 2.0,
                    height = 2.0,
                    weight = 10.0,
                    deliveryStopIndex = 1,
                    orientationPolicy = 1,
                    maxLoadBearingWeight = 100.0
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, createScenarioResponse.StatusCode);

        using var scenarioDoc = JsonDocument.Parse(await createScenarioResponse.Content.ReadAsStringAsync());
        var scenarioId = scenarioDoc.RootElement.GetProperty("id").GetGuid();

        var scenarioResponse = await client.GetAsync($"/api/v1/scenarios/{scenarioId}");
        Assert.Equal(HttpStatusCode.OK, scenarioResponse.StatusCode);

        var scenario = await scenarioResponse.Content.ReadFromJsonAsync<ScenarioResponse>();
        Assert.NotNull(scenario);
        Assert.Equal(scenarioId, scenario.Id);
        Assert.Equal(2, scenario.Packages.Count);
        Assert.Equal("test-van", scenario.Van.Name);

        var solveResponse = await client.PostAsJsonAsync($"/api/v1/scenarios/{scenarioId}/solve", new
        {
            phases = new[] { "InitialGreedy", "Brkga", "Cleanup" },
            randomSeed = 42,
            timeBudgetSeconds = 2
        });

        Assert.Equal(HttpStatusCode.Accepted, solveResponse.StatusCode);

        using var solveDoc = JsonDocument.Parse(await solveResponse.Content.ReadAsStringAsync());
        var jobId = solveDoc.RootElement.GetProperty("id").GetGuid();

        Guid? solutionId = null;
        for (var i = 0; i < 40; i++)
        {
            var jobResponse = await client.GetAsync($"/api/v1/solve-jobs/{jobId}");
            Assert.Equal(HttpStatusCode.OK, jobResponse.StatusCode);

            using var jobDoc = JsonDocument.Parse(await jobResponse.Content.ReadAsStringAsync());
            var status = jobDoc.RootElement.GetProperty("status").GetInt32();

            if (status == 4)
            {
                var message = jobDoc.RootElement.TryGetProperty("errorMessage", out var errorElement)
                    ? errorElement.GetString()
                    : "unknown";
                Assert.Fail($"Solve job failed: {message}");
            }

            if (status == 3)
            {
                if (jobDoc.RootElement.TryGetProperty("solutionId", out var solutionElement) &&
                    solutionElement.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(solutionElement.GetString(), out var parsed))
                {
                    solutionId = parsed;
                }
                break;
            }

            await Task.Delay(100);
        }

        Assert.True(solutionId.HasValue);

        var resultResponse = await client.GetAsync($"/api/v1/solve-jobs/{jobId}/result");
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);

        var solution = await resultResponse.Content.ReadFromJsonAsync<SolutionResponse>();
        Assert.NotNull(solution);
        Assert.Equal(solutionId, solution.Id);
        Assert.Equal(scenarioId, solution.ScenarioId);
        Assert.True(solution.IsValid);
        Assert.NotNull(solution.ValidationErrors);
        Assert.True(solution.UnpackedItemCount >= 0);
        Assert.Equal(2,
            solution.Placements.Count +
            solution.UnpackedItemCount);

        var revalidateResponse = await client.PostAsync($"/api/v1/solutions/{solutionId}/revalidate", content: null);
        Assert.Equal(HttpStatusCode.OK, revalidateResponse.StatusCode);

        using var revalidateDoc = JsonDocument.Parse(await revalidateResponse.Content.ReadAsStringAsync());
        Assert.Equal(solutionId.Value, revalidateDoc.RootElement.GetProperty("solutionId").GetGuid());
        Assert.Equal(JsonValueKind.Array, revalidateDoc.RootElement.GetProperty("validationErrors").ValueKind);
        Assert.True(revalidateDoc.RootElement.TryGetProperty("isValid", out _));

        var updateScenarioResponse = await client.PutAsJsonAsync($"/api/v1/scenarios/{scenarioId}", new
        {
            name = "integration-scenario-updated",
            van = new
            {
                name = "test-van",
                length = 10.0,
                width = 4.0,
                height = 4.0,
                maxPayloadWeight = 1000.0
            },
            packages = new object[]
            {
                new
                {
                    externalReference = "p1-updated",
                    length = 2.0,
                    width = 2.0,
                    height = 2.0,
                    weight = 10.0,
                    deliveryStopIndex = 2,
                    orientationPolicy = 1,
                    maxLoadBearingWeight = 100.0
                },
                new
                {
                    externalReference = "p2-updated",
                    length = 2.0,
                    width = 2.0,
                    height = 2.0,
                    weight = 10.0,
                    deliveryStopIndex = 1,
                    orientationPolicy = 1,
                    maxLoadBearingWeight = 100.0
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, updateScenarioResponse.StatusCode);

        var updatedSummary = await updateScenarioResponse.Content.ReadFromJsonAsync<ScenarioSummaryResponse>();
        Assert.NotNull(updatedSummary);

        var updatedScenarioResponse = await client.GetAsync($"/api/v1/scenarios/{updatedSummary.Id}");
        Assert.Equal(HttpStatusCode.OK, updatedScenarioResponse.StatusCode);

        var updatedScenario = await updatedScenarioResponse.Content.ReadFromJsonAsync<ScenarioResponse>();
        Assert.NotNull(updatedScenario);
        Assert.Equal("integration-scenario-updated", updatedScenario.Name);
        Assert.Contains(updatedScenario.Packages, package => package.ExternalReference == "p1-updated");

        var shrinkScenarioResponse = await client.PostAsJsonAsync("/api/v1/scenarios", new
        {
            name = "integration-scenario-shrink",
            van = new
            {
                name = "test-van",
                length = 10.0,
                width = 4.0,
                height = 4.0,
                maxPayloadWeight = 1000.0
            },
            packages = new object[]
            {
                new
                {
                    externalReference = "keep-me",
                    length = 2.0,
                    width = 2.0,
                    height = 2.0,
                    weight = 10.0,
                    deliveryStopIndex = 1,
                    orientationPolicy = 1,
                    maxLoadBearingWeight = 100.0
                },
                new
                {
                    externalReference = "remove-me",
                    length = 1.0,
                    width = 1.0,
                    height = 1.0,
                    weight = 5.0,
                    deliveryStopIndex = 2,
                    orientationPolicy = 1,
                    maxLoadBearingWeight = 50.0
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, shrinkScenarioResponse.StatusCode);

        var shrinkSummary = await shrinkScenarioResponse.Content.ReadFromJsonAsync<ScenarioSummaryResponse>();
        Assert.NotNull(shrinkSummary);

        var shrinkUpdateResponse = await client.PutAsJsonAsync($"/api/v1/scenarios/{shrinkSummary.Id}", new
        {
            name = "integration-scenario-shrink-updated",
            van = new
            {
                name = "test-van",
                length = 10.0,
                width = 4.0,
                height = 4.0,
                maxPayloadWeight = 1000.0
            },
            packages = new object[]
            {
                new
                {
                    externalReference = "keep-me",
                    length = 2.0,
                    width = 2.0,
                    height = 2.0,
                    weight = 10.0,
                    deliveryStopIndex = 1,
                    orientationPolicy = 1,
                    maxLoadBearingWeight = 100.0
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, shrinkUpdateResponse.StatusCode);

        var shrinkGetResponse = await client.GetAsync($"/api/v1/scenarios/{shrinkSummary.Id}");
        Assert.Equal(HttpStatusCode.OK, shrinkGetResponse.StatusCode);

        var shrinkScenario = await shrinkGetResponse.Content.ReadFromJsonAsync<ScenarioResponse>();
        Assert.NotNull(shrinkScenario);
        Assert.Single(shrinkScenario.Packages);
    }

    public sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"3dloads-integration-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = "unused",
                    ["Jwt:Issuer"] = "3DLoads.Api",
                    ["Jwt:Audience"] = "3DLoads.Client",
                    ["Jwt:Key"] = "integration-test-key-integration-test-key-123",
                    ["Jwt:DevUsername"] = "admin",
                    ["Jwt:DevPassword"] = "admin"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<LoadsDbContext>));

                services.AddDbContext<LoadsDbContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName);
                });

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
            });
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestAuth";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "integration-user"),
                new Claim(ClaimTypes.Role, "SolverUser")
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}