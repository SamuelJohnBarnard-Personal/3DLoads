using Microsoft.Extensions.DependencyInjection;
using ThreeDLoads.Application.Abstractions;
using ThreeDLoads.Application.Services;
using ThreeDLoads.Application.Services.Phases;
using ThreeDLoads.Application.Services.Phases.Brkga;

namespace ThreeDLoads.Application;

public static class ServiceCollectionExtensions
{
    // Registers all application-layer services. Phases are registered in pipeline execution order.
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IOrientationEnumerator, OrientationEnumerator>();
        services.AddScoped<IGeometryFeasibilityChecker, GeometryFeasibilityChecker>();
        services.AddScoped<IExtremePointAnchorEngine, ExtremePointAnchorEngine>();
        services.AddScoped<ISolutionValidator, SolutionValidator>();

        services.AddScoped<IPhase, InitialGreedyPhase>();
        services.AddScoped<IPhase, BrkgaPhase>();
        services.AddScoped<IPhase, CleanupPhase>();
        services.AddScoped<IPhase, FinalValidationPhase>();
        services.AddScoped<ISolverService, PhasePipelineSolver>();

        return services;
    }
}