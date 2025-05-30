using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RefactorScore.Core.Common;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;

namespace RefactorScore.Infrastructure.LLM
{
    /// <summary>
    /// Implementação do analisador de código usando LLM
    /// </summary>
    public class AnalisadorCodigo : IAnalisadorCodigo
    {
        private readonly ILLMService _llmService;
        private readonly IGitRepository _gitRepository;
        private readonly ILogger<AnalisadorCodigo> _logger;
        private readonly Dictionary<string, Dictionary<string, CodigoLimpo>> _resultadosCache;
        private readonly IAnaliseRepository _analiseRepository;
        
        public AnalisadorCodigo(
            ILLMService llmService, 
            IGitRepository gitRepository,
            ILogger<AnalisadorCodigo> logger,
            IAnaliseRepository analiseRepository)
        {
            _llmService = llmService;
            _gitRepository = gitRepository;
            _logger = logger;
            _analiseRepository = analiseRepository;
            _resultadosCache = new Dictionary<string, Dictionary<string, CodigoLimpo>>();
        }
        
        /// <inheritdoc/>
        public async Task<AnaliseDeCommit> AnalisarCommitAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("🚀 Iniciando análise do commit {CommitId}", commitId);
                
                if (string.IsNullOrEmpty(commitId))
                    throw new ArgumentException("ID do commit não pode ser nulo ou vazio", nameof(commitId));
                
                // Verificar se já existe uma análise recente para este commit
                var analiseExistente = await _analiseRepository.ObterAnaliseRecentePorCommitAsync(commitId);
                if (analiseExistente != null)
                {
                    _logger.LogInformation("📋 Análise recente encontrada para o commit {CommitId}. Retornando resultado do banco de dados.", commitId);
                    return analiseExistente;
                }
                
                // Obter commit do repositório
                _logger.LogInformation("🔍 Buscando dados do commit {CommitId}", commitId);
                var commit = await _gitRepository.ObterCommitPorIdAsync(commitId);
                if (commit == null)
                {
                    _logger.LogError("❌ Erro ao obter commit {CommitId}: Commit não encontrado", commitId);
                    throw new Exception($"Erro ao obter commit: Commit não encontrado");
                }
                
                // Obter mudanças do commit
                _logger.LogInformation("🔍 Buscando mudanças no commit {CommitId}", commitId);
                var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commitId);
                commit.Mudancas = mudancas;
                
                _logger.LogInformation("📊 Commit {CommitId} possui {TotalArquivos} arquivos alterados, sendo {ArquivosCodigo} arquivos de código fonte",
                    commitId,
                    mudancas.Count,
                    mudancas.Count(m => m.EhCodigoFonte));
                
                // Analisar o commit obtido
                var analiseResult = await AnalisarCommitInternoAsync(commit);
                
                // Salvar a análise no banco de dados
                _logger.LogInformation("💾 Salvando análise do commit {CommitId} no banco de dados", commitId);
                await _analiseRepository.AdicionarAsync(analiseResult);
                _logger.LogInformation("✅ Análise do commit {CommitId} salva com sucesso no banco de dados", commitId);
                
                return analiseResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao analisar commit {CommitId}", commitId);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<AnaliseDeArquivo> AnalisarArquivoNoCommitAsync(string commitId, string caminhoArquivo)
        {
            try
            {
                _logger.LogInformation("Iniciando análise do arquivo {CaminhoArquivo} no commit {CommitId}", 
                    caminhoArquivo, commitId);
                
                if (string.IsNullOrEmpty(commitId))
                    throw new ArgumentException("ID do commit não pode ser nulo ou vazio", nameof(commitId));
                
                if (string.IsNullOrEmpty(caminhoArquivo))
                    throw new ArgumentException("Caminho do arquivo não pode ser nulo ou vazio", nameof(caminhoArquivo));
                
                // Verificar se já existe uma análise para este arquivo no banco
                var analiseExistente = await _analiseRepository.ObterAnalisesArquivoPorCommitAsync(commitId);
                var arquivoExistente = analiseExistente?.FirstOrDefault(a => a.CaminhoArquivo == caminhoArquivo);
                
                if (arquivoExistente != null)
                {
                    _logger.LogInformation("📋 Análise existente encontrada para o arquivo {CaminhoArquivo} no commit {CommitId}. Retornando resultado do banco de dados.", 
                        caminhoArquivo, commitId);
                    return arquivoExistente;
                }
                
                // Obter commit do repositório
                var commit = await _gitRepository.ObterCommitPorIdAsync(commitId);
                if (commit == null)
                {
                    _logger.LogError("Erro ao obter commit {CommitId}: Commit não encontrado", commitId);
                    throw new Exception($"Erro ao obter commit: Commit não encontrado");
                }
                
                // Obter mudanças do commit
                var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commitId);
                commit.Mudancas = mudancas;
                
                // Buscar o arquivo específico no commit
                var arquivo = mudancas
                    .FirstOrDefault(m => m.CaminhoArquivo.Equals(caminhoArquivo, StringComparison.OrdinalIgnoreCase));
                
                if (arquivo == null)
                {
                    _logger.LogError("Arquivo {CaminhoArquivo} não encontrado no commit {CommitId}", 
                        caminhoArquivo, commitId);
                    throw new Exception($"Arquivo {caminhoArquivo} não encontrado no commit {commitId}");
                }
                
                // Analisar o arquivo
                var resultadoAnalise = await AnalisarArquivoAsync(arquivo);
                
                // Se não foi possível analisar o arquivo, retornar null
                if (resultadoAnalise == null)
                {
                    _logger.LogWarning("Não foi possível analisar o arquivo {CaminhoArquivo} no commit {CommitId}", 
                        caminhoArquivo, commitId);
                    return null;
                }
                
                // Gerar recomendações para o arquivo
                var recomendacoes = await GerarRecomendacoesParaArquivoAsync(
                    resultadoAnalise,
                    arquivo.ConteudoModificado,
                    DeterminarLinguagem(arquivo.CaminhoArquivo));
                
                // Criar a análise de arquivo
                var analiseArquivo = new AnaliseDeArquivo
                {
                    Id = Guid.NewGuid().ToString(),
                    IdCommit = commitId,
                    CaminhoArquivo = caminhoArquivo,
                    DataAnalise = DateTime.UtcNow,
                    TipoArquivo = Path.GetExtension(caminhoArquivo),
                    Linguagem = DeterminarLinguagem(caminhoArquivo),
                    LinhasAdicionadas = arquivo.LinhasAdicionadas,
                    LinhasRemovidas = arquivo.LinhasRemovidas,
                    Analise = resultadoAnalise
                };
                
                // Salvar a análise do arquivo no banco de dados primeiro (sem recomendações)
                _logger.LogInformation("💾 Salvando análise inicial do arquivo {CaminhoArquivo} no banco de dados", caminhoArquivo);
                await _analiseRepository.SalvarAnaliseArquivoAsync(analiseArquivo);
                _logger.LogInformation("✅ Análise inicial do arquivo salva com sucesso no banco de dados");
                
                // Adicionar e salvar as recomendações
                if (recomendacoes != null && recomendacoes.Any())
                {
                    // Preparar recomendações com as referências adequadas
                    foreach (var recomendacao in recomendacoes)
                    {
                        recomendacao.ReferenciaArquivo = caminhoArquivo;
                        recomendacao.IdCommit = commitId;
                    }
                    
                    // Atualizar o objeto de análise com as recomendações
                    analiseArquivo.Recomendacoes = recomendacoes;
                    
                    // Salvar a versão atualizada com recomendações
                    _logger.LogInformation("💾 Salvando análise do arquivo com {Total} recomendações no banco de dados", 
                        recomendacoes.Count);
                    await _analiseRepository.SalvarAnaliseArquivoAsync(analiseArquivo);
                    _logger.LogInformation("✅ Análise do arquivo com recomendações salva com sucesso no banco de dados");
                }
                
                return analiseArquivo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar arquivo {CaminhoArquivo} no commit {CommitId}", 
                    caminhoArquivo, commitId);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<List<AnaliseDeCommit>> AnalisarCommitsRecentesAsync(int quantidadeDias = 1, int limitarQuantidade = 10)
        {
            try
            {
                _logger.LogInformation("Iniciando análise de commits recentes: {QuantidadeDias} dias, limitado a {LimitarQuantidade}",
                    quantidadeDias, limitarQuantidade);
                
                DateTime dataInicio = DateTime.UtcNow.AddDays(-quantidadeDias);
                DateTime dataFim = DateTime.UtcNow;
                
                // Obter commits do período
                var commits = await _gitRepository.ObterCommitsPorPeriodoAsync(dataInicio, dataFim);
                
                // Limitar quantidade se necessário
                if (commits.Count > limitarQuantidade)
                {
                    commits = commits.Take(limitarQuantidade).ToList();
                }
                
                var analises = new List<AnaliseDeCommit>();
                
                // Analisar cada commit
                foreach (var commit in commits)
                {
                    try
                    {
                        // Obter mudanças do commit
                        var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commit.Id);
                        commit.Mudancas = mudancas;
                        
                        var analiseCommit = await AnalisarCommitInternoAsync(commit);
                        analises.Add(analiseCommit);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao analisar commit {CommitId}", commit.Id);
                    }
                }
                
                return analises;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar commits recentes");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<AnaliseTemporal> GerarAnaliseTemporalAsync(string? autor = null, int dias = 30)
        {
            try
            {
                _logger.LogInformation("Iniciando análise temporal: autor {Autor}, {Dias} dias", 
                    autor ?? "todos", dias);
                
                DateTime dataInicio = DateTime.UtcNow.AddDays(-dias);
                DateTime dataFim = DateTime.UtcNow;
                
                // Obter commits do período
                var commits = await _gitRepository.ObterCommitsPorPeriodoAsync(dataInicio, dataFim);
                
                // Filtrar por autor se necessário
                if (!string.IsNullOrEmpty(autor))
                {
                    commits = commits.Where(c => c.Autor.Equals(autor, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                
                // Criar análise temporal
                var analiseTemporal = new AnaliseTemporal
                {
                    DataInicio = dataInicio,
                    DataFim = dataFim,
                    Autor = autor ?? string.Empty,
                    CommitsAnalisados = commits.Select(c => c.Id).ToList()
                };
                
                // Calcular métricas
                var metricas = CalcularMetricasTemporais(commits);
                analiseTemporal.Metricas = metricas;
                
                return analiseTemporal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar análise temporal");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<List<Recomendacao>> GerarRecomendacoesAsync(AnaliseDeCommit analiseCommit)
        {
            try
            {
                if (analiseCommit == null)
                    throw new ArgumentNullException(nameof(analiseCommit), "Análise não pode ser nula");
                
                // Verificar se já temos recomendações
                if (analiseCommit.Recomendacoes != null && analiseCommit.Recomendacoes.Any())
                    return analiseCommit.Recomendacoes;
                
                // Verificar se o LLM está disponível
                if (!await _llmService.IsAvailableAsync())
                    throw new InvalidOperationException("Serviço LLM não está disponível");
                
                var todasRecomendacoes = new List<Recomendacao>();
                
                // Recuperar os resultados do cache (se não houver no cache, não geramos recomendações)
                if (!_resultadosCache.TryGetValue(analiseCommit.IdCommit, out var resultadosAnalise))
                {
                    _logger.LogWarning("Não há análises em cache para o commit {CommitId}", analiseCommit.IdCommit);
                    return todasRecomendacoes;
                }
                
                // Gerar recomendações para cada arquivo analisado
                foreach (var arquivoAnalise in resultadosAnalise)
                {
                    // Obter conteúdo do arquivo
                    var mudanca = analiseCommit.Commit.Mudancas
                        .FirstOrDefault(m => m.CaminhoArquivo == arquivoAnalise.Key);
                    
                    if (mudanca == null)
                        continue;
                    
                    var recomendacoes = await GerarRecomendacoesParaArquivoAsync(
                        arquivoAnalise.Value,
                        mudanca.ConteudoModificado,
                        DeterminarLinguagem(arquivoAnalise.Key));
                    
                    if (recomendacoes.Any())
                    {
                        // Adicionar caminho do arquivo às recomendações
                        foreach (var recomendacao in recomendacoes)
                        {
                            recomendacao.ReferenciaArquivo = arquivoAnalise.Key;
                            todasRecomendacoes.Add(recomendacao);
                        }
                    }
                }
                
                return todasRecomendacoes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar recomendações para análise {AnaliseId}", analiseCommit?.Id);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<Dictionary<string, double>> AvaliarEvolucaoAsync(string autor, int dias = 90)
        {
            try
            {
                _logger.LogInformation("Avaliando evolução: autor {Autor}, {Dias} dias", autor, dias);
                
                if (string.IsNullOrEmpty(autor))
                    throw new ArgumentException("Autor não pode ser nulo ou vazio", nameof(autor));
                
                // Obter análise temporal
                var analiseTemporal = await GerarAnaliseTemporalAsync(autor, dias);
                
                // Adicionar métricas específicas de evolução
                var metricas = new Dictionary<string, double>(analiseTemporal.Metricas);
                
                // Aqui poderiam ser calculadas métricas adicionais de evolução
                // comparando análises de commits mais antigos com mais recentes
                
                return metricas;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao avaliar evolução para autor {Autor}", autor);
                throw;
            }
        }
        
        #region Métodos internos
        
        /// <summary>
        /// Analisa internamente um commit
        /// </summary>
        private async Task<AnaliseDeCommit> AnalisarCommitInternoAsync(Commit commit)
        {
            try
            {
                _logger.LogInformation("🔎 Iniciando análise interna do commit {CommitId} de {Autor} ({DataCommit})", 
                    commit.Id, commit.Autor, commit.Data);
                
                if (commit == null)
                    throw new ArgumentNullException(nameof(commit), "Commit não pode ser nulo");
                
                // Verificar se o LLM está disponível
                _logger.LogInformation("🔍 Verificando disponibilidade do serviço LLM");
                if (!await _llmService.IsAvailableAsync())
                {
                    _logger.LogError("❌ Serviço LLM não está disponível");
                    throw new InvalidOperationException("Serviço LLM não está disponível");
                }
                
                // Criar a análise do commit
                var analise = new AnaliseDeCommit
                {
                    Id = Guid.NewGuid().ToString(),
                    IdCommit = commit.Id,
                    DataDaAnalise = DateTime.UtcNow,
                    TipoCommit = commit.Tipo,
                    Commit = commit,
                    AnalisesDeArquivos = new List<AnaliseDeArquivo>()
                };
                
                // Armazenar resultados de análise para cache
                var resultadosAnalise = new Dictionary<string, CodigoLimpo>();
                
                // Filtrar apenas arquivos de código fonte que foram modificados
                var arquivosCodigo = commit.Mudancas
                    .Where(m => m.EhCodigoFonte && m.TipoMudanca != TipoMudanca.Removido)
                    .ToList();
                
                if (!arquivosCodigo.Any())
                {
                    _logger.LogInformation("ℹ️ Commit {CommitId} não contém mudanças em arquivos de código fonte", commit.Id);
                    analise.Justificativa = "Commit não contém mudanças em arquivos de código fonte";
                    return analise;
                }
                
                _logger.LogInformation("📊 Commit contém {Total} arquivos de código fonte", arquivosCodigo.Count);
                
                // Limitar número de arquivos para análise (no máximo 5)
                var arquivosSelecionados = arquivosCodigo;
                if (arquivosCodigo.Count > 5)
                {
                    _logger.LogInformation("⚠️ Commit com muitos arquivos ({Total}), limitando a 5 arquivos para análise", 
                        arquivosCodigo.Count);
                    arquivosSelecionados = arquivosCodigo.Take(5).ToList();
                }
                
                // Analisar cada arquivo selecionado
                int arquivoAtual = 0;
                foreach (var arquivo in arquivosSelecionados)
                {
                    arquivoAtual++;
                    _logger.LogInformation("🔎 Analisando arquivo {Atual}/{Total}: {CaminhoArquivo}", 
                        arquivoAtual, arquivosSelecionados.Count, arquivo.CaminhoArquivo);
                    
                    _logger.LogInformation("📝 Arquivo {CaminhoArquivo}: {LinhasAdd} linhas adicionadas, {LinhasRem} removidas, {TipoMudanca}", 
                        arquivo.CaminhoArquivo, arquivo.LinhasAdicionadas, arquivo.LinhasRemovidas, 
                        ConverterTipoMudancaParaDescricao(arquivo.TipoMudanca));
                    
                    _logger.LogInformation("🤖 Enviando código para análise com o LLM (Linguagem: {Linguagem})", 
                        DeterminarLinguagem(arquivo.CaminhoArquivo));
                    
                    var codigoLimpo = await AnalisarArquivoAsync(arquivo);
                    
                    if (codigoLimpo != null)
                    {
                        _logger.LogInformation("✅ Análise do arquivo {CaminhoArquivo} concluída. Nota geral: {Nota:F1}", 
                            arquivo.CaminhoArquivo, codigoLimpo.NotaGeral);
                        
                        var analiseArquivo = new AnaliseDeArquivo
                        {
                            Id = Guid.NewGuid().ToString(),
                            IdCommit = commit.Id,
                            CaminhoArquivo = arquivo.CaminhoArquivo,
                            DataAnalise = DateTime.UtcNow,
                            TipoArquivo = Path.GetExtension(arquivo.CaminhoArquivo),
                            Linguagem = DeterminarLinguagem(arquivo.CaminhoArquivo),
                            LinhasAdicionadas = arquivo.LinhasAdicionadas,
                            LinhasRemovidas = arquivo.LinhasRemovidas,
                            Analise = codigoLimpo
                        };
                        
                        // Salvar a análise do arquivo imediatamente após obter o resultado
                        _logger.LogInformation("💾 Salvando análise do arquivo {CaminhoArquivo} no banco de dados", arquivo.CaminhoArquivo);
                        await _analiseRepository.SalvarAnaliseArquivoAsync(analiseArquivo);
                        _logger.LogInformation("✅ Análise do arquivo {CaminhoArquivo} salva com sucesso no banco de dados", arquivo.CaminhoArquivo);
                        
                        analise.AnalisesDeArquivos.Add(analiseArquivo);
                        resultadosAnalise[arquivo.CaminhoArquivo] = codigoLimpo;
                        
                        // Gerar recomendações para este arquivo
                        _logger.LogInformation("🔄 Gerando recomendações para o arquivo {CaminhoArquivo}", arquivo.CaminhoArquivo);
                        var recomendacoes = await GerarRecomendacoesParaArquivoAsync(
                            codigoLimpo, 
                            arquivo.ConteudoModificado, 
                            DeterminarLinguagem(arquivo.CaminhoArquivo));
                        
                        if (recomendacoes.Any())
                        {
                            _logger.LogInformation("📋 Geradas {Total} recomendações para o arquivo {CaminhoArquivo}", 
                                recomendacoes.Count, arquivo.CaminhoArquivo);
                            
                            // Adicionar o caminho do arquivo às recomendações e salvá-las no banco
                            _logger.LogInformation("💾 Salvando {Total} recomendações para o arquivo {CaminhoArquivo} no banco de dados", 
                                recomendacoes.Count, arquivo.CaminhoArquivo);
                                
                            foreach (var recomendacao in recomendacoes)
                            {
                                recomendacao.ReferenciaArquivo = arquivo.CaminhoArquivo;
                                recomendacao.IdCommit = commit.Id;
                                recomendacao.DataCriacao = DateTime.UtcNow;
                                
                                if (string.IsNullOrEmpty(recomendacao.Id))
                                {
                                    recomendacao.Id = Guid.NewGuid().ToString();
                                }
                                
                                analise.Recomendacoes.Add(recomendacao);
                            }
                            
                            // Atualizar a análise de arquivo com as recomendações
                            analiseArquivo.Recomendacoes = recomendacoes;
                            await _analiseRepository.SalvarAnaliseArquivoAsync(analiseArquivo);
                            _logger.LogInformation("✅ Recomendações para o arquivo {CaminhoArquivo} salvas com sucesso", arquivo.CaminhoArquivo);
                        }
                        else
                        {
                            _logger.LogInformation("ℹ️ Nenhuma recomendação gerada para o arquivo {CaminhoArquivo}", 
                                arquivo.CaminhoArquivo);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Falha ao analisar arquivo {Arquivo}", arquivo.CaminhoArquivo);
                    }
                }
                
                // Armazenar os resultados em cache
                _resultadosCache[commit.Id] = resultadosAnalise;
                
                // Calcular a nota geral do commit (média das notas dos arquivos)
                if (analise.AnalisesDeArquivos.Any())
                {
                    analise.NotaGeral = analise.AnalisesDeArquivos.Average(a => a.Analise.NotaGeral);
                    
                    // Gerar justificativa
                    analise.Justificativa = GerarJustificativaCommit(analise, resultadosAnalise);
                }
                else
                {
                    analise.NotaGeral = 0;
                    analise.Justificativa = "Não foi possível analisar nenhum arquivo neste commit";
                }
                
                // Salvar a análise do commit completa no banco de dados
                _logger.LogInformation("💾 Salvando análise completa do commit {CommitId} no banco de dados", commit.Id);
                await _analiseRepository.AdicionarAsync(analise);
                _logger.LogInformation("✅ Análise completa do commit {CommitId} salva com sucesso no banco de dados", commit.Id);
                
                _logger.LogInformation("📊 Resumo: Nota geral: {NotaGeral:F1}, Arquivos analisados: {ArquivosAnalisados}, Recomendações: {Recomendacoes}", 
                    analise.NotaGeral, analise.AnalisesDeArquivos.Count, analise.Recomendacoes.Count);
                
                // Copiar propriedades do objeto Commit para a raiz do AnaliseDeCommit
                if (commit != null)
                {
                    analise.Autor = commit.Autor;
                    analise.Email = commit.Email;
                    analise.DataDoCommit = commit.Data;
                    
                    // Preencher AnaliseCodigoLimpo com base nas análises dos arquivos
                    if (analise.AnalisesDeArquivos.Any())
                    {
                        analise.AnaliseCodigoLimpo = new CodigoLimpo
                        {
                            NomenclaturaVariaveis = (int)Math.Round(analise.AnalisesDeArquivos.Average(a => a.Analise.NomenclaturaVariaveis)),
                            TamanhoFuncoes = (int)Math.Round(analise.AnalisesDeArquivos.Average(a => a.Analise.TamanhoFuncoes)),
                            UsoComentariosRelevantes = (int)Math.Round(analise.AnalisesDeArquivos.Average(a => a.Analise.UsoComentariosRelevantes)),
                            CoesaoMetodos = (int)Math.Round(analise.AnalisesDeArquivos.Average(a => a.Analise.CoesaoMetodos)),
                            EvitacaoCodigoMorto = (int)Math.Round(analise.AnalisesDeArquivos.Average(a => a.Analise.EvitacaoCodigoMorto)),
                            Justificativas = new Dictionary<string, string>()
                        };
                    }
                }
                
                return analise;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao analisar commit {CommitId}", commit?.Id);
                throw;
            }
        }
        
        /// <summary>
        /// Analisa um arquivo específico e retorna sua análise de código limpo
        /// </summary>
        private async Task<CodigoLimpo> AnalisarArquivoAsync(MudancaDeArquivoNoCommit arquivo)
        {
            try
            {
                // Validar se o arquivo é adequado para análise
                var validacao = ValidacaoArquivo.ValidarArquivo(arquivo);
                if (!validacao.Sucesso)
                {
                    _logger.LogWarning("⚠️ Arquivo {CaminhoArquivo} não é válido para análise: {Motivo}", 
                        arquivo.CaminhoArquivo, string.Join(", ", validacao.Erros));
                    return null;
                }
                
                string linguagem = DeterminarLinguagem(arquivo.CaminhoArquivo);
                string contexto = $"Este arquivo foi {ConverterTipoMudancaParaDescricao(arquivo.TipoMudanca)} " +
                    $"em um commit. Foram adicionadas {arquivo.LinhasAdicionadas} linhas e " +
                    $"removidas {arquivo.LinhasRemovidas} linhas.";
                
                // Processar conteúdo para lidar com arquivos grandes
                string conteudoProcessado = ProcessadorArquivoGrande.PrepararConteudoParaAnalise(
                    arquivo.ConteudoModificado,
                    EstimarLinhaModificada(arquivo));
                
                _logger.LogInformation("🤖 Enviando conteúdo do arquivo {CaminhoArquivo} para análise (Tamanho original: {TamanhoOriginal} bytes, Processado: {TamanhoProcessado} bytes)", 
                    arquivo.CaminhoArquivo, 
                    arquivo.ConteudoModificado?.Length ?? 0,
                    conteudoProcessado?.Length ?? 0);
                
                var resultado = await _llmService.AnalisarCodigoAsync(
                    conteudoProcessado, 
                    linguagem, 
                    contexto);
                
                if (resultado != null)
                {
                    _logger.LogInformation("✅ Análise do arquivo {CaminhoArquivo} concluída com sucesso", arquivo.CaminhoArquivo);
                }
                
                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao analisar arquivo {CaminhoArquivo}", arquivo.CaminhoArquivo);
                return null;
            }
        }
        
        /// <summary>
        /// Estima a linha central das modificações com base no tipo de mudança e conteúdo
        /// </summary>
        private int EstimarLinhaModificada(MudancaDeArquivoNoCommit arquivo)
        {
            // Para arquivos adicionados ou removidos, não há uma linha específica modificada
            if (arquivo.TipoMudanca == TipoMudanca.Adicionado || arquivo.TipoMudanca == TipoMudanca.Removido)
                return -1;
            
            // Se tivermos um diff, podemos tentar extrair informação dele
            if (!string.IsNullOrEmpty(arquivo.TextoDiff))
            {
                // Procurar por linhas de contexto do diff como "@@ -10,5 +10,8 @@"
                var diffLines = arquivo.TextoDiff.Split('\n');
                foreach (var line in diffLines)
                {
                    if (line.StartsWith("@@") && line.IndexOf("@@", 2) >= 0)
                    {
                        // Extrair o número após o +
                        var match = Regex.Match(line, @"\+(\d+),");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int lineNumber))
                        {
                            return lineNumber;
                        }
                    }
                }
            }
            
            // Se não conseguimos informação mais precisa, assumimos que as modificações
            // estão próximas do meio do arquivo
            if (!string.IsNullOrEmpty(arquivo.ConteudoModificado))
            {
                int totalLines = arquivo.ConteudoModificado.Split('\n').Length;
                return totalLines / 2;
            }
            
            return -1;
        }
        
        /// <summary>
        /// Gera recomendações para um arquivo específico
        /// </summary>
        private async Task<List<Recomendacao>> GerarRecomendacoesParaArquivoAsync(
            CodigoLimpo analise, 
            string codigoFonte, 
            string linguagem)
        {
            try
            {
                var recomendacoes = await _llmService.GerarRecomendacoesAsync(analise, codigoFonte, linguagem);
                
                // Verificar se há recomendações para salvar
                if (recomendacoes != null && recomendacoes.Count > 0)
                {
                    _logger.LogInformation("✅ Geradas {Count} recomendações válidas para o arquivo", recomendacoes.Count);
                    
                    // Preparar recomendações com IDs
                    foreach (var recomendacao in recomendacoes)
                    {
                        if (string.IsNullOrEmpty(recomendacao.Id))
                        {
                            recomendacao.Id = Guid.NewGuid().ToString();
                        }
                        
                        // A propriedade ReferenciaArquivo será definida pelo método chamador
                        // A propriedade IdCommit será definida pelo método chamador
                        recomendacao.DataCriacao = DateTime.UtcNow;
                    }
                }
                else
                {
                    _logger.LogInformation("ℹ️ Nenhuma recomendação válida gerada para o arquivo");
                }
                
                return recomendacoes ?? new List<Recomendacao>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao gerar recomendações para arquivo");
                return new List<Recomendacao>();
            }
        }
        
        /// <summary>
        /// Determina a linguagem de programação com base na extensão do arquivo
        /// </summary>
        private string DeterminarLinguagem(string caminhoArquivo)
        {
            if (string.IsNullOrEmpty(caminhoArquivo))
                return "Desconhecida";
                
            string extensao = Path.GetExtension(caminhoArquivo).ToLowerInvariant();
            
            return extensao switch
            {
                ".cs" => "C#",
                ".java" => "Java",
                ".js" => "JavaScript",
                ".ts" => "TypeScript",
                ".py" => "Python",
                ".rb" => "Ruby",
                ".php" => "PHP",
                ".go" => "Go",
                ".c" => "C",
                ".cpp" => "C++",
                ".h" => "C/C++",
                ".swift" => "Swift",
                ".kt" => "Kotlin",
                ".rs" => "Rust",
                ".sh" => "Shell",
                ".pl" => "Perl",
                ".sql" => "SQL",
                _ => "Desconhecida"
            };
        }
        
        /// <summary>
        /// Converte o tipo de mudança para uma descrição
        /// </summary>
        private string ConverterTipoMudancaParaDescricao(TipoMudanca tipoMudanca)
        {
            return tipoMudanca switch
            {
                TipoMudanca.Adicionado => "adicionado",
                TipoMudanca.Modificado => "modificado",
                TipoMudanca.Removido => "removido",
                TipoMudanca.Renomeado => "renomeado",
                _ => "modificado"
            };
        }
        
        /// <summary>
        /// Gera uma justificativa para a análise do commit
        /// </summary>
        private string GerarJustificativaCommit(AnaliseDeCommit analise, Dictionary<string, CodigoLimpo> resultadosAnalise)
        {
            // Se não tiver arquivos analisados
            if (analise.AnalisesDeArquivos.Count == 0)
                return "Não foi possível analisar nenhum arquivo neste commit";
            
            // Obter a média das notas gerais
            double mediaGeral = resultadosAnalise.Values
                .Average(a => a.NotaGeral);
            
            // Calcular quantos arquivos estão abaixo da média e quais os critérios piores
            int arquivosAbaixoMedia = resultadosAnalise.Values
                .Count(a => a.NotaGeral < mediaGeral);
            
            // Identificar os critérios com piores notas
            var criteriosPiores = IdentificarCriteriosPiores(resultadosAnalise.Values.ToList());
            
            // Gerar justificativa
            var justificativa = $"Commit com {analise.AnalisesDeArquivos.Count} arquivos analisados. " +
                $"Nota média: {mediaGeral:F1}. ";
            
            if (arquivosAbaixoMedia > 0)
            {
                justificativa += $"{arquivosAbaixoMedia} arquivos estão abaixo da média. ";
            }
            
            if (criteriosPiores.Any())
            {
                justificativa += $"Aspectos que precisam de mais atenção: {string.Join(", ", criteriosPiores)}. ";
            }
            
            if (analise.Recomendacoes.Any())
            {
                justificativa += $"Foram geradas {analise.Recomendacoes.Count} recomendações de melhoria.";
            }
            
            return justificativa;
        }
        
        /// <summary>
        /// Identifica os critérios com piores notas nas análises
        /// </summary>
        private List<string> IdentificarCriteriosPiores(List<CodigoLimpo> analises)
        {
            if (analises == null || !analises.Any())
                return new List<string>();
            
            // Calcular médias por critério
            double mediaNomenclatura = analises.Average(a => a.NomenclaturaVariaveis);
            double mediaTamanho = analises.Average(a => a.TamanhoFuncoes);
            double mediaComentarios = analises.Average(a => a.UsoComentariosRelevantes);
            double mediaCoesao = analises.Average(a => a.CoesaoMetodos);
            double mediaCodigoMorto = analises.Average(a => a.EvitacaoCodigoMorto);
            
            // Criar dicionário de critérios e médias
            var criterios = new Dictionary<string, double>
            {
                { "Nomenclatura de variáveis", mediaNomenclatura },
                { "Tamanho de funções", mediaTamanho },
                { "Comentários", mediaComentarios },
                { "Coesão", mediaCoesao },
                { "Código morto", mediaCodigoMorto }
            };
            
            // Ordenar por nota (crescente) e pegar os 2 piores
            return criterios
                .OrderBy(c => c.Value)
                .Take(2)
                .Select(c => c.Key)
                .ToList();
        }
        
        /// <summary>
        /// Calcula métricas temporais para a análise
        /// </summary>
        private Dictionary<string, double> CalcularMetricasTemporais(List<Commit> commits)
        {
            var metricas = new Dictionary<string, double>();
            
            if (commits == null || !commits.Any())
                return metricas;
            
            // Exemplo de métricas que poderiam ser calculadas
            metricas["TotalCommits"] = commits.Count;
            metricas["MediaArquivosPorCommit"] = commits.Average(c => c.Mudancas.Count);
            metricas["MediaLinhasAdicionadas"] = commits.Average(c => c.Mudancas.Sum(m => m.LinhasAdicionadas));
            metricas["MediaLinhasRemovidas"] = commits.Average(c => c.Mudancas.Sum(m => m.LinhasRemovidas));
            
            return metricas;
        }
        
        #endregion
    }
} 