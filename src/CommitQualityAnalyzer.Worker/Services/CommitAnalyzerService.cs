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
                        
                        // Obter as diferenças entre a versão anterior e a atual
                        var (originalCode, modifiedCode, diffText) = GetCodeDiff(repo, commit, change.Path);
                        
                        // Se temos uma diferença significativa, analisar apenas as diferenças
                        if (!string.IsNullOrEmpty(diffText))
                        {
                            Log.Information("Analisando diferenças para {FilePath}", change.Path);
                            var analysis = await AnalyzeCodeDiff(originalCode, modifiedCode, diffText, change.Path, commit);
                            await _repository.SaveAnalysisAsync(analysis);
                        }
                        else
                        {
                            // Fallback para o método original se não conseguirmos obter diferenças
                            Log.Information("Analisando arquivo completo para {FilePath}", change.Path);
                            var analysis = await AnalyzeCode(change.Content, change.Path, commit);
                            await _repository.SaveAnalysisAsync(analysis);
                        }
                        
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
                // Se não encontrar o marcador de código, verificar se há diff
                codeStartIndex = input.IndexOf("```diff");
                
                // Se ainda não encontrar, dividir pela metade
                if (codeStartIndex == -1)
                {
                    codeStartIndex = input.Length / 2;
                }
            }
            
            // Definir o tamanho máximo absoluto para cada parte
            var absoluteMaxLength = 2000; // Abaixo do limite de 2048
            
            // Primeira parte: instruções e contexto
            var instructionPart = input.Substring(0, codeStartIndex);
            
            // Se a parte de instruções for muito grande, dividir em partes menores
            if (instructionPart.Length > absoluteMaxLength)
            {
                // Dividir a parte de instruções em partes menores
                int startIndex = 0;
                while (startIndex < instructionPart.Length)
                {
                    // Calcular o tamanho da próxima parte
                    int partLength = Math.Min(absoluteMaxLength, instructionPart.Length - startIndex);
                    
                    // Se não estamos no início ou no final, tentar encontrar um ponto de quebra natural
                    if (startIndex > 0 && startIndex + partLength < instructionPart.Length)
                    {
                        // Procurar por quebras de linha ou pontuação para dividir de forma mais natural
                        int breakPoint = instructionPart.LastIndexOf("\n\n", startIndex + partLength - 1, Math.Min(partLength, 200));
                        if (breakPoint > startIndex)
                        {
                            partLength = breakPoint - startIndex + 2; // +2 para incluir o \n\n
                        }
                        else
                        {
                            breakPoint = instructionPart.LastIndexOf(". ", startIndex + partLength - 1, Math.Min(partLength, 100));
                            if (breakPoint > startIndex)
                            {
                                partLength = breakPoint - startIndex + 2; // +2 para incluir o ". "
                            }
                        }
                    }
                    
                    // Extrair a parte
                    string part = instructionPart.Substring(startIndex, partLength);
                    
                    // Adicionar cabeçalho para partes subsequentes
                    if (startIndex > 0)
                    {
                        part = "Continuação das instruções:\n\n" + part;
                    }
                    
                    // Adicionar à lista de partes
                    parts.Add(part);
                    
                    // Avançar para a próxima parte
                    startIndex += partLength;
                }
            }
            else
            {
                // A parte de instruções cabe dentro do limite
                parts.Add(instructionPart);
            }
            
            // Segunda parte: código a ser analisado
            var codePart = input.Substring(codeStartIndex);
            
            // Se a parte de código for muito grande, dividir em partes menores
            if (codePart.Length > absoluteMaxLength)
            {
                // Encontrar o final do bloco de código
                var codeEndIndex = codePart.IndexOf("```", 10); // Começar a busca após o início do bloco
                
                if (codeEndIndex > 0 && codeEndIndex < codePart.Length - 3)
                {
                    // Dividir em: início do bloco de código, meio do código, fim do bloco + instruções finais
                    var codeStart = codePart.Substring(0, Math.Min(absoluteMaxLength, codeEndIndex));
                    parts.Add(codeStart);
                    
                    // Se o código for muito grande, dividir o meio
                    if (codeEndIndex > absoluteMaxLength)
                    {
                        int startIndex = codeStart.Length;
                        while (startIndex < codeEndIndex)
                        {
                            int partLength = Math.Min(absoluteMaxLength, codeEndIndex - startIndex);
                            string part = "Continuação do código:\n\n" + codePart.Substring(startIndex, partLength);
                            parts.Add(part);
                            startIndex += partLength;
                        }
                    }
                    
                    // Adicionar o final do código e as instruções finais
                    if (codeEndIndex + 3 < codePart.Length)
                    {
                        parts.Add("Final do código e instruções:\n\n" + codePart.Substring(codeEndIndex));
                    }
                }
                else
                {
                    // Se não conseguir identificar a estrutura do código, dividir em partes de tamanho fixo
                    int startIndex = 0;
                    while (startIndex < codePart.Length)
                    {
                        int partLength = Math.Min(absoluteMaxLength, codePart.Length - startIndex);
                        string part = (startIndex == 0) ? 
                            codePart.Substring(startIndex, partLength) : 
                            "Continuação do código:\n\n" + codePart.Substring(startIndex, partLength);
                        parts.Add(part);
                        startIndex += partLength;
                    }
                }
            }
            else
            {
                // A parte de código cabe dentro do limite
                parts.Add(codePart);
            }
            
            // Adicionar instruções finais na última parte
            if (parts.Count > 0)
            {
                var lastPart = parts[parts.Count - 1];
                if (!lastPart.Contains("Agora que você tem o código completo"))
                {
                    parts[parts.Count - 1] = lastPart + "\n\nAgora que você tem o código completo, forneça sua análise no formato JSON conforme solicitado anteriormente.";
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

        private (string OriginalCode, string ModifiedCode, string DiffText) GetCodeDiff(Repository repo, Commit commit, string path)
        {
            try
            {
                string originalCode = string.Empty;
                string modifiedCode = string.Empty;
                
                // Obter código modificado (versão atual)
                var currentBlob = commit[path]?.Target as Blob;
                if (currentBlob != null)
                {
                    using var contentStream = currentBlob.GetContentStream();
                    using var reader = new StreamReader(contentStream);
                    modifiedCode = reader.ReadToEnd();
                }
                
                // Obter código original (versão anterior)
                if (commit.Parents.Any())
                {
                    var parent = commit.Parents.First();
                    var parentBlob = parent[path]?.Target as Blob;
                    
                    if (parentBlob != null)
                    {
                        using var contentStream = parentBlob.GetContentStream();
                        using var reader = new StreamReader(contentStream);
                        originalCode = reader.ReadToEnd();
                    }
                }
                
                // Se não conseguimos obter ambas as versões, retornar vazio
                if (string.IsNullOrEmpty(originalCode) || string.IsNullOrEmpty(modifiedCode))
                {
                    return (originalCode, modifiedCode, string.Empty);
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
                
                return (originalCode, modifiedCode, diffText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao obter diferenças para {FilePath}", path);
                return (string.Empty, string.Empty, string.Empty);
            }
        }
        
        private async Task<CodeAnalysis> AnalyzeCodeDiff(string originalCode, string modifiedCode, string diffText, string filePath, Commit commit)
        {
            try
            {
                Log.Information("Analisando diferenças para {FilePath}", filePath);
                
                // Construir o prompt com as diferenças
                var prompt = BuildDiffAnalysisPrompt(originalCode, modifiedCode, diffText);
                
                // Executar a análise com o CodeLlama
                var result = await RunCodeLlama(prompt);
                
                if (string.IsNullOrEmpty(result))
                {
                    Log.Warning("CodeLlama não retornou resultado para {FilePath}", filePath);
                    
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
                Log.Error(ex, "Erro ao analisar diferenças para {FilePath}", filePath);
                
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
        
        private string BuildDiffAnalysisPrompt(string originalCode, string modifiedCode, string diffText)
        {
            return @$"Você é um especialista Sênior em análise de código DOTNET. Analise as alterações feitas no código e forneça uma avaliação detalhada considerando:

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

Diferenças entre a versão original e a versão modificada:

```diff
{diffText}
```

Responda APENAS com o JSON, sem texto adicional antes ou depois. Responda com o JSON exatamente como solicitado, garantindo que seja um JSON válido.";
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
