using LibGit2Sharp;
using System.Text;
using CommitQualityAnalyzer.Core.Models;
using CommitQualityAnalyzer.Core.Repositories;
using System.Diagnostics;
using Serilog;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace CommitQualityAnalyzer.Worker.Services
{
    public class CommitAnalyzerService
    {
        private readonly string _repoPath;
        private readonly ICodeAnalysisRepository _repository;
        private readonly IConfiguration _configuration;

        public CommitAnalyzerService(string repoPath, ICodeAnalysisRepository repository, IConfiguration configuration)
        {
            _repoPath = repoPath;
            _repository = repository;
            _configuration = configuration;
        }

        public async Task AnalyzeLastDayCommits()
        {
            Log.Information("Iniciando análise de commits do último dia");

            using var repo = new Repository(_repoPath);
            var yesterday = DateTimeOffset.Now.AddDays(-1);
            
            // Obter todos os commits desde ontem
            var commits = repo.Commits.Where(c => c.Author.When >= yesterday).ToList();
            
            Log.Information("Encontrados {CommitCount} commits desde {Date}", commits.Count, yesterday);

            foreach (var commit in commits)
            {
                Log.Information("Analisando commit {CommitSha} - {CommitMessage}", commit.Sha, commit.MessageShort);
                
                var changes = GetCommitChanges(repo, commit).ToList();
                
                Log.Information("Commit {CommitSha} contém {ChangeCount} alterações", commit.Sha, changes.Count);

                foreach (var change in changes)
                {
                    if (Path.GetExtension(change.Path).ToLower() == ".cs")
                    {
                        Log.Information("Analisando arquivo {FilePath}", change.Path);
                        
                        var analysis = await AnalyzeCode(change.Content, change.Path, commit);
                        
                        await _repository.SaveAnalysisAsync(analysis);
                        Log.Information("Análise salva para {FilePath}", change.Path);
                    }
                }
            }
            
            Log.Information("Análise de commits concluída");
        }

        private string GetOriginalCode(Repository repo, Commit commit, string path)
        {
            try
            {
                if (commit.Parents.Any())
                {
                    var parent = commit.Parents.First();
                    var parentBlob = parent[path]?.Target as Blob;
                    
                    if (parentBlob != null)
                    {
                        using var contentStream = parentBlob.GetContentStream();
                        using var reader = new StreamReader(contentStream);
                        return reader.ReadToEnd();
                    }
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao obter código original para {FilePath}", path);
                return string.Empty;
            }
        }

        private async Task<CodeAnalysis> AnalyzeCode(string code, string filePath, Commit commit)
        {
            try
            {
                Log.Information("Iniciando análise de código para {FilePath}", filePath);
                
                var result = await RunCodeLlama(BuildAnalysisPrompt(code));
                
                if (string.IsNullOrEmpty(result))
                {
                    Log.Warning("Não foi possível analisar o código para {FilePath}", filePath);
                    
                    return new CodeAnalysis
                    {
                        CommitId = commit.Sha,
                        FilePath = filePath,
                        AuthorName = commit.Author.Name,
                        CommitDate = commit.Author.When.DateTime,
                        AnalysisDate = DateTime.Now,
                        Analysis = new AnalysisResult
                        {
                            AnaliseGeral = new Dictionary<string, CriteriaAnalysis>
                            {
                                { "cleanCode", new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" } },
                                { "solidPrinciples", new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" } },
                                { "designPatterns", new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" } },
                                { "testability", new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" } },
                                { "security", new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" } }
                            },
                            NotaFinal = 0,
                            ComentarioGeral = "Não foi possível analisar o código"
                        }
                    };
                }
                
                try
                {
                    // Tentar deserializar o JSON retornado
                    var analysisResult = JsonSerializer.Deserialize<AnalysisResult>(result);
                    if (analysisResult != null)
                    {
                        Log.Information("Análise gerada com sucesso para {FilePath}", filePath);
                        
                        return new CodeAnalysis
                        {
                            CommitId = commit.Sha,
                            FilePath = filePath,
                            AuthorName = commit.Author.Name,
                            CommitDate = commit.Author.When.DateTime,
                            AnalysisDate = DateTime.Now,
                            Analysis = analysisResult
                        };
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Erro ao deserializar análise para {FilePath}: {Result}", filePath, result);
                }
                
                // Fallback para análise padrão em caso de erro
                return new CodeAnalysis
                {
                    CommitId = commit.Sha,
                    FilePath = filePath,
                    AuthorName = commit.Author.Name,
                    CommitDate = commit.Author.When.DateTime,
                    AnalysisDate = DateTime.Now,
                    Analysis = new AnalysisResult
                    {
                        AnaliseGeral = new Dictionary<string, CriteriaAnalysis>
                        {
                            { "cleanCode", new CriteriaAnalysis { Nota = 0, Comentario = "Erro ao analisar" } },
                            { "solidPrinciples", new CriteriaAnalysis { Nota = 0, Comentario = "Erro ao analisar" } },
                            { "designPatterns", new CriteriaAnalysis { Nota = 0, Comentario = "Erro ao analisar" } },
                            { "testability", new CriteriaAnalysis { Nota = 0, Comentario = "Erro ao analisar" } },
                            { "security", new CriteriaAnalysis { Nota = 0, Comentario = "Erro ao analisar" } }
                        },
                        NotaFinal = 0,
                        ComentarioGeral = "Erro ao analisar o código"
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao analisar código para {FilePath}", filePath);
                
                return new CodeAnalysis
                {
                    CommitId = commit.Sha,
                    FilePath = filePath,
                    AuthorName = commit.Author.Name,
                    CommitDate = commit.Author.When.DateTime,
                    AnalysisDate = DateTime.Now,
                    Analysis = new AnalysisResult
                    {
                        AnaliseGeral = new Dictionary<string, CriteriaAnalysis>
                        {
                            { "cleanCode", new CriteriaAnalysis { Nota = 0, Comentario = "Exceção: " + ex.Message } },
                            { "solidPrinciples", new CriteriaAnalysis { Nota = 0, Comentario = "Exceção: " + ex.Message } },
                            { "designPatterns", new CriteriaAnalysis { Nota = 0, Comentario = "Exceção: " + ex.Message } },
                            { "testability", new CriteriaAnalysis { Nota = 0, Comentario = "Exceção: " + ex.Message } },
                            { "security", new CriteriaAnalysis { Nota = 0, Comentario = "Exceção: " + ex.Message } }
                        },
                        NotaFinal = 0,
                        ComentarioGeral = "Exceção ao analisar o código: " + ex.Message
                    }
                };
            }
        }

        private string BuildAnalysisPrompt(string code)
        {
            return @$"Você é um especialista Sênior em análise de código DOTNET. Analise o seguinte código e forneça uma avaliação detalhada considerando:

1. Clean Code (0-10):
   - Nomes significativos de variáveis e métodos
   - Funções pequenas e com propósito único
   - Comentários apropriados
   - Formatação consistente
   - Ausência de código duplicado

2. SOLID Principles (0-10):
   - Single Responsibility Principle
   - Open/Closed Principle
   - Liskov Substitution Principle
   - Interface Segregation Principle
   - Dependency Inversion Principle

3. Design Patterns (0-10):
   - Uso apropriado de padrões de design
   - Estrutura arquitetural
   - Organização de código

4. Testability (0-10):
   - Facilidade de escrever testes unitários
   - Injeção de dependências
   - Separação de preocupações

5. Security (0-10):
   - Validação de entrada
   - Proteção contra vulnerabilidades comuns
   - Gerenciamento seguro de dados sensíveis

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

Seja criterioso na sua análise, seja coerente com a mensagem de justificativa e a nota numérica, além de dizer o que faltou para uma nota melhor.

Código para análise:

{code}

Responda APENAS com o JSON, sem texto adicional antes ou depois. Responda com o JSON exatamente como solicitado, garantindo que seja um JSON válido.";
        }

        private async Task<string> RunCodeLlama(string input)
        {
            try
            {
                Log.Information("Iniciando análise com CodeLlama");
                
                var containerName = _configuration.GetValue<string>("Ollama:DockerContainerName") ?? "ollama";
                var modelName = _configuration.GetValue<string>("Ollama:ModelName") ?? "codellama";
                var timeoutMinutes = _configuration.GetValue<int>("Ollama:TimeoutMinutes", 2);
                var maxPromptLength = _configuration.GetValue<int>("Ollama:MaxPromptLength", 1500);
                
                // Verificar se o prompt é muito longo e precisa ser dividido
                if (input.Length > maxPromptLength)
                {
                    Log.Warning("Prompt excede o tamanho máximo de {MaxLength} caracteres. Tamanho atual: {CurrentLength}. Dividindo o prompt.", 
                        maxPromptLength, input.Length);
                    return await RunCodeLlamaWithSplitPrompt(input, maxPromptLength, containerName, modelName, timeoutMinutes);
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec -i {containerName} ollama run {modelName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Log.Error("Falha ao iniciar o processo do Docker");
                    return string.Empty;
                }
                
                // Usar CancellationToken para timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
                
                // Escrever o prompt na entrada padrão e fechar para evitar que o processo aguarde mais entrada
                await process.StandardInput.WriteLineAsync(input);
                process.StandardInput.Close();
                
                // Ler a saída de forma assíncrona
                var outputBuilder = new StringBuilder();
                
                try
                {
                    // Ler a saída até o fim ou até o timeout
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    if (await Task.WhenAny(outputTask, Task.Delay(TimeSpan.FromMinutes(timeoutMinutes), cts.Token)) == outputTask)
                    {
                        var output = await outputTask;
                        outputBuilder.Append(output);
                    }
                    else
                    {
                        // Timeout ocorreu
                        Log.Warning("Timeout ao aguardar resposta do CodeLlama após {Timeout} minutos", timeoutMinutes);
                        try { process.Kill(); } catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("Operação cancelada por timeout");
                    try { process.Kill(); } catch { }
                }
                
                if (!process.WaitForExit(5000))
                {
                    Log.Warning("Processo não terminou após 5 segundos, forçando encerramento");
                    try { process.Kill(); } catch { }
                }
                
                var result = outputBuilder.ToString();
                Log.Information("Análise com CodeLlama concluída. Tamanho da resposta: {Length} caracteres", result.Length);
                
                // Limpar códigos ANSI e extrair JSON
                result = CleanAnsiEscapeCodes(result);
                result = ExtractJsonFromOutput(result);
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao executar análise com CodeLlama");
                return string.Empty;
            }
        }
        
        private async Task<string> RunCodeLlamaWithSplitPrompt(string input, int maxLength, string containerName, string modelName, int timeoutMinutes)
        {
            try
            {
                // Dividir o prompt em duas partes: instruções e código
                var promptParts = SplitPromptIntoParts(input, maxLength);
                
                Log.Information("Prompt dividido em {Count} partes", promptParts.Count);
                
                // Configurar o processo para a primeira parte (instruções)
                var startInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec -i {containerName} ollama run {modelName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Log.Error("Falha ao iniciar o processo do Docker para prompt dividido");
                    return string.Empty;
                }
                
                // Usar CancellationToken para timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
                
                // Enviar as partes do prompt sequencialmente
                foreach (var part in promptParts)
                {
                    Log.Information("Enviando parte do prompt com {Length} caracteres", part.Length);
                    await process.StandardInput.WriteLineAsync(part);
                    // Pequena pausa para o modelo processar
                    await Task.Delay(500);
                }
                
                // Fechar a entrada para sinalizar que não há mais dados
                process.StandardInput.Close();
                
                // Ler a saída de forma assíncrona
                var outputBuilder = new StringBuilder();
                
                try
                {
                    // Ler a saída até o fim ou até o timeout
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    if (await Task.WhenAny(outputTask, Task.Delay(TimeSpan.FromMinutes(timeoutMinutes), cts.Token)) == outputTask)
                    {
                        var output = await outputTask;
                        outputBuilder.Append(output);
                    }
                    else
                    {
                        // Timeout ocorreu
                        Log.Warning("Timeout ao aguardar resposta do CodeLlama após {Timeout} minutos", timeoutMinutes);
                        try { process.Kill(); } catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("Operação cancelada por timeout");
                    try { process.Kill(); } catch { }
                }
                
                if (!process.WaitForExit(5000))
                {
                    Log.Warning("Processo não terminou após 5 segundos, forçando encerramento");
                    try { process.Kill(); } catch { }
                }
                
                var result = outputBuilder.ToString();
                Log.Information("Análise com prompt dividido concluída. Tamanho da resposta: {Length} caracteres", result.Length);
                
                // Limpar códigos ANSI e extrair JSON
                result = CleanAnsiEscapeCodes(result);
                result = ExtractJsonFromOutput(result);
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao executar análise com prompt dividido");
                return string.Empty;
            }
        }
        
        private List<string> SplitPromptIntoParts(string input, int maxLength)
        {
            var parts = new List<string>();
            
            // Encontrar o início do código no prompt
            var codeStartIndex = input.IndexOf("```csharp");
            if (codeStartIndex == -1)
            {
                // Se não encontrar o marcador de código, dividir pela metade
                codeStartIndex = input.Length / 2;
            }
            
            // Primeira parte: instruções e contexto
            var instructionPart = "Você é um especialista em análise de código C#. Vou te enviar um código em partes. " +
                                 "Após receber todas as partes, analise o código completo e forneça uma avaliação detalhada " +
                                 "no formato JSON conforme solicitado. Aqui está a primeira parte:\n\n" +
                                 input.Substring(0, codeStartIndex);
            
            // Segunda parte: código a ser analisado
            var codePart = "Aqui está a parte final do código para análise:\n\n" +
                          input.Substring(codeStartIndex) +
                          "\n\nAgora que você tem o código completo, forneça sua análise no formato JSON conforme solicitado anteriormente.";
            
            // Adicionar as partes à lista
            parts.Add(instructionPart);
            parts.Add(codePart);
            
            return parts;
        }
        
        private string CleanAnsiEscapeCodes(string input)
        {
            // Remover códigos de escape ANSI
            return Regex.Replace(input, @"\e\[[0-9;]*[a-zA-Z]", string.Empty);
        }
        
        private string ExtractJsonFromOutput(string output)
        {
            // Tentar extrair JSON da saída
            var jsonMatch = Regex.Match(output, @"\{.*\}", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                return jsonMatch.Value;
            }
            
            return output;
        }

        private IEnumerable<(string Path, string Content)> GetCommitChanges(Repository repo, Commit commit)
        {
            if (commit.Parents.Any())
            {
                var parent = commit.Parents.First();
                var comparison = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);

                foreach (var change in comparison.Modified)
                {
                    var blob = commit[change.Path].Target as Blob;
                    if (blob != null)
                    {
                        using var contentStream = blob.GetContentStream();
                        using var reader = new StreamReader(contentStream);
                        yield return (change.Path, reader.ReadToEnd());
                    }
                }

                foreach (var change in comparison.Added)
                {
                    var blob = commit[change.Path].Target as Blob;
                    if (blob != null)
                    {
                        using var contentStream = blob.GetContentStream();
                        using var reader = new StreamReader(contentStream);
                        yield return (change.Path, reader.ReadToEnd());
                    }
                }
            }
        }
    }
}
