using CommitQualityAnalyzer.Core.Models;
using CommitQualityAnalyzer.Core.Repositories;
using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço principal para análise de qualidade de commits
    /// </summary>
    public class CommitAnalyzerService
    {
        private readonly string _repoPath;
        private readonly ILogger<CommitAnalyzerService> _logger;
        private readonly ICodeAnalysisRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly GitDiffService _gitDiffService;
        private readonly OllamaService _ollamaService;
        private readonly ResponseAnalysisService _responseAnalysisService;
        private readonly PromptBuilderService _promptBuilderService;
        private readonly AnalysisMapperService _analysisMapperService;
        private readonly CommitSchedulerService _commitSchedulerService;
        private readonly GitRepositoryWrapper _gitRepositoryWrapper;
        private readonly CodeChunkerService _chunkerService;
        private readonly string _defaultModelName;

        public CommitAnalyzerService(
            string repoPath,
            ILogger<CommitAnalyzerService> logger,
            ICodeAnalysisRepository repository,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<GitDiffService> gitDiffLogger,
            ILogger<OllamaService> ollamaLogger,
            ILogger<ResponseAnalysisService> responseAnalysisLogger,
            ILogger<PromptBuilderService> promptBuilderLogger,
            ILogger<AnalysisMapperService> analysisMapperLogger,
            ILogger<CommitSchedulerService> commitSchedulerLogger,
            ILogger<GitRepositoryWrapper> gitRepositoryWrapperLogger,
            CodeChunkerService chunkerService)
        {
            _repoPath = repoPath;
            _logger = logger;
            _repository = repository;
            _configuration = configuration;
            _defaultModelName = _configuration.GetValue<string>("Ollama:ModelName", "codellama");
            
            // Inicializar serviços auxiliares
            _gitRepositoryWrapper = new GitRepositoryWrapper(gitRepositoryWrapperLogger, repoPath);
            _gitDiffService = new GitDiffService(gitDiffLogger, _gitRepositoryWrapper, repoPath);
            _ollamaService = new OllamaService(ollamaLogger, configuration, httpClientFactory.CreateClient());
            _responseAnalysisService = new ResponseAnalysisService(responseAnalysisLogger);
            _promptBuilderService = new PromptBuilderService(promptBuilderLogger);
            _analysisMapperService = new AnalysisMapperService(analysisMapperLogger);
            _commitSchedulerService = new CommitSchedulerService(repoPath, commitSchedulerLogger, _gitRepositoryWrapper, chunkerService);
            _chunkerService = chunkerService;
        }

        /// <summary>
        /// Analisa os commits do último dia
        /// </summary>
        public async Task AnalyzeLastDayCommits()
        {
            _logger.LogInformation("Iniciando análise do repositório: {RepoPath}", _repoPath);
            
            // Obter commits do último dia
            var commits = _commitSchedulerService.GetLastDayCommits();
            
            // Processar cada commit
            await _commitSchedulerService.ProcessCommitsWithLogging(commits, async (commit) => {
                await ProcessCommitFiles(commit);
            });
        }

        /// <summary>
        /// Analisa um commit específico
        /// </summary>
        public async Task AnalyzeCommit(string commitId)
        {
            _logger.LogInformation("Iniciando análise do commit específico: {CommitId}", commitId);
            
            // Obter commit pelo ID
            var commit = _commitSchedulerService.GetCommitById(commitId);
            
            if (commit == null)
            {
                return;
            }
            
            // Processar o commit
            await ProcessCommitFiles(commit);
        }
        
        /// <summary>
        /// Verifica se um arquivo deve ser ignorado na análise de qualidade de código
        /// </summary>
        private bool ShouldIgnoreFile(string filePath)
        {
            // Ignorar arquivos gerados automaticamente
            if (filePath.Contains("AssemblyInfo.cs") || 
                filePath.Contains(".g.cs") || 
                filePath.Contains(".generated.cs") ||
                filePath.Contains(".designer.cs"))
            {
                _logger.LogInformation("Ignorando arquivo gerado automaticamente: {FilePath}", filePath);
                return true;
            }
            
            // Ignorar arquivos em diretórios de build e obj
            if (filePath.Contains("/obj/") || 
                filePath.Contains("\\obj\\") || 
                filePath.Contains("/bin/") || 
                filePath.Contains("\\bin\\"))
            {
                _logger.LogInformation("Ignorando arquivo em diretório de build: {FilePath}", filePath);
                return true;
            }
            
            // Ignorar arquivos de teste
            if (filePath.Contains(".Test") || 
                filePath.Contains(".Tests") || 
                filePath.Contains("Test.cs") || 
                filePath.Contains("Tests.cs"))
            {
                _logger.LogInformation("Ignorando arquivo de teste: {FilePath}", filePath);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Processa os arquivos modificados em um commit
        /// </summary>
        private async Task ProcessCommitFiles(CommitInfo commit)
        {
            try
            {
                // Obter as mudanças do commit usando o wrapper do repositório Git
                var changes = _gitRepositoryWrapper.GetCommitChangesWithDiff(commit.Sha);
                
                _logger.LogInformation("Encontradas {ChangeCount} alterações no commit {CommitId}", 
                    changes.Count, commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)));
                
                // Filtrar apenas arquivos C# e ignorar arquivos que devem ser ignorados
                var csharpFiles = changes
                    .Where(c => Path.GetExtension(c.FilePath).ToLower() == ".cs" && !ShouldIgnoreFile(c.FilePath))
                    .ToList();
                
                _logger.LogInformation("Encontrados {FileCount} arquivos C# relevantes para analisar", csharpFiles.Count);
                
                // Processar cada arquivo modificado sequencialmente
                foreach (var change in csharpFiles)
                {
                    // Verificar se o arquivo já foi analisado para este commit
                    var existingAnalysis = await _repository.GetAnalysisByCommitAndFileAsync(commit.Sha, change.FilePath);
                    if (existingAnalysis != null)
                    {
                        _logger.LogInformation("Arquivo {FilePath} já foi analisado anteriormente para o commit {CommitId}", 
                            change.FilePath, commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)));
                        continue;
                    }
                    
                    _logger.LogInformation("Analisando arquivo: {FilePath}", change.FilePath);
                    
                    try
                    {
                        // Verificar se o arquivo é muito grande (tamanho em bytes ou conteúdo)
                        int maxFileSizeBytes = _configuration.GetValue<int>("Analysis:MaxFileSizeBytes", 500000); // 500KB por padrão
                        int maxContentLength = _configuration.GetValue<int>("Analysis:MaxContentLength", 50000); // 50K caracteres por padrão
                        
                        if (change.FileSize > maxFileSizeBytes)
                        {
                            _logger.LogWarning("Arquivo {FilePath} muito grande para análise. Tamanho: {Size} bytes (limite: {MaxSize} bytes)", 
                                change.FilePath, change.FileSize, maxFileSizeBytes);
                            continue;
                        }
                        
                        if (change.ModifiedContent.Length > maxContentLength || change.OriginalContent.Length > maxContentLength)
                        {
                            _logger.LogWarning("Conteúdo do arquivo {FilePath} muito grande para análise. Tamanho: {Size} caracteres (limite: {MaxSize} caracteres)", 
                                change.FilePath, Math.Max(change.ModifiedContent.Length, change.OriginalContent.Length), maxContentLength);
                            continue;
                        }
                        
                        // Verificar se há conteúdo para analisar
                        if (string.IsNullOrWhiteSpace(change.DiffText))
                        {
                            _logger.LogInformation("Arquivo {FilePath} não possui diferenças significativas para análise", change.FilePath);
                            continue;
                        }
                        
                        // Se o diff ou conteúdo é grande, dividir em partes coerentes usando CodeChunkerService
                        var analyses = new List<CommitAnalysisResult>();
                        var codeParts = _chunkerService.Split(change.ModifiedContent).ToList();
                        
                        _logger.LogInformation("Arquivo {FilePath} dividido em {PartCount} partes para análise", 
                            change.FilePath, codeParts.Count);
                            
                        int partIndex = 0;
                        foreach (var part in codeParts)
                        {
                            _logger.LogInformation("Analisando parte {PartIndex} de {TotalParts} do arquivo {FilePath}", 
                                partIndex + 1, codeParts.Count, change.FilePath);
                                
                            try
                            {
                                // Gerar prompt para análise de código (não de diff)
                                var prompt = _promptBuilderService.BuildCodeAnalysisPrompt(
                                    change.FilePath, part, commit, partIndex);
                                    
                                // Obter o modelo a ser usado da configuração
                                var model = _configuration.GetValue<string>("Ollama:ModelName", "deepseek-extended");
                                
                                // Processar o prompt usando o serviço Ollama
                                var response = await _ollamaService.ProcessPrompt(prompt, model);
                                
                                if (string.IsNullOrEmpty(response))
                                {
                                    _logger.LogWarning("Resposta vazia para a parte {PartIndex} do arquivo {FilePath}", 
                                        partIndex, change.FilePath);
                                    continue;
                                }
                                
                                // Converter a resposta em um objeto estruturado
                                var analysisResult = _responseAnalysisService.ConvertTextResponseToJson(response);
                                analyses.Add(analysisResult);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Erro ao analisar parte {PartIndex} do arquivo {FilePath}: {ErrorMessage}", 
                                    partIndex, change.FilePath, ex.Message);
                            }
                            
                            partIndex++;
                        }

                        // Se não conseguiu analisar nenhuma parte, usar fallback
                        if (analyses.Count == 0)
                        {
                            _logger.LogWarning("Nenhuma parte do arquivo {FilePath} pôde ser analisada. Usando fallback.", change.FilePath);
                            analyses.Add(_responseAnalysisService.CreateFallbackAnalysisResult());
                        }
                        
                        // Por enquanto, usar apenas o primeiro resultado (futuramente combinar)
                        var analysis = analyses.FirstOrDefault();
                        
                        // Mapear e salvar a análise no repositório
                        var codeAnalysis = _analysisMapperService.MapToCodeAnalysis(analysis, commit, change.FilePath);
                        await _repository.SaveAnalysisAsync(codeAnalysis);
                        
                        _logger.LogInformation("Análise salva com sucesso para {FilePath} no commit {CommitId}", 
                            change.FilePath, commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)));
                        
                        // Aguardar um pouco antes de processar o próximo arquivo para evitar sobrecarga
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao analisar arquivo {FilePath}: {ErrorMessage}", 
                            change.FilePath, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar commit {CommitId}: {ErrorMessage}", 
                    commit.Sha, ex.Message);
            }
        }

        /// <summary>
        /// Analisa as diferenças de código em um arquivo
        /// </summary>
        private async Task<CommitAnalysisResult> AnalyzeCodeDiff(CommitChangeInfo change, CommitInfo commit)
        {
            _logger.LogInformation("Analisando diferenças para {FilePath}", change.FilePath);
            
            try
            {
                // Verificar se temos diferenças para analisar
                if (string.IsNullOrEmpty(change.DiffText))
                {
                    _logger.LogWarning("Sem diferenças significativas para analisar em {FilePath}", change.FilePath);
                    return _responseAnalysisService.CreateFallbackAnalysisResult();
                }
                
                // Registrar informações detalhadas para debug
                _logger.LogDebug("Analisando arquivo {FilePath} com {ChangeType}, tamanho: {FileSize} bytes, conteúdo modificado: {ContentLength} caracteres", 
                    change.FilePath, change.ChangeType, change.FileSize, change.ModifiedContent.Length);
                
                // Construir o prompt para análise
                var prompt = BuildAnalysisPrompt(change.DiffText);
                
                // Testar conexão com o modelo
                var modelName = _configuration.GetValue<string>("Ollama:ModelName", "codellama");
                var isConnected = await _ollamaService.TestOllamaConnection(modelName);
                
                if (!isConnected)
                {
                    _logger.LogError("Não foi possível conectar ao modelo Ollama");
                    return _responseAnalysisService.CreateFallbackAnalysisResult();
                }
                
                // Processar o prompt com o modelo
                var response = await _ollamaService.ProcessPrompt(prompt, modelName);
                
                if (string.IsNullOrEmpty(response))
                {
                    _logger.LogError("Resposta vazia do modelo Ollama");
                    return _responseAnalysisService.CreateFallbackAnalysisResult();
                }
                
                // Converter a resposta em um objeto estruturado
                var analysisResult = _responseAnalysisService.ConvertTextResponseToJson(response);
                
                _logger.LogInformation("Análise concluída com sucesso para {FilePath}", change.FilePath);
                
                return analysisResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar diferenças para {FilePath}: {ErrorMessage}", 
                    change.FilePath, ex.Message);
                return _responseAnalysisService.CreateFallbackAnalysisResult();
            }
        }

        private string BuildAnalysisPrompt(string code)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Você é um especialista em Clean Code em C#. Analise o trecho de código abaixo exclusivamente sob a ótica de Clean Code.");
            sb.AppendLine("Avalie, de 0 a 10, cada um dos aspectos listados, justificando em uma frase ou duas. Em seguida, forneça um resumo geral.");
            sb.AppendLine();
            sb.AppendLine("Responda SOMENTE com o JSON no formato abaixo (sem texto extra):");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"analiseGeral\": {");
            sb.AppendLine("    \"nomenclaturaVariaveis\": { \"nota\": 0-10, \"comentario\": \"...\" },");
            sb.AppendLine("    \"nomenclaturaMetodos\":  { \"nota\": 0-10, \"comentario\": \"...\" },");
            sb.AppendLine("    \"tamanhoFuncoes\":       { \"nota\": 0-10, \"comentario\": \"...\" },");
            sb.AppendLine("    \"comentarios\":          { \"nota\": 0-10, \"comentario\": \"...\" },");
            sb.AppendLine("    \"duplicacaoCodigo\":     { \"nota\": 0-10, \"comentario\": \"...\" }");
            sb.AppendLine("  },");
            sb.AppendLine("  \"comentarioGeral\": \"resumo geral da qualidade de Clean Code no arquivo\",");
            sb.AppendLine("  \"propostaRefatoracao\": {");
            sb.AppendLine("    \"titulo\": \"título curto da proposta\",");
            sb.AppendLine("    \"descricao\": \"descrição do que deve ser melhorado e por quê\",");
            sb.AppendLine("    \"codigoOriginal\": \"trecho relevante original (pode ser resumido)\",");
            sb.AppendLine("    \"codigoRefatorado\": \"exemplo de como ficaria após aplicar Clean Code\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Cada *nota* deve ser um número inteiro.");
            sb.AppendLine();
            sb.AppendLine("Código para análise:");
            sb.AppendLine();
            sb.AppendLine(code);
            
            return sb.ToString();
        }
    }
}
