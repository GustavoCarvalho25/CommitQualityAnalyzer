using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RefactorScore.CrossCutting.IoC.DependenceInjection;

public static class CrossCuttingExtensions
{
    public static IServiceCollection AddRefactorScoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);
        services.AddHealthChecks(configuration);
        
        return services;
    }
}