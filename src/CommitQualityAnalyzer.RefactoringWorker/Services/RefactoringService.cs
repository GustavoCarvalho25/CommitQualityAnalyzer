using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommitQualityAnalyzer.Core.Models;
using CommitQualityAnalyzer.Core.Repositories;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace CommitQualityAnalyzer.RefactoringWorker.Services
{
    public class RefactoringService
    {
        private readonly string _repoPath;
        private readonly ICodeAnalysisRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly int _maxPromptLength;

        public RefactoringService(string repoPath, ICodeAnalysisRepository repository, IConfiguration configuration)
        {
            _repoPath = repoPath;
            _repository = repository;
            _configuration = configuration;
            _maxPromptLength = _configuration.GetValue<int>("Ollama:MaxPromptLength", 1500);
            
            Log.Information("RefactoringService inicializado com repositório em {RepoPath}", _repoPath);
        }

        public async Task GenerateRefactoringProposalsForLastDay()
        {
            try
            {
                Log.Information("Iniciando geração de propostas de refatoração para commits do último dia");
                
                if (string.IsNullOrEmpty(_repoPath) || !Directory.Exists(_repoPath))
                {
                    Log.Error("Caminho do repositório inválido: {RepoPath}", _repoPath);
                    return;
                }

                using var repo = new Repository(_repoPath);
                var lastDayCommits = GetLastDayCommits(repo);
                
                Log.Information("Encontrados {CommitCount} commits desde {Date}", lastDayCommits.Count(), DateTime.Now.AddDays(-1));
                
                foreach (var commit in lastDayCommits)
                {
                    await ProcessCommit(repo, commit);
                }
                
                Log.Information("Processamento de propostas de refatoração concluído");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao gerar propostas de refatoração");
            }
        }

        private async Task ProcessCommit(Repository repo, Commit commit)
        {
            try
            {
                Log.Information("Analisando commit {CommitSha} - {CommitMessage}", commit.Sha.Substring(0, 10), commit.MessageShort);
                
                var changes = GetChangesFromCommit(repo, commit);
                Log.Information("Commit {CommitSha} contém {ChangesCount} alterações", commit.Sha.Substring(0, 10), changes.Count);
                
                foreach (var change in changes)
                {
                    if (Path.GetExtension(change.Path).ToLower() == ".cs")
                    {
                        await ProcessCSharpFile(repo, commit, change);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao processar commit {CommitSha}", commit.Sha);
            }
        }

        private async Task ProcessCSharpFile(Repository repo, Commit commit, TreeEntryChanges change)
        {
            try
            {
                Log.Information("Analisando arquivo {FilePath}", change.Path);
                
                // Verificar se já existe uma análise para este arquivo neste commit
                var existingAnalyses = await _repository.GetAnalysesByCommitIdAsync(commit.Sha);
                var existingAnalysis = existingAnalyses?.FirstOrDefault(a => a.FilePath == change.Path);
                
                if (existingAnalysis == null)
                {
                    Log.Warning("Não foi encontrada análise para o arquivo {FilePath} no commit {CommitSha}", change.Path, commit.Sha);
                    return;
                }
                
                // Verificar se já existe uma proposta de refatoração
                if (existingAnalysis.RefactoringProposals?.Any() == true)
                {
                    Log.Information("Já existe uma proposta de refatoração para {FilePath}", change.Path);
                    return;
                }
                
                var originalCode = GetOriginalCode(repo, commit, change.Path);
                var modifiedCode = GetFileContent(change);
                
                if (string.IsNullOrEmpty(originalCode) || string.IsNullOrEmpty(modifiedCode))
                {
                    Log.Warning("Não foi possível obter o código original ou modificado para {FilePath}", change.Path);
                    return;
                }
                
                var proposal = await GenerateRefactoringProposal(originalCode, modifiedCode, change.Path);
                if (proposal != null)
                {
                    // Atualizar a análise existente com a proposta de refatoração
                    existingAnalysis.RefactoringProposals.Add(proposal);
                    
                    // Como não temos um método de atualização, vamos salvar novamente
                    await _repository.SaveAnalysisAsync(existingAnalysis);
                    
                    Log.Information("Proposta de refatoração gerada e salva para {FilePath}", change.Path);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao processar arquivo C# {FilePath}", change.Path);
            }
        }

        private async Task<RefactoringProposal> GenerateRefactoringProposal(string originalCode, string modifiedCode, string filePath)
        {
            try
            {
                Log.Information("Gerando proposta de refatoração para {FilePath}", filePath);
                
                // Truncar o código original e modificado para não exceder o limite do prompt
                var (truncatedOriginal, truncatedModified) = TruncateCodeForPrompt(originalCode, modifiedCode);
                
                var result = await RunCodeLlama(BuildRefactoringPrompt(truncatedOriginal, truncatedModified));
                if (string.IsNullOrEmpty(result))
                {
                    Log.Warning("CodeLlama não retornou uma proposta de refatoração válida para {FilePath}", filePath);
                    return null;
                }
                
                try
                {
                    // Tentar deserializar o JSON retornado
                    var refactoringResult = JsonSerializer.Deserialize<RefactoringResult>(result);
                    if (refactoringResult != null && refactoringResult.SugestoesRefatoracao.Any())
                    {
                        var suggestion = refactoringResult.SugestoesRefatoracao.First();
                        
                        return new RefactoringProposal
                        {
                            Description = suggestion.Descricao,
                            OriginalCodeSnippet = suggestion.CodigoOriginal,
                            RefactoredCodeSnippet = suggestion.CodigoRefatorado,
                            Category = suggestion.Categoria,
                            LineStart = suggestion.LinhaInicio,
                            LineEnd = suggestion.LinhaFim,
                            ImprovementScore = suggestion.PontuacaoMelhoria,
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Erro ao deserializar proposta de refatoração para {FilePath}: {Result}", filePath, result);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao gerar proposta de refatoração para {FilePath}", filePath);
                return null;
            }
        }

        private (string truncatedOriginal, string truncatedModified) TruncateCodeForPrompt(string originalCode, string modifiedCode)
        {
            // Calcular o tamanho total disponível para o código
            // Reservar ~500 caracteres para o texto do prompt
            int availableSpace = _maxPromptLength - 500;
            
            if (originalCode.Length + modifiedCode.Length <= availableSpace)
            {
                // Se o código completo cabe no prompt, não é necessário truncar
                return (originalCode, modifiedCode);
            }
            
            // Dividir o espaço disponível igualmente entre o código original e modificado
            int halfSpace = availableSpace / 2;
            
            string truncatedOriginal = originalCode;
            string truncatedModified = modifiedCode;
            
            if (originalCode.Length > halfSpace)
            {
                // Truncar o código original
                truncatedOriginal = originalCode.Substring(0, halfSpace) + "\n// ... código truncado ...\n";
            }
            
            if (modifiedCode.Length > halfSpace)
            {
                // Truncar o código modificado
                truncatedModified = modifiedCode.Substring(0, halfSpace) + "\n// ... código truncado ...\n";
            }
            
            Log.Information("Código truncado para caber no limite do prompt. Original: {OriginalLength} -> {TruncatedOriginalLength}, Modificado: {ModifiedLength} -> {TruncatedModifiedLength}",
                originalCode.Length, truncatedOriginal.Length, modifiedCode.Length, truncatedModified.Length);
            
            return (truncatedOriginal, truncatedModified);
        }

        private string BuildRefactoringPrompt(string originalCode, string modifiedCode)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Você é um especialista em refatoração de código C#. Analise o código original e o código modificado abaixo e forneça sugestões de refatoração adicionais para melhorar ainda mais o código.");
            sb.AppendLine("Foque em princípios SOLID, Clean Code, Design Patterns, e boas práticas de C#.");
            sb.AppendLine();
            sb.AppendLine("Código Original:");
            sb.AppendLine("```csharp");
            sb.AppendLine(originalCode);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Código Modificado:");
            sb.AppendLine("```csharp");
            sb.AppendLine(modifiedCode);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Forneça sugestões de refatoração adicionais que poderiam ser aplicadas ao código modificado para melhorá-lo ainda mais.");
            sb.AppendLine("Seja específico e forneça exemplos de código quando possível.");
            
            return sb.ToString();
        }

        private async Task<string> RunCodeLlama(string input)
        {
            try
            {
                Log.Information("Iniciando análise com CodeLlama para refatoração");
                
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
                    Log.Error("Falha ao iniciar o processo do Docker para refatoração");
                    return string.Empty;
                }

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
                
                // Limpar códigos de escape ANSI da saída
                result = CleanAnsiEscapeCodes(result);
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao executar análise com CodeLlama para refatoração");
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
                
                // Limpar códigos de escape ANSI
                result = CleanAnsiEscapeCodes(result);
                
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
            
            // Encontrar o início do código original no prompt
            var originalCodeStartIndex = input.IndexOf("Código Original:");
            var originalCodeBlockStartIndex = input.IndexOf("```csharp", originalCodeStartIndex);
            var originalCodeBlockEndIndex = input.IndexOf("```", originalCodeBlockStartIndex + 10);
            
            // Encontrar o início do código modificado no prompt
            var modifiedCodeStartIndex = input.IndexOf("Código Modificado:");
            var modifiedCodeBlockStartIndex = input.IndexOf("```csharp", modifiedCodeStartIndex);
            var modifiedCodeBlockEndIndex = input.IndexOf("```", modifiedCodeBlockStartIndex + 10);
            
            if (originalCodeBlockStartIndex == -1 || originalCodeBlockEndIndex == -1 || 
                modifiedCodeBlockStartIndex == -1 || modifiedCodeBlockEndIndex == -1)
            {
                // Se não encontrar os marcadores de código, dividir pela metade
                var halfLength = input.Length / 2;
                parts.Add(input.Substring(0, halfLength));
                parts.Add(input.Substring(halfLength));
                return parts;
            }
            
            // Primeira parte: instruções e contexto
            var instructionPart = "Você é um especialista em refatoração de código C#. Vou te enviar um código em partes. " +
                                 "Após receber todas as partes, analise o código completo e forneça sugestões de refatoração " +
                                 "detalhadas. Aqui está a primeira parte:\n\n" +
                                 input.Substring(0, originalCodeBlockStartIndex);
            
            // Segunda parte: código original
            var originalCodePart = input.Substring(originalCodeBlockStartIndex, originalCodeBlockEndIndex - originalCodeBlockStartIndex + 3);
            
            // Terceira parte: código modificado e instruções finais
            var modifiedCodePart = input.Substring(modifiedCodeStartIndex);
            
            // Verificar se cada parte está dentro do limite
            if (instructionPart.Length <= maxLength && 
                originalCodePart.Length <= maxLength && 
                modifiedCodePart.Length <= maxLength)
            {
                parts.Add(instructionPart);
                parts.Add(originalCodePart);
                parts.Add(modifiedCodePart);
            }
            else
            {
                // Se alguma parte ainda for muito grande, dividir em partes menores
                if (instructionPart.Length > maxLength)
                {
                    var halfLength = instructionPart.Length / 2;
                    parts.Add(instructionPart.Substring(0, halfLength));
                    parts.Add(instructionPart.Substring(halfLength));
                }
                else
                {
                    parts.Add(instructionPart);
                }
                
                if (originalCodePart.Length > maxLength)
                {
                    var halfLength = originalCodePart.Length / 2;
                    parts.Add(originalCodePart.Substring(0, halfLength));
                    parts.Add(originalCodePart.Substring(halfLength));
                }
                else
                {
                    parts.Add(originalCodePart);
                }
                
                if (modifiedCodePart.Length > maxLength)
                {
                    var halfLength = modifiedCodePart.Length / 2;
                    parts.Add(modifiedCodePart.Substring(0, halfLength));
                    parts.Add(modifiedCodePart.Substring(halfLength));
                }
                else
                {
                    parts.Add(modifiedCodePart);
                }
            }
            
            return parts;
        }

        private string CleanAnsiEscapeCodes(string input)
        {
            // Remover códigos de escape ANSI
            return Regex.Replace(input, @"\e\[[0-9;]*[a-zA-Z]", string.Empty);
        }

        private async Task ProcessCSharpFileContent(Commit commit, string path, string content)
        {
            try
            {
                Log.Information("Processando arquivo {FilePath} do primeiro commit", path);
                
                // Verificar se já existe uma análise para este arquivo neste commit
                var existingAnalyses = await _repository.GetAnalysesByCommitIdAsync(commit.Sha);
                var existingAnalysis = existingAnalyses?.FirstOrDefault(a => a.FilePath == path);
                
                if (existingAnalysis == null)
                {
                    Log.Warning("Não foi encontrada análise para o arquivo {FilePath} no commit {CommitSha}", path, commit.Sha);
                    return;
                }
                
                // Para o primeiro commit, não há código original para comparar
                Log.Information("Arquivo {FilePath} é do primeiro commit, não há código original para comparar", path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao processar arquivo C# {FilePath} do primeiro commit", path);
            }
        }

        private IEnumerable<Commit> GetLastDayCommits(Repository repo)
        {
            var yesterday = DateTimeOffset.Now.AddDays(-1);
            Log.Information("Buscando commits desde {Date}", yesterday);
            
            return repo.Commits.Where(c => c.Author.When > yesterday);
        }

        private List<TreeEntryChanges> GetChangesFromCommit(Repository repo, Commit commit)
        {
            if (commit.Parents.Any())
            {
                var parent = commit.Parents.First();
                var comparison = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
                return comparison.Added.Concat(comparison.Modified).ToList();
            }
            
            // Se for o primeiro commit, não há parent para comparar
            // Para o primeiro commit, vamos considerar todas as entradas como adicionadas
            var changes = new List<TreeEntryChanges>();
            
            foreach (var entry in commit.Tree)
            {
                if (entry.TargetType == TreeEntryTargetType.Blob)
                {
                    // Obter o conteúdo do arquivo
                    var blob = (Blob)entry.Target;
                    var content = blob.GetContentText();
                    
                    // Criar uma entrada manual para o arquivo
                    Log.Information("Adicionando arquivo {Path} do primeiro commit", entry.Path);
                    
                    // Processar o arquivo diretamente
                    if (Path.GetExtension(entry.Path).ToLower() == ".cs")
                    {
                        // Usando Task.Run para evitar deadlock em métodos síncronos que chamam métodos assíncronos
                        Task.Run(async () => 
                        {
                            await ProcessCSharpFileContent(commit, entry.Path, content);
                        }).Wait();
                    }
                }
            }
            
            return changes;
        }

        private string GetOriginalCode(Repository repo, Commit commit, string path)
        {
            try
            {
                if (!commit.Parents.Any())
                {
                    // Se for o primeiro commit, não há versão anterior
                    return string.Empty;
                }

                var parent = commit.Parents.First();
                var blob = parent[path]?.Target as Blob;
                
                if (blob == null)
                {
                    return string.Empty;
                }

                using var contentStream = blob.GetContentStream();
                using var reader = new StreamReader(contentStream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao obter código original para {Path} no commit {CommitSha}", path, commit.Sha);
                return string.Empty;
            }
        }

        private string GetFileContent(TreeEntryChanges change)
        {
            try
            {
                var fullPath = Path.Combine(_repoPath, change.Path);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllText(fullPath);
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao obter conteúdo do arquivo {Path}", change.Path);
                return string.Empty;
            }
        }
    }
}
