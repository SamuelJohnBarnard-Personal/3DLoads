using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Infrastructure.Background;
using ThreeDLoads.Infrastructure.Persistence;
using ThreeDLoads.Infrastructure.Queue;

namespace ThreeDLoads.Infrastructure;

public static class ServiceCollectionExtensions
{
    // Registers EF Core (Postgres with snake_case naming), the in-memory job queue, and the background worker.
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=3dloads;Username=postgres;Password=postgres";

        services.AddDbContext<LoadsDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddSingleton<ISolveJobQueue, InMemorySolveJobQueue>();
        services.AddHostedService<SolveJobBackgroundService>();

        return services;
    }
}