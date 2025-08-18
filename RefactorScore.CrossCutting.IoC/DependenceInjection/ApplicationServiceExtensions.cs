using Microsoft.Extensions.DependencyInjection;
using RefactorScore.Application.Services;
using RefactorScore.Domain.Services;

namespace RefactorScore.CrossCutting.IoC.DependenceInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICommitAnalysisService, CommitAnalysisService>();
        
        return services;
    }
}