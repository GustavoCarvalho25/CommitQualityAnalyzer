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
                
                // Obter as diferenças entre a versão original e a modificada
                var diffText = GetCodeDiff(originalCode, modifiedCode);
                Log.Information("Diferenças identificadas para {FilePath}: {DiffLength} caracteres", change.Path, diffText?.Length ?? 0);
                
                // Se temos diferenças significativas, incluí-las na proposta de refatoração
                RefactoringProposal proposal;
                if (!string.IsNullOrEmpty(diffText))
                {
                    proposal = await GenerateRefactoringProposalWithDiff(originalCode, modifiedCode, diffText, change.Path);
                }
                else
                {
                    // Fallback para o método original se não conseguirmos obter diferenças
                    proposal = await GenerateRefactoringProposal(originalCode, modifiedCode, change.Path);
                }
                
                if (proposal != null)
                {
                    // Atualizar a análise existente com a proposta de refatoração
                    existingAnalysis.RefactoringProposals ??= new List<RefactoringProposal>();
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

        private string GetCodeDiff(string originalCode, string modifiedCode)
        {
            try
            {
                if (string.IsNullOrEmpty(originalCode) || string.IsNullOrEmpty(modifiedCode))
                {
                    return string.Empty;
                }
                
                // Gerar texto de diferença simplificado
                var diffBuilder = new StringBuilder();
                var originalLines = originalCode.Split('\n');
                var modifiedLines = modifiedCode.Split('\n');
                
                // Usar um algoritmo simples de diferença para identificar linhas alteradas
                var diff = new List<string>();
                
                // Limite o tamanho para evitar processamento excessivo
                var maxLines = Math.Min(1000, Math.Max(originalLines.Length, modifiedLines.Length));
                originalLines = originalLines.Take(maxLines).ToArray();
                modifiedLines = modifiedLines.Take(maxLines).ToArray();
                
                // Encontrar linhas adicionadas, removidas ou modificadas
                int i = 0, j = 0;
                while (i < originalLines.Length || j < modifiedLines.Length)
                {
                    // Ambas as linhas existem
                    if (i < originalLines.Length && j < modifiedLines.Length)
                    {
                        if (originalLines[i] == modifiedLines[j])
                        {
                            // Linhas iguais - manter contexto limitado
                            if (diff.Count > 0 && diff.Count < 5)
                            {
                                diff.Add("  " + modifiedLines[j]);
                            }
                            i++;
                            j++;
                        }
                        else
                        {
                            // Verificar se é uma modificação, adição ou remoção
                            var foundMatch = false;
                            
                            // Procurar por linhas removidas (presentes no original, ausentes no modificado)
                            for (int k = i + 1; k < Math.Min(i + 5, originalLines.Length); k++)
                            {
                                if (j < modifiedLines.Length && originalLines[k] == modifiedLines[j])
                                {
                                    // Linhas removidas
                                    for (int m = i; m < k; m++)
                                    {
                                        diff.Add("- " + originalLines[m]);
                                    }
                                    i = k;
                                    foundMatch = true;
                                    break;
                                }
                            }
                            
                            if (!foundMatch)
                            {
                                // Procurar por linhas adicionadas (ausentes no original, presentes no modificado)
                                for (int k = j + 1; k < Math.Min(j + 5, modifiedLines.Length); k++)
                                {
                                    if (i < originalLines.Length && modifiedLines[k] == originalLines[i])
                                    {
                                        // Linhas adicionadas
                                        for (int m = j; m < k; m++)
                                        {
                                            diff.Add("+ " + modifiedLines[m]);
                                        }
                                        j = k;
                                        foundMatch = true;
                                        break;
                                    }
                                }
                            }
                            
                            if (!foundMatch)
                            {
                                // Modificação de linha
                                diff.Add("- " + originalLines[i]);
                                diff.Add("+ " + modifiedLines[j]);
                                i++;
                                j++;
                            }
                        }
                    }
                    // Apenas linhas originais restantes (removidas)
                    else if (i < originalLines.Length)
                    {
                        diff.Add("- " + originalLines[i]);
                        i++;
                    }
                    // Apenas linhas modificadas restantes (adicionadas)
                    else if (j < modifiedLines.Length)
                    {
                        diff.Add("+ " + modifiedLines[j]);
                        j++;
                    }
                }
                
                // Limitar o tamanho do diff para evitar prompts muito grandes
                var diffText = string.Join("\n", diff.Take(500));
                
                return diffText;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao gerar diferenças entre versões de código");
                return string.Empty;
            }
        }
        
        private async Task<RefactoringProposal> GenerateRefactoringProposalWithDiff(string originalCode, string modifiedCode, string diffText, string filePath)
        {
            try
            {
                Log.Information("Gerando proposta de refatoração com diferenças para {FilePath}", filePath);
                
                // Truncar o código se for muito longo
                var truncatedOriginalCode = TruncateCodeForPrompt(originalCode);
                var truncatedModifiedCode = TruncateCodeForPrompt(modifiedCode);
                
                // Construir o prompt com as diferenças
                var prompt = BuildRefactoringPromptWithDiff(truncatedOriginalCode, truncatedModifiedCode, diffText);
                
                // Executar a análise com o CodeLlama
                var result = await RunCodeLlama(prompt);
                
                if (string.IsNullOrEmpty(result))
                {
                    Log.Warning("CodeLlama não retornou resultado para {FilePath}", filePath);
                    return null;
                }
                
                // Criar a proposta de refatoração
                return new RefactoringProposal
                {
                    FilePath = filePath,
                    GeneratedDate = DateTime.Now,
                    Proposal = result,
                    Status = "Pending"
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao gerar proposta de refatoração com diferenças para {FilePath}", filePath);
                return null;
            }
        }
        
        private string BuildRefactoringPromptWithDiff(string originalCode, string modifiedCode, string diffText)
        {
            return @$"Você é um especialista em refatoração de código C#. Analise as diferenças entre a versão original e a versão modificada do código abaixo e forneça sugestões detalhadas de refatoração para melhorar ainda mais o código.

Código Original:
```csharp
{originalCode}
```

Código Modificado:
```csharp
{modifiedCode}
```

Diferenças entre as versões:
```diff
{diffText}
```

Forneça uma análise detalhada das mudanças realizadas e sugira melhorias adicionais que poderiam ser aplicadas para:
1. Melhorar a legibilidade e manutenibilidade
2. Seguir os princípios SOLID
3. Aplicar padrões de design apropriados
4. Aumentar a testabilidade
5. Melhorar a segurança e robustez

Sua resposta deve incluir:
- Análise das alterações já realizadas
- Sugestões específicas de refatoração adicional
- Exemplos de código para implementar as sugestões
- Justificativa para cada sugestão

Seja específico e forneça exemplos concretos de código quando possível.";
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

        private string TruncateCodeForPrompt(string code)
        {
            if (code.Length <= _maxPromptLength)
            {
                return code;
            }
            return code.Substring(0, _maxPromptLength) + "\n// ... código truncado ...\n";
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
            
            // Definir o tamanho máximo absoluto para cada parte
            var absoluteMaxLength = 2000; // Abaixo do limite de 2048
            
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
                // Se não encontrar os marcadores de código, dividir de forma inteligente
                int codeStartIndex = input.Length / 2;
                
                // Tentar encontrar um ponto de quebra natural
                int breakPoint = input.LastIndexOf("\n\n", codeStartIndex, Math.Min(codeStartIndex, 200));
                if (breakPoint > 0)
                {
                    codeStartIndex = breakPoint + 2; // +2 para incluir o \n\n
                }
                
                // Dividir em partes de tamanho adequado
                DivideAndAddParts(parts, input.Substring(0, codeStartIndex), absoluteMaxLength, "Instruções");
                DivideAndAddParts(parts, input.Substring(codeStartIndex), absoluteMaxLength, "Código");
                
                return parts;
            }
            
            // Dividir o prompt em três partes principais
            
            // Primeira parte: instruções e contexto até o início do código original
            var instructionPart = input.Substring(0, originalCodeBlockStartIndex);
            DivideAndAddParts(parts, instructionPart, absoluteMaxLength, "Instruções");
            
            // Segunda parte: código original
            var originalCodePart = input.Substring(originalCodeBlockStartIndex, originalCodeBlockEndIndex - originalCodeBlockStartIndex + 3);
            DivideAndAddParts(parts, originalCodePart, absoluteMaxLength, "Código Original");
            
            // Terceira parte: código modificado e instruções finais
            var modifiedCodePart = input.Substring(modifiedCodeStartIndex);
            DivideAndAddParts(parts, modifiedCodePart, absoluteMaxLength, "Código Modificado");
            
            // Adicionar instruções finais na última parte
            if (parts.Count > 0)
            {
                var lastPart = parts[parts.Count - 1];
                if (!lastPart.Contains("Agora que você tem o código completo"))
                {
                    parts[parts.Count - 1] = lastPart + "\n\nAgora que você tem o código completo, forneça suas sugestões de refatoração conforme solicitado anteriormente.";
                }
            }
            
            // Verificar se todas as partes estão dentro do limite
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Length > absoluteMaxLength)
                {
                    Log.Warning("Parte {Index} do prompt ainda excede o tamanho máximo: {Length} caracteres", i, parts[i].Length);
                    // Truncar a parte se ainda estiver muito grande
                    parts[i] = parts[i].Substring(0, absoluteMaxLength);
                }
            }
            
            return parts;
        }
        
        private void DivideAndAddParts(List<string> parts, string content, int maxLength, string sectionName)
        {
            // Se o conteúdo for menor que o tamanho máximo, adicionar diretamente
            if (content.Length <= maxLength)
            {
                parts.Add(content);
                return;
            }
            
            // Dividir o conteúdo em partes menores
            int startIndex = 0;
            while (startIndex < content.Length)
            {
                // Calcular o tamanho da próxima parte
                int partLength = Math.Min(maxLength, content.Length - startIndex);
                
                // Se não estamos no início ou no final, tentar encontrar um ponto de quebra natural
                if (startIndex > 0 && startIndex + partLength < content.Length)
                {
                    // Procurar por quebras de linha ou pontuação para dividir de forma mais natural
                    int breakPoint = content.LastIndexOf("\n\n", startIndex + partLength - 1, Math.Min(partLength, 200));
                    if (breakPoint > startIndex)
                    {
                        partLength = breakPoint - startIndex + 2; // +2 para incluir o \n\n
                    }
                    else
                    {
                        breakPoint = content.LastIndexOf(". ", startIndex + partLength - 1, Math.Min(partLength, 100));
                        if (breakPoint > startIndex)
                        {
                            partLength = breakPoint - startIndex + 2; // +2 para incluir o ". "
                        }
                    }
                }
                
                // Extrair a parte
                string part = content.Substring(startIndex, partLength);
                
                // Adicionar cabeçalho para partes subsequentes
                if (startIndex > 0)
                {
                    part = $"Continuação da seção {sectionName}:\n\n" + part;
                }
                else if (parts.Count > 0)
                {
                    // Se não é a primeira parte do conteúdo, mas é a primeira parte desta seção
                    part = $"Seção {sectionName}:\n\n" + part;
                }
                
                // Adicionar à lista de partes
                parts.Add(part);
                
                // Avançar para a próxima parte
                startIndex += partLength;
            }
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
