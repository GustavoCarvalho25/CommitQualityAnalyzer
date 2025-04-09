using CommitQualityAnalyzer.Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Extensões para registrar os serviços de análise de commits
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adiciona os serviços de análise de commits ao contêiner de injeção de dependência
        /// </summary>
        public static IServiceCollection AddCommitAnalysisServices(this IServiceCollection services, string repoPath)
        {
            // Registrar serviços auxiliares
            services.AddHttpClient();
            services.AddSingleton<GitRepositoryWrapper>(sp => new GitRepositoryWrapper(
                sp.GetRequiredService<ILogger<GitRepositoryWrapper>>(),
                repoPath
            ));
            services.AddSingleton<GitDiffService>(sp => new GitDiffService(
                sp.GetRequiredService<ILogger<GitDiffService>>(),
                sp.GetRequiredService<GitRepositoryWrapper>(),
                repoPath
            ));
            services.AddSingleton<OllamaService>();
            services.AddSingleton<ResponseAnalysisService>();
            services.AddSingleton<PromptBuilderService>();
            services.AddSingleton<AnalysisMapperService>();
            services.AddSingleton(sp => new CommitSchedulerService(
                repoPath,
                sp.GetRequiredService<ILogger<CommitSchedulerService>>(),
                sp.GetRequiredService<GitRepositoryWrapper>()
            ));
            services.AddSingleton(sp => new CommitAnalyzerService(
                repoPath,
                sp.GetRequiredService<ILogger<CommitAnalyzerService>>(),
                sp.GetRequiredService<ICodeAnalysisRepository>(),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<GitDiffService>>(),
                sp.GetRequiredService<ILogger<OllamaService>>(),
                sp.GetRequiredService<ILogger<ResponseAnalysisService>>(),
                sp.GetRequiredService<ILogger<PromptBuilderService>>(),
                sp.GetRequiredService<ILogger<AnalysisMapperService>>(),
                sp.GetRequiredService<ILogger<CommitSchedulerService>>(),
                sp.GetRequiredService<ILogger<GitRepositoryWrapper>>()
            ));
            
            return services;
        }
    }
}
