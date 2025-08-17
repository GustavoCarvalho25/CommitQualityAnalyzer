using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrossCutting.IoC.DependenceInjection;

public static class CrossCuttingExtensions
{
    public static IServiceCollection AddRefactorScoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplicationServices();
        services.AddInfraestructureServices(configuration);
        
        return services;
    }
}