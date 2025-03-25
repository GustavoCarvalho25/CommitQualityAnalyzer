using LibGit2Sharp;
using System.Text;
using CommitQualityAnalyzer.Core.Models;
using CommitQualityAnalyzer.Core.Repositories;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CommitQualityAnalyzer.Worker.Services
{
    public class CommitAnalyzerService
    {
        private readonly string _repoPath;
        private readonly ILogger<CommitAnalyzerService> _logger;
        private readonly ICodeAnalysisRepository _repository;
        private readonly IConfiguration _configuration;

        public CommitAnalyzerService(
            string repoPath, 
            ILogger<CommitAnalyzerService> logger,
            ICodeAnalysisRepository repository,
            IConfiguration configuration)
        {
            _repoPath = repoPath;
            _logger = logger;
            _repository = repository;
            _configuration = configuration;
        }

        public async Task AnalyzeLastDayCommits()
        {
            _logger.LogInformation($"Iniciando análise do repositório: {_repoPath}");
            
            using var repo = new Repository(_repoPath);
            var yesterday = DateTime.Now.AddDays(-1);

            var commits = repo.Commits
                .Where(c => c.Author.When >= yesterday)
                .ToList();

            _logger.LogInformation($"Encontrados {commits.Count} commits nas últimas 24 horas");

            foreach (var commit in commits)
            {
                _logger.LogInformation($"Analisando commit: {commit.Id} de {commit.Author.Name}");
                
                var changes = GetCommitChanges(repo, commit);
                
                foreach (var change in changes)
                {
                    if (Path.GetExtension(change.Path).ToLower() == ".cs")
                    {
                        var analysis = await AnalyzeCode(change.Content, change.Path, commit);
                        
                        if (analysis != null)
                        {
                            await _repository.SaveAnalysisAsync(analysis);
                            _logger.LogInformation($"Análise salva para o arquivo {change.Path}");
                        }
                    }
                }
            }
        }

        private async Task<CodeAnalysis?> AnalyzeCode(string content, string filePath, Commit commit)
        {
            _logger.LogInformation($"Analisando arquivo: {filePath}");

            // Truncar o conteúdo se for muito longo (limite de 4000 caracteres para o CodeLlama)
            var truncatedContent = content.Length > 4000
                ? content.Substring(0, 4000) + "\n... (truncated)"
                : content;

            _logger.LogInformation("Iniciando análise com CodeLlama");
            var result = await RunCodeLlama(BuildAnalysisPrompt(truncatedContent));

            if (string.IsNullOrEmpty(result))
            {
                _logger.LogWarning("CodeLlama não retornou nenhuma análise");
                return null;
            }

            try
            {
                var analysisResult = JsonSerializer.Deserialize<AnalysisResult>(result);
                if (analysisResult == null)
                {
                    _logger.LogError("Erro ao deserializar resultado da análise");
                    return null;
                }

                _logger.LogInformation($"Análise concluída com nota final: {analysisResult.NotaFinal}");

                return new CodeAnalysis
                {
                    CommitId = commit.Id.Sha,
                    FilePath = filePath,
                    AuthorName = commit.Author.Name,
                    CommitDate = commit.Author.When.DateTime,
                    AnalysisDate = DateTime.UtcNow,
                    Analysis = analysisResult
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parsear resultado da análise");
                return null;
            }
        }

        private string BuildAnalysisPrompt(string code)
        {
            return @$"Você é um especialista em análise de código C#. Analise o seguinte código e forneça uma avaliação detalhada considerando:

1. Clean Code (0-10):
   - Nomes significativos de variáveis e métodos
   - Funções pequenas e focadas
   - Comentários apropriados
   - DRY (Don't Repeat Yourself)

2. Princípios SOLID (0-10):
   - Single Responsibility
   - Open/Closed
   - Liskov Substitution
   - Interface Segregation
   - Dependency Inversion

3. Design Patterns (0-10):
   - Uso apropriado de padrões
   - Estrutura do código
   - Organização das classes

4. Testabilidade (0-10):
   - Facilidade de teste
   - Injeção de dependências
   - Desacoplamento

5. Segurança (0-10):
   - Validação de entrada
   - Tratamento de exceções
   - Práticas seguras de código

Forneça a análise em formato JSON seguindo exatamente esta estrutura:

{{
    ""analiseGeral"": {{
        ""cleanCode"": {{ ""nota"": 0-10, ""comentario"": ""explicação detalhada"" }},
        ""solidPrinciples"": {{ ""nota"": 0-10, ""comentario"": ""explicação detalhada"" }},
        ""designPatterns"": {{ ""nota"": 0-10, ""comentario"": ""explicação detalhada"" }},
        ""testability"": {{ ""nota"": 0-10, ""comentario"": ""explicação detalhada"" }},
        ""security"": {{ ""nota"": 0-10, ""comentario"": ""explicação detalhada"" }}
    }},
    ""notaFinal"": 0-10,
    ""comentarioGeral"": ""resumo geral da análise""
}}

Código para análise:

{code}

Responda APENAS com o JSON, sem texto adicional antes ou depois. Seja bem criterioso nas notas, pois um gestor avaliará as notas e tomará as providência com base nelas, evitando notas muito baixas sem justificativa adequada, mas podendo sim adicionar notas baixas, mas com justificativa adequada.";
        }

        private async Task<string> RunCodeLlama(string input)
        {
            try
            {
                _logger.LogInformation("Iniciando análise com CodeLlama");
                
                var containerName = _configuration.GetValue<string>("Ollama:DockerContainerName") ?? "ollama";
                var modelName = _configuration.GetValue<string>("Ollama:ModelName") ?? "codellama";
                var timeoutMinutes = _configuration.GetValue<int>("Ollama:TimeoutMinutes", 2);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec {containerName} ollama run {modelName} \"{input.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    _logger.LogError("Falha ao iniciar o processo do Docker");
                    return string.Empty;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
                
                try
                {
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    
                    var processExitTask = Task.Run(() => process.WaitForExit());
                    if (await Task.WhenAny(processExitTask, Task.Delay(TimeSpan.FromMinutes(timeoutMinutes))) != processExitTask)
                    {
                        _logger.LogError($"Timeout ao executar CodeLlama ({timeoutMinutes} minutos)");
                        try { process.Kill(); } catch { }
                        return string.Empty;
                    }

                    var output = await outputTask;
                    var error = await errorTask;

                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogError($"Erro ao executar CodeLlama: {error}");
                    }

                    if (string.IsNullOrEmpty(output))
                    {
                        _logger.LogWarning("CodeLlama não retornou nenhuma análise");
                    }
                    else
                    {
                        _logger.LogInformation($"CodeLlama retornou análise: {output}");
                    }

                    var jsonStart = output.IndexOf('{');
                    var jsonEnd = output.LastIndexOf('}');
                    
                    if (jsonStart >= 0 && jsonEnd >= 0)
                    {
                        return output.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    }

                    return string.Empty;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError("Operação cancelada por timeout");
                    try { process.Kill(); } catch { }
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar análise com CodeLlama");
                return string.Empty;
            }
        }

        private IEnumerable<(string Path, string Content)> GetCommitChanges(Repository repo, Commit commit)
        {
            if (commit.Parents.Any())
            {
                var parent = commit.Parents.First();
                var comparison = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);

                foreach (var change in comparison)
                {
                    if (change.Status == ChangeKind.Modified || change.Status == ChangeKind.Added)
                    {
                        var blob = commit[change.Path].Target as Blob;
                        if (blob != null)
                        {
                            using var content = new StreamReader(blob.GetContentStream(), Encoding.UTF8);
                            yield return (change.Path, content.ReadToEnd());
                        }
                    }
                }
            }
        }
    }
}
