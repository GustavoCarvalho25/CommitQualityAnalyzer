using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;
using System.Text.RegularExpressions;
using System.Linq;

namespace RefactorScore.Infrastructure.LLM
{
    /// <summary>
    /// Implementação do serviço LLM utilizando Ollama
    /// </summary>
    public class OllamaService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;
        private readonly ILogger<OllamaService> _logger;
        private readonly PromptTemplates _promptTemplates;
        
        public OllamaService(HttpClient httpClient, IOptions<OllamaOptions> options, 
            ILogger<OllamaService> logger, PromptTemplates promptTemplates)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
            _promptTemplates = promptTemplates;
            
            // Configurar o client HTTP com a URL base do Ollama
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            
            // Garantir que o timeout do HttpClient está configurado corretamente
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSegundos);
            
            _logger.LogInformation("🤖 Serviço Ollama inicializado com base URL: {BaseUrl}", _options.BaseUrl);
            _logger.LogInformation("⏱️ Timeout configurado: {TimeoutSegundos} segundos", _options.TimeoutSegundos);
            _logger.LogInformation("⏱️ HttpClient Timeout: {HttpClientTimeout} segundos", _httpClient.Timeout.TotalSeconds);
        }
        
        /// <inheritdoc/>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                _logger.LogInformation("🔍 Verificando disponibilidade do serviço Ollama em {BaseUrl}", _options.BaseUrl);
                var modelos = await ObterModelosDisponiveisAsync();
                
                bool disponivel = modelos.Count > 0;
                
                if (disponivel)
                {
                    _logger.LogInformation("✅ Serviço Ollama está disponível. Encontrados {NumModelos} modelos.", modelos.Count);
                }
                else
                {
                    _logger.LogWarning("⚠️ Serviço Ollama não está disponível. Nenhum modelo encontrado.");
                }
                
                return disponivel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao verificar disponibilidade do serviço Ollama");
                return false;
            }
        }
        
        /// <inheritdoc/>
        public async Task<string> ProcessarPromptAsync(
            string prompt, 
            string? modelo = null, 
            float temperatura = 0.1f, 
            int maxTokens = 2048)
        {
            try
            {
                // Usar o modelo configurado se não for especificado
                string modeloAtual = string.IsNullOrEmpty(modelo) 
                    ? _options.ModeloPadrao 
                    : modelo;
                
                _logger.LogInformation("🤖 Processando prompt usando modelo {Modelo} (temperatura: {Temperatura}, maxTokens: {MaxTokens})", 
                    modeloAtual, temperatura, maxTokens);
                
                // Verificar se o modelo está disponível
                _logger.LogInformation("🔍 Verificando disponibilidade do modelo {Modelo}", modeloAtual);
                if (!await VerificarModeloDisponivelAsync(modeloAtual))
                {
                    _logger.LogError("❌ Modelo '{Modelo}' não está disponível", modeloAtual);
                    throw new InvalidOperationException($"Modelo '{modeloAtual}' não está disponível");
                }
                
                // Preparar a requisição para o Ollama
                var request = new OllamaRequest
                {
                    Model = modeloAtual,
                    Prompt = prompt,
                    Temperature = temperatura,
                    MaxTokens = maxTokens,
                    Stream = false
                };
                
                // Fazer a requisição
                _logger.LogInformation("📤 Enviando requisição para Ollama API (tamanho do prompt: {TamanhoPrompt} caracteres)", 
                    prompt.Length);
                
                var startTime = DateTime.UtcNow;
                var response = await _httpClient.PostAsJsonAsync("/api/generate", request);
                
                // Verificar o resultado da requisição
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Falha na requisição ao Ollama: {StatusCode} - {ReasonPhrase}", 
                        (int)response.StatusCode, response.ReasonPhrase);
                    response.EnsureSuccessStatusCode(); // Isso vai lançar uma exceção com os detalhes
                }
                
                // Processar a resposta
                var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                var responseTime = DateTime.UtcNow - startTime;
                
                _logger.LogInformation("📥 Resposta recebida do Ollama em {ResponseTime:N2}s (tamanho: {TamanhoResposta} caracteres, tokens gerados: {TokensGerados})", 
                    responseTime.TotalSeconds,
                    ollamaResponse?.Response?.Length ?? 0,
                    ollamaResponse?.EvalCount ?? 0);
                
                return ollamaResponse?.Response ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar prompt no Ollama");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<CodigoLimpo> AnalisarCodigoAsync(
            string codigo, 
            string linguagem, 
            string? contexto = null)
        {
            try
            {
                _logger.LogInformation("🔍 Iniciando análise de código na linguagem {Linguagem}", linguagem);
                
                // Verificar se o código é muito grande para processar de uma vez
                const int TAMANHO_MAXIMO_CHUNK = 7000;
                if (codigo.Length > TAMANHO_MAXIMO_CHUNK)
                {
                    _logger.LogWarning("⚠️ Código muito grande ({TamanhoTotal} caracteres). Dividindo em chunks para evitar timeout.", 
                        codigo.Length);
                    return await AnalisarCodigoGrandeEmChunksAsync(codigo, linguagem, contexto);
                }
                
                // Construir o prompt usando o template de análise de código
                string prompt = _promptTemplates.AnaliseCodigo
                    .Replace("{{CODIGO}}", codigo)
                    .Replace("{{LINGUAGEM}}", linguagem)
                    .Replace("{{CONTEXTO}}", contexto ?? "Nenhum contexto adicional fornecido.");
                
                _logger.LogInformation("🤖 Preparando análise de código: {TamanhoArquivo} caracteres, linguagem: {Linguagem}", 
                    codigo.Length, linguagem);
                
                // Enviar para o LLM
                string resposta = await ProcessarPromptAsync(prompt, _options.ModeloAnalise);
                
                // Processar a resposta para extrair a análise formatada
                _logger.LogInformation("🔄 Processando resposta de análise de código");
                var analise = await ProcessarRespostaAnalise(resposta);
                
                _logger.LogInformation("✅ Análise de código concluída. Nota geral: {NotaGeral:F1}", analise.NotaGeral);
                
                return analise;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao analisar código no Ollama");
                throw;
            }
        }
        
        /// <summary>
        /// Analisa código grande dividindo-o em chunks para evitar timeouts
        /// </summary>
        private async Task<CodigoLimpo> AnalisarCodigoGrandeEmChunksAsync(
            string codigo, 
            string linguagem, 
            string? contexto = null)
        {
            try
            {
                _logger.LogInformation("🔄 Iniciando análise de código grande em chunks");
                
                // Dividir o código em chunks
                var chunks = DividirCodigoEmChunks(codigo, 7000);
                _logger.LogInformation("📊 Código dividido em {NumeroChunks} chunks", chunks.Count);
                
                // Analisar cada chunk separadamente
                var resultadosChunks = new List<CodigoLimpo>();
                int chunkAtual = 0;
                
                foreach (var chunk in chunks)
                {
                    chunkAtual++;
                    _logger.LogInformation("🔍 Analisando chunk {ChunkAtual}/{TotalChunks} (tamanho: {TamanhoChunk} caracteres)",
                        chunkAtual, chunks.Count, chunk.Length);
                    
                    // Adicionar contexto sobre o chunking
                    string contextoChunk = $"{contexto ?? "Nenhum contexto adicional."} Este é o chunk {chunkAtual} de {chunks.Count} do arquivo completo.";
                    
                    // Analisar o chunk
                    try
                    {
                        // Construir o prompt para o chunk
                        string prompt = _promptTemplates.AnaliseCodigo
                            .Replace("{{CODIGO}}", chunk)
                            .Replace("{{LINGUAGEM}}", linguagem)
                            .Replace("{{CONTEXTO}}", contextoChunk);
                        
                        // Enviar para o LLM com timeout reduzido para chunks
                        string resposta = await ProcessarPromptAsync(prompt, _options.ModeloAnalise, 0.1f, 
                            Math.Min(2048, _options.MaxTokens));
                        
                        // Processar a resposta
                        var analiseChunk = await ProcessarRespostaAnalise(resposta);
                        
                        if (analiseChunk != null)
                        {
                            resultadosChunks.Add(analiseChunk);
                            _logger.LogInformation("✅ Chunk {ChunkAtual} analisado com sucesso. Nota: {NotaGeral:F1}", 
                                chunkAtual, analiseChunk.NotaGeral);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Erro ao analisar chunk {ChunkAtual}", chunkAtual);
                        // Continuar para o próximo chunk mesmo em caso de erro
                    }
                }
                
                // Se não conseguimos analisar nenhum chunk, retornar objeto padrão
                if (resultadosChunks.Count == 0)
                {
                    _logger.LogWarning("⚠️ Não foi possível analisar nenhum chunk do código. Usando valores padrão.");
                    return CriarCodigoLimpoPadrao("Falha ao analisar todos os chunks");
                }
                
                // Combinar os resultados dos chunks
                var resultadoCombinado = CombinarResultadosChunks(resultadosChunks);
                
                _logger.LogInformation("✅ Análise combinada de {NumChunks} chunks concluída. Nota geral: {NotaGeral:F1}", 
                    resultadosChunks.Count, resultadoCombinado.NotaGeral);
                
                return resultadoCombinado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao analisar código grande em chunks");
                throw;
            }
        }
        
        /// <summary>
        /// Divide um código em chunks inteligentes, tentando manter a integridade das funções
        /// </summary>
        private List<string> DividirCodigoEmChunks(string codigo, int tamanhoMaximoChunk)
        {
            var chunks = new List<string>();
            
            // Se o código já é pequeno o suficiente, retornar como um único chunk
            if (codigo.Length <= tamanhoMaximoChunk)
            {
                chunks.Add(codigo);
                return chunks;
            }
            
            // Dividir o código em linhas
            string[] linhas = codigo.Split('\n');
            
            StringBuilder chunkAtual = new StringBuilder();
            int contadorChars = 0;
            
            foreach (var linha in linhas)
            {
                int tamanhoLinha = linha.Length + 1; // +1 para o \n
                
                // Se adicionar esta linha excederia o tamanho máximo e já temos algum conteúdo,
                // adicionar o chunk atual à lista e começar um novo
                if (contadorChars + tamanhoLinha > tamanhoMaximoChunk && chunkAtual.Length > 0)
                {
                    chunks.Add(chunkAtual.ToString());
                    chunkAtual.Clear();
                    contadorChars = 0;
                }
                
                // Adicionar a linha ao chunk atual
                chunkAtual.AppendLine(linha);
                contadorChars += tamanhoLinha;
            }
            
            // Adicionar o último chunk se tiver conteúdo
            if (chunkAtual.Length > 0)
            {
                chunks.Add(chunkAtual.ToString());
            }
            
            return chunks;
        }
        
        /// <summary>
        /// Combina os resultados da análise de múltiplos chunks
        /// </summary>
        private CodigoLimpo CombinarResultadosChunks(List<CodigoLimpo> resultados)
        {
            // Se houver apenas um resultado, retorná-lo diretamente
            if (resultados.Count == 1)
                return resultados[0];
            
            // Calcular médias para cada métrica
            double somaVariaveis = 0;
            double somaFuncoes = 0;
            double somaComentarios = 0;
            double somaCoesao = 0;
            double somaCodigoMorto = 0;
            
            // Coletar justificativas de todos os chunks
            var todasJustificativas = new Dictionary<string, List<string>>
            {
                { "NomenclaturaVariaveis", new List<string>() },
                { "TamanhoFuncoes", new List<string>() },
                { "UsoComentariosRelevantes", new List<string>() },
                { "CoesaoMetodos", new List<string>() },
                { "EvitacaoCodigoMorto", new List<string>() }
            };
            
            foreach (var resultado in resultados)
            {
                somaVariaveis += resultado.NomenclaturaVariaveis;
                somaFuncoes += resultado.TamanhoFuncoes;
                somaComentarios += resultado.UsoComentariosRelevantes;
                somaCoesao += resultado.CoesaoMetodos;
                somaCodigoMorto += resultado.EvitacaoCodigoMorto;
                
                // Coletar justificativas (se disponíveis)
                if (resultado.Justificativas != null)
                {
                    if (resultado.Justificativas.TryGetValue("NomenclaturaVariaveis", out var justificativa))
                        todasJustificativas["NomenclaturaVariaveis"].Add(justificativa);
                    
                    if (resultado.Justificativas.TryGetValue("TamanhoFuncoes", out justificativa))
                        todasJustificativas["TamanhoFuncoes"].Add(justificativa);
                    
                    if (resultado.Justificativas.TryGetValue("UsoComentariosRelevantes", out justificativa))
                        todasJustificativas["UsoComentariosRelevantes"].Add(justificativa);
                    
                    if (resultado.Justificativas.TryGetValue("CoesaoMetodos", out justificativa))
                        todasJustificativas["CoesaoMetodos"].Add(justificativa);
                    
                    if (resultado.Justificativas.TryGetValue("EvitacaoCodigoMorto", out justificativa))
                        todasJustificativas["EvitacaoCodigoMorto"].Add(justificativa);
                }
            }
            
            // Calcular médias
            int count = resultados.Count;
            double notaVariaveis = Math.Round(somaVariaveis / count, 1);
            double notaFuncoes = Math.Round(somaFuncoes / count, 1);
            double notaComentarios = Math.Round(somaComentarios / count, 1);
            double notaCoesao = Math.Round(somaCoesao / count, 1);
            double notaCodigoMorto = Math.Round(somaCodigoMorto / count, 1);
            
            // Consolidar justificativas (pegar a primeira não vazia ou combinar até 2)
            var justificativasFinal = new Dictionary<string, string>();
            
            foreach (var categoria in todasJustificativas.Keys)
            {
                var justificativasCategoria = todasJustificativas[categoria]
                    .Where(j => !string.IsNullOrWhiteSpace(j))
                    .Take(2)
                    .ToList();
                
                if (justificativasCategoria.Count > 0)
                {
                    justificativasFinal[categoria] = string.Join(" ", justificativasCategoria);
                }
                else
                {
                    justificativasFinal[categoria] = "Análise baseada em múltiplos chunks do código.";
                }
            }
            
            // Incluir informação sobre o processo de chunking
            justificativasFinal["Observacao"] = $"Esta análise foi combinada a partir de {count} chunks do código original.";
            
            // Criar o objeto final
            return new CodigoLimpo
            {
                NomenclaturaVariaveis = (int)notaVariaveis,
                TamanhoFuncoes = (int)notaFuncoes,
                UsoComentariosRelevantes = (int)notaComentarios,
                CoesaoMetodos = (int)notaCoesao,
                EvitacaoCodigoMorto = (int)notaCodigoMorto,
                Justificativas = justificativasFinal
            };
        }
        
        /// <inheritdoc/>
        public async Task<List<Recomendacao>> GerarRecomendacoesAsync(CodigoLimpo analise, 
            string codigo, string linguagem)
        {
            try
            {
                _logger.LogInformation("🔍 Iniciando geração de recomendações para código na linguagem {Linguagem}", linguagem);
                
                // Verificar se o código é muito grande para processar de uma vez
                const int TAMANHO_MAXIMO_CHUNK = 7000;
                if (codigo.Length > TAMANHO_MAXIMO_CHUNK)
                {
                    _logger.LogWarning("⚠️ Código muito grande ({TamanhoTotal} caracteres) para recomendações. Dividindo em chunks para evitar timeout.", 
                        codigo.Length);
                    return await GerarRecomendacoesEmChunksAsync(analise, codigo, linguagem);
                }
                
                // Criar um JSON com a análise para incluir no prompt
                string analiseJson = JsonSerializer.Serialize(analise);
                
                // Construir o prompt usando o template de recomendações
                string prompt = _promptTemplates.Recomendacoes
                    .Replace("{{ANALISE}}", analiseJson)
                    .Replace("{{CODIGO}}", codigo)
                    .Replace("{{LINGUAGEM}}", linguagem);
                
                _logger.LogInformation("🤖 Preparando geração de recomendações para arquivo de {TamanhoArquivo} caracteres", 
                    codigo.Length);
                
                // Enviar para o LLM
                string resposta = await ProcessarPromptAsync(prompt, _options.ModeloRecomendacoes);
                
                // Processar a resposta para extrair as recomendações
                _logger.LogInformation("🔄 Processando resposta para extrair recomendações");
                var recomendacoes = await ProcessarRespostaRecomendacoes(resposta);
                
                _logger.LogInformation("✅ Geração de recomendações concluída. Total de recomendações: {Total}", 
                    recomendacoes.Count);
                
                return recomendacoes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao gerar recomendações no Ollama");
                throw;
            }
        }
        
        /// <summary>
        /// Gera recomendações para código grande dividindo-o em chunks
        /// </summary>
        private async Task<List<Recomendacao>> GerarRecomendacoesEmChunksAsync(
            CodigoLimpo analiseCompleta, 
            string codigo, 
            string linguagem)
        {
            try
            {
                _logger.LogInformation("🔄 Iniciando geração de recomendações em chunks");
                
                // Dividir o código em chunks
                var chunks = DividirCodigoEmChunks(codigo, 7000);
                _logger.LogInformation("📊 Código dividido em {NumeroChunks} chunks para recomendações", chunks.Count);
                
                // Gerar recomendações para cada chunk separadamente
                var todasRecomendacoes = new List<Recomendacao>();
                int chunkAtual = 0;
                
                foreach (var chunk in chunks)
                {
                    chunkAtual++;
                    _logger.LogInformation("🔍 Gerando recomendações para chunk {ChunkAtual}/{TotalChunks} (tamanho: {TamanhoChunk} caracteres)",
                        chunkAtual, chunks.Count, chunk.Length);
                    
                    // Adaptar a análise para este chunk
                    var analiseChunk = new CodigoLimpo
                    {
                        NomenclaturaVariaveis = analiseCompleta.NomenclaturaVariaveis,
                        TamanhoFuncoes = analiseCompleta.TamanhoFuncoes,
                        UsoComentariosRelevantes = analiseCompleta.UsoComentariosRelevantes,
                        CoesaoMetodos = analiseCompleta.CoesaoMetodos,
                        EvitacaoCodigoMorto = analiseCompleta.EvitacaoCodigoMorto,
                        Justificativas = analiseCompleta.Justificativas
                    };
                    
                    // Adicionar informação sobre chunking à análise
                    if (analiseChunk.Justificativas == null)
                        analiseChunk.Justificativas = new Dictionary<string, string>();
                    
                    analiseChunk.Justificativas["Chunking"] = $"Este é o chunk {chunkAtual} de {chunks.Count} do arquivo completo.";
                    
                    // Gerar recomendações para o chunk
                    try
                    {
                        // Serializar a análise adaptada
                        string analiseJson = JsonSerializer.Serialize(analiseChunk);
                        
                        // Construir o prompt para o chunk
                        string prompt = _promptTemplates.Recomendacoes
                            .Replace("{{ANALISE}}", analiseJson)
                            .Replace("{{CODIGO}}", chunk)
                            .Replace("{{LINGUAGEM}}", linguagem);
                        
                        prompt += $"\n\nATENÇÃO: Este é o chunk {chunkAtual} de {chunks.Count} do arquivo completo. Gere recomendações específicas para este trecho de código.";
                        
                        // Enviar para o LLM com timeout reduzido para chunks
                        string resposta = await ProcessarPromptAsync(prompt, _options.ModeloRecomendacoes, 0.1f, 
                            Math.Min(2048, _options.MaxTokens));
                        
                        // Processar a resposta
                        var recomendacoesChunk = await ProcessarRespostaRecomendacoes(resposta);
                        
                        if (recomendacoesChunk != null && recomendacoesChunk.Count > 0)
                        {
                            // Adicionar informação sobre o chunk em cada recomendação
                            foreach (var recomendacao in recomendacoesChunk)
                            {
                                recomendacao.Titulo = $"[Chunk {chunkAtual}] {recomendacao.Titulo}";
                                
                                // Evitar duplicação de recomendações muito similares
                                if (!ExisteRecomendacaoSimilar(todasRecomendacoes, recomendacao))
                                {
                                    todasRecomendacoes.Add(recomendacao);
                                }
                            }
                            
                            _logger.LogInformation("✅ Geradas {NumRecomendacoes} recomendações para o chunk {ChunkAtual}", 
                                recomendacoesChunk.Count, chunkAtual);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Erro ao gerar recomendações para chunk {ChunkAtual}", chunkAtual);
                        // Continuar para o próximo chunk mesmo em caso de erro
                    }
                    
                    // Limitar o número total de recomendações (máximo 10)
                    if (todasRecomendacoes.Count >= 10)
                    {
                        _logger.LogInformation("🛑 Limite de 10 recomendações atingido. Parando processamento de chunks.");
                        break;
                    }
                }
                
                // Se não conseguimos gerar nenhuma recomendação, tentar uma abordagem mais genérica
                if (todasRecomendacoes.Count == 0)
                {
                    _logger.LogWarning("⚠️ Não foi possível gerar recomendações para nenhum chunk. Tentando uma abordagem mais genérica.");
                    
                    // Tentar gerar recomendações com base apenas na análise geral
                    try
                    {
                        string analiseJson = JsonSerializer.Serialize(analiseCompleta);
                        string promptGenerico = _promptTemplates.RecomendacoesGenericas
                            .Replace("{{ANALISE}}", analiseJson)
                            .Replace("{{LINGUAGEM}}", linguagem);
                        
                        string resposta = await ProcessarPromptAsync(promptGenerico, _options.ModeloRecomendacoes);
                        var recomendacoesGenericas = await ProcessarRespostaRecomendacoes(resposta);
                        
                        if (recomendacoesGenericas != null && recomendacoesGenericas.Count > 0)
                        {
                            todasRecomendacoes.AddRange(recomendacoesGenericas);
                            _logger.LogInformation("✅ Geradas {NumRecomendacoes} recomendações genéricas", 
                                recomendacoesGenericas.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Erro ao gerar recomendações genéricas");
                    }
                }
                
                // Ordenar as recomendações por prioridade
                var recomendacoesOrdenadas = todasRecomendacoes
                    .OrderByDescending(r => r.Prioridade == "Alta" ? 3 : r.Prioridade == "Média" ? 2 : 1)
                    .ToList();
                
                _logger.LogInformation("✅ Geração de recomendações em chunks concluída. Total: {Total} recomendações", 
                    recomendacoesOrdenadas.Count);
                
                return recomendacoesOrdenadas;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao gerar recomendações em chunks");
                throw;
            }
        }
        
        /// <summary>
        /// Verifica se já existe uma recomendação similar na lista
        /// </summary>
        private bool ExisteRecomendacaoSimilar(List<Recomendacao> recomendacoes, Recomendacao novaRecomendacao)
        {
            // Simplificar o título para comparação (remover prefixo de chunk)
            string tituloSimplificado = novaRecomendacao.Titulo;
            if (tituloSimplificado.Contains("]"))
            {
                tituloSimplificado = tituloSimplificado.Substring(tituloSimplificado.IndexOf("]") + 1).Trim();
            }
            
            // Verificar se já existe recomendação com título similar
            foreach (var recomendacao in recomendacoes)
            {
                string tituloExistente = recomendacao.Titulo;
                if (tituloExistente.Contains("]"))
                {
                    tituloExistente = tituloExistente.Substring(tituloExistente.IndexOf("]") + 1).Trim();
                }
                
                // Comparar títulos simplificados e tipos
                if ((tituloExistente.Contains(tituloSimplificado) || 
                     tituloSimplificado.Contains(tituloExistente)) &&
                    recomendacao.Tipo == novaRecomendacao.Tipo)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <inheritdoc/>
        public async Task<List<string>> ObterModelosDisponiveisAsync()
        {
            try
            {
                _logger.LogInformation("🔍 Buscando modelos disponíveis no Ollama");
                
                var response = await _httpClient.GetFromJsonAsync<OllamaModelosResponse>("/api/tags");
                
                if (response?.Models == null)
                {
                    _logger.LogWarning("⚠️ Nenhum modelo encontrado no Ollama");
                    return new List<string>();
                }
                
                var modelos = new List<string>();
                foreach (var modelo in response.Models)
                {
                    modelos.Add(modelo.Name);
                }
                
                _logger.LogInformation("📋 Encontrados {Total} modelos no Ollama: {ModelosList}", 
                    modelos.Count, string.Join(", ", modelos));
                
                return modelos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao obter modelos disponíveis no Ollama");
                return new List<string>();
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> VerificarModeloDisponivelAsync(string nomeModelo)
        {
            try
            {
                _logger.LogInformation("🔍 Verificando disponibilidade do modelo {NomeModelo}", nomeModelo);
                
                var modelos = await ObterModelosDisponiveisAsync();
                bool disponivel = modelos.Contains(nomeModelo);
                
                if (disponivel)
                {
                    _logger.LogInformation("✅ Modelo {NomeModelo} está disponível", nomeModelo);
                }
                else
                {
                    _logger.LogWarning("⚠️ Modelo {NomeModelo} não está disponível. Modelos disponíveis: {ModelosDisponiveis}", 
                        nomeModelo, string.Join(", ", modelos));
                }
                
                return disponivel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao verificar disponibilidade do modelo {NomeModelo}", nomeModelo);
                return false;
            }
        }
        
        #region Métodos Auxiliares
        
        /// <summary>
        /// Processa a resposta da análise de código e retorna um objeto CodigoLimpo
        /// </summary>
        private async Task<CodigoLimpo> ProcessarRespostaAnalise(string resposta)
        {
            const int MAX_TENTATIVAS = 3;
            int tentativas = 0;
            
            string jsonContent = string.Empty;
            // Tentar extrair JSON da resposta
            int startIndex = resposta.IndexOf('{');
            int endIndex = resposta.LastIndexOf('}');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                jsonContent = resposta.Substring(startIndex, endIndex - startIndex + 1);
                // Limpar tokens especiais e formatação markdown
                jsonContent = LimparFormatacaoETokens(jsonContent);
            }
            else
            {
                _logger.LogWarning("⚠️ Não foi possível encontrar objeto JSON na resposta de análise. Usando valores padrão.");
                return CriarCodigoLimpoPadrao("Não foi possível extrair JSON da resposta");
            }
            
            while (tentativas < MAX_TENTATIVAS)
            {
                tentativas++;
                try
                {
                    _logger.LogInformation("🔄 Tentativa {Tentativa}/{MaxTentativas} de deserializar análise (tamanho: {TamanhoJson} caracteres)", 
                        tentativas, MAX_TENTATIVAS, jsonContent.Length);
                    
                    // Deserializar o JSON para um objeto CodigoLimpo
                    var analise = JsonSerializer.Deserialize<CodigoLimpo>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (analise != null)
                    {
                        return analise;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Deserialização resultou em objeto nulo");
                        if (tentativas >= MAX_TENTATIVAS)
                            return CriarCodigoLimpoPadrao("Deserialização resultou em objeto nulo");
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning("⚠️ Erro ao deserializar JSON de análise (tentativa {Tentativa}/{MaxTentativas}): {Erro}", 
                        tentativas, MAX_TENTATIVAS, jsonEx.Message);
                    
                    if (tentativas < MAX_TENTATIVAS)
                    {
                        // Em vez de tentar corrigir localmente, pedir ao LLM para corrigir o JSON
                        _logger.LogInformation("🤖 Solicitando ao LLM para corrigir o JSON com problemas");
                        jsonContent = await SolicitarCorrecaoJsonAnaliseAoLLM(jsonContent, jsonEx.Message);
                    }
                    else
                    {
                        _logger.LogError(jsonEx, "❌ Todas as tentativas de correção de JSON falharam");
                        return CriarCodigoLimpoPadrao($"Erro ao deserializar: {jsonEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao processar resposta de análise (tentativa {Tentativa}/{MaxTentativas})", 
                        tentativas, MAX_TENTATIVAS);
                    
                    return CriarCodigoLimpoPadrao($"Erro: {ex.Message}");
                }
            }
            
            // Não deveria chegar aqui, mas por segurança
            return CriarCodigoLimpoPadrao("Número máximo de tentativas excedido");
        }
        
        /// <summary>
        /// Cria um objeto CodigoLimpo com valores padrão
        /// </summary>
        private CodigoLimpo CriarCodigoLimpoPadrao(string mensagemErro)
        {
            return new CodigoLimpo
            {
                NomenclaturaVariaveis = 5,
                TamanhoFuncoes = 5,
                UsoComentariosRelevantes = 5,
                CoesaoMetodos = 5,
                EvitacaoCodigoMorto = 5,
                Justificativas = new Dictionary<string, string>
                {
                    { "NomenclaturaVariaveis", "Não foi possível analisar" },
                    { "TamanhoFuncoes", "Não foi possível analisar" },
                    { "UsoComentariosRelevantes", "Não foi possível analisar" },
                    { "CoesaoMetodos", "Não foi possível analisar" },
                    { "EvitacaoCodigoMorto", "Não foi possível analisar" },
                    { "Error", mensagemErro }
                }
            };
        }
        
        /// <summary>
        /// Solicita ao LLM para corrigir um JSON com problemas (para análise)
        /// </summary>
        private async Task<string> SolicitarCorrecaoJsonAnaliseAoLLM(string jsonProblematico, string mensagemErro)
        {
            try
            {
                // Construir o prompt para correção de JSON
                string promptCorrecao = $@"
Você é um especialista em formatação e correção de JSON. Por favor, corrija o seguinte JSON que está com problemas de formatação.
O erro reportado é: {mensagemErro}

JSON com problemas:
```
{jsonProblematico}
```

INSTRUÇÕES CRÍTICAS:
1. Retorne APENAS o JSON corrigido, sem nenhum texto antes ou depois.
2. NÃO use caracteres especiais Unicode ou caracteres de controle.
3. NÃO inclua tags como &lt;begin_of_sentence&gt;, &lt;|begin_of_sentence|&gt;, ou qualquer outra marcação.
4. NÃO use formatação markdown, comentários, explicações ou qualquer texto que não seja parte do JSON.
5. NÃO inclua backticks (```) ou outros delimitadores de código.
6. Use APENAS caracteres ASCII padrão no JSON.
7. Todos os campos de texto devem estar entre aspas duplas (""texto"").
8. Certifique-se de que o JSON é um objeto válido contendo os campos da análise de código.
9. Mantenha ao máximo o conteúdo original, corrigindo apenas a estrutura/sintaxe.
10. Não inclua caracteres de quebra de linha dentro de strings, use espaços no lugar.

ERROS COMUNS A EVITAR:
- NÃO inclua caracteres especiais, tokens ou marcações
- NÃO use aspas simples no lugar de aspas duplas
- NÃO deixe propriedades sem aspas
- NÃO use vírgulas depois do último elemento de arrays ou objetos
- NÃO use barras invertidas desnecessárias
- NÃO use ':' em vez de '"":'

Retorne o JSON puro e corrigido:";

                // Usar temperatura mais baixa para correções mais precisas
                string respostaCorrecao = await ProcessarPromptAsync(promptCorrecao, _options.ModeloAnalise, 0.1f);
                
                // Extrair apenas o JSON da resposta
                int startIndex = respostaCorrecao.IndexOf('{');
                int endIndex = respostaCorrecao.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string jsonCorrigido = respostaCorrecao.Substring(startIndex, endIndex - startIndex + 1);
                    
                    // Limpar tokens especiais que possam ter permanecido
                    jsonCorrigido = LimparFormatacaoETokens(jsonCorrigido);
                    
                    // Verificar estrutura básica do JSON
                    if (jsonCorrigido.StartsWith("{") && jsonCorrigido.EndsWith("}") && 
                        (jsonCorrigido.Contains("\"nomenclaturaVariaveis\"") || jsonCorrigido.Contains("\"justificativas\"")))
                    {
                        _logger.LogInformation("✅ JSON de análise corrigido pelo LLM (tamanho: {TamanhoJson} caracteres)", jsonCorrigido.Length);
                        return jsonCorrigido;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ JSON de análise corrigido não tem a estrutura esperada");
                    }
                }
                
                _logger.LogWarning("⚠️ LLM não conseguiu corrigir o JSON de análise adequadamente");
                return jsonProblematico; // Retornar o original se não conseguir extrair
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao solicitar correção de JSON de análise ao LLM");
                return jsonProblematico; // Retornar o original em caso de erro
            }
        }
        
        /// <summary>
        /// Remove tokens especiais e formatação markdown que podem interferir na desserialização do JSON
        /// </summary>
        private string LimparFormatacaoETokens(string json)
        {
            // Lista de tokens especiais conhecidos para remover
            string[] tokensParaRemover = new[]
            {
                "<|begin_of_sentence|>",
                "<|end_of_sentence|>",
                "<|endoftext|>",
                // Tokens especiais que aparecem em outros formatos (com caracteres unicode)
                "<\uff5cbegin\u2581of\u2581sentence\uff5c>",
                "<\uff5cend\u2581of\u2581sentence\uff5c>",
                "<\uff5cendoftext\uff5c>",
                // Formatação markdown
                "```json",
                "```",
                "`"
            };
            
            string resultado = json;
            foreach (var token in tokensParaRemover)
            {
                resultado = resultado.Replace(token, "");
            }
            
            // Normalizar espaços duplicados que podem ter sido criados
            resultado = Regex.Replace(resultado, @"\s+", " ");
            // Remover espaços no início e fim
            resultado = resultado.Trim();
            
            return resultado;
        }
        
        /// <summary>
        /// Processa a resposta de recomendações e retorna uma lista de objetos Recomendacao
        /// </summary>
        private async Task<List<Recomendacao>> ProcessarRespostaRecomendacoes(string resposta)
        {
            const int MAX_TENTATIVAS = 3;
            int tentativas = 0;
            
            string jsonContent = string.Empty;
            // Tentar extrair JSON da resposta
            int startIndex = resposta.IndexOf('[');
            int endIndex = resposta.LastIndexOf(']');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                jsonContent = resposta.Substring(startIndex, endIndex - startIndex + 1);
                // Limpar tokens especiais que podem estar interferindo na desserialização
                jsonContent = LimparFormatacaoETokens(jsonContent);
            }
            else
            {
                _logger.LogWarning("⚠️ Não foi possível encontrar array JSON na resposta de recomendações");
                return new List<Recomendacao>();
            }
            
            while (tentativas < MAX_TENTATIVAS)
            {
                tentativas++;
                try
                {
                    _logger.LogInformation("🔄 Tentativa {Tentativa}/{MaxTentativas} de deserializar recomendações (tamanho: {TamanhoJson} caracteres)", 
                        tentativas, MAX_TENTATIVAS, jsonContent.Length);
                    
                    // Deserializar o JSON para uma lista de Recomendacao
                    var recomendacoes = JsonSerializer.Deserialize<List<Recomendacao>>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Recomendacao>();
                    
                    return recomendacoes;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning("⚠️ Erro ao deserializar JSON de recomendações (tentativa {Tentativa}/{MaxTentativas}): {Erro}", 
                        tentativas, MAX_TENTATIVAS, jsonEx.Message);
                    
                    if (tentativas < MAX_TENTATIVAS)
                    {
                        // Em vez de tentar corrigir localmente, pedir ao LLM para corrigir o JSON
                        _logger.LogInformation("🤖 Solicitando ao LLM para corrigir o JSON com problemas");
                        jsonContent = await SolicitarCorrecaoJsonAoLLM(jsonContent, jsonEx.Message);
                    }
                    else
                    {
                        _logger.LogError(jsonEx, "❌ Todas as tentativas de correção de JSON falharam");
                        
                        // Retornar uma lista com uma recomendação de erro
                        return new List<Recomendacao>
                        {
                            new Recomendacao
                            {
                                Titulo = "Erro ao processar recomendações",
                                Descricao = $"Ocorreu um erro ao processar as recomendações: {jsonEx.Message}",
                                Prioridade = "Alta",
                                Tipo = "Erro",
                                Dificuldade = "N/A",
                                ReferenciaArquivo = "N/A",
                                RecursosEstudo = new List<string> { "N/A" }
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao processar resposta de recomendações (tentativa {Tentativa}/{MaxTentativas})", 
                        tentativas, MAX_TENTATIVAS);
                    
                    // Retornar uma lista com uma recomendação de erro
                    return new List<Recomendacao>
                    {
                        new Recomendacao
                        {
                            Titulo = "Erro ao processar recomendações",
                            Descricao = $"Ocorreu um erro ao processar as recomendações: {ex.Message}",
                            Prioridade = "Alta",
                            Tipo = "Erro",
                            Dificuldade = "N/A",
                            ReferenciaArquivo = "N/A",
                            RecursosEstudo = new List<string> { "N/A" }
                        }
                    };
                }
            }
            
            // Não deveria chegar aqui, mas por segurança
            return new List<Recomendacao>();
        }
        
        /// <summary>
        /// Solicita ao LLM para corrigir um JSON com problemas
        /// </summary>
        private async Task<string> SolicitarCorrecaoJsonAoLLM(string jsonProblematico, string mensagemErro)
        {
            try
            {
                // Construir o prompt para correção de JSON
                string promptCorrecao = $@"
Você é um especialista em formatação e correção de JSON. Por favor, corrija o seguinte JSON que está com problemas de formatação.
O erro reportado é: {mensagemErro}

JSON com problemas:
```
{jsonProblematico}
```

INSTRUÇÕES CRÍTICAS:
1. Retorne APENAS o JSON corrigido, sem nenhum texto antes ou depois.
2. NÃO use caracteres especiais Unicode ou caracteres de controle.
3. NÃO inclua tags como &lt;begin_of_sentence&gt;, &lt;|begin_of_sentence|&gt;, ou qualquer outra marcação.
4. NÃO use formatação markdown, comentários, explicações ou qualquer texto que não seja parte do JSON.
5. Use APENAS caracteres ASCII padrão no JSON.
6. Todos os campos de texto devem estar entre aspas duplas (""texto"").
7. Certifique-se de que o JSON é um array válido contendo objetos 'Recomendacao'.
8. Cada objeto deve ter TODAS as seguintes propriedades com os tipos corretos:
   - ""titulo"": string
   - ""descricao"": string
   - ""exemplo"": string
   - ""prioridade"": string
   - ""tipo"": string
   - ""dificuldade"": string
   - ""referenciaArquivo"": string
   - ""recursosEstudo"": array de strings
9. Mantenha ao máximo o conteúdo original, corrigindo apenas a estrutura/sintaxe.
10. Não inclua caracteres de quebra de linha dentro de strings, use espaços no lugar.

ERROS COMUNS A EVITAR:
- NÃO inclua caracteres especiais, tokens ou marcações como &lt;|begin_of_sentence|&gt;
- NÃO use aspas simples no lugar de aspas duplas
- NÃO deixe propriedades sem aspas
- NÃO use vírgulas depois do último elemento de arrays ou objetos
- NÃO use barras invertidas desnecessárias
- NÃO use ':' em vez de '"":'

Retorne o JSON puro e corrigido:";

                // Usar temperatura mais baixa para correções mais precisas
                string respostaCorrecao = await ProcessarPromptAsync(promptCorrecao, _options.ModeloRecomendacoes, 0.1f);
                
                // Extrair apenas o JSON da resposta
                int startIndex = respostaCorrecao.IndexOf('[');
                int endIndex = respostaCorrecao.LastIndexOf(']');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string jsonCorrigido = respostaCorrecao.Substring(startIndex, endIndex - startIndex + 1);
                    
                    // Limpar tokens especiais que possam ter permanecido
                    jsonCorrigido = LimparFormatacaoETokens(jsonCorrigido);
                    
                    // Verificar estrutura básica do JSON
                    if (jsonCorrigido.StartsWith("[") && jsonCorrigido.EndsWith("]") && jsonCorrigido.Contains("\"titulo\""))
                    {
                        _logger.LogInformation("✅ JSON corrigido pelo LLM (tamanho: {TamanhoJson} caracteres)", jsonCorrigido.Length);
                        return jsonCorrigido;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ JSON corrigido não tem a estrutura esperada");
                    }
                }
                
                _logger.LogWarning("⚠️ LLM não conseguiu corrigir o JSON adequadamente");
                return jsonProblematico; // Retornar o original se não conseguir extrair
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao solicitar correção de JSON ao LLM");
                return jsonProblematico; // Retornar o original em caso de erro
            }
        }
        
        #endregion
    }
    
    #region Classes de Apoio
    
    /// <summary>
    /// Opções de configuração para o serviço Ollama
    /// </summary>
    public class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string ModeloPadrao { get; set; } = "deepseek-coder:6.7b-instruct-q4_0";
        public string ModeloAnalise { get; set; } = "deepseek-coder:6.7b-instruct-q4_0";
        public string ModeloRecomendacoes { get; set; } = "deepseek-coder:6.7b-instruct-q4_0";
        public int TimeoutSegundos { get; set; } = 600;
        public int MaxTokens { get; set; } = 2048;
    }
    
    /// <summary>
    /// Templates de prompts para diferentes tarefas
    /// </summary>
    public class PromptTemplates
    {
        /// <summary>
        /// Template para análise de código
        /// </summary>
        public string AnaliseCodigo { get; set; } = @"
Você é um especialista em análise de código e boas práticas de programação. Analise o código abaixo e avalie-o segundo os princípios de código limpo e boas práticas.

Linguagem: {{LINGUAGEM}}

Contexto adicional: {{CONTEXTO}}

Código a ser analisado:
```
{{CODIGO}}
```

Avalie cada um dos seguintes critérios de 0 a 10 (onde 0 é péssimo e 10 é excelente):
1. Nomenclatura de variáveis, funções e classes
2. Tamanho e responsabilidade única de funções
3. Uso adequado de comentários e documentação
4. Coesão de métodos e classes
5. Ausência de código morto ou redundante

Para cada critério, forneça uma breve justificativa.

IMPORTANTE: Responda APENAS em formato JSON, sem texto adicional antes ou depois. Forneça apenas o objeto JSON puro, sem formatação markdown ou explicações.

Formato único e esperado (não fuja desse formato do JSON):

{
  ""nomenclaturaVariaveis"": numero,
  ""tamanhoFuncoes"": numero,
  ""usoComentariosRelevantes"": numero,
  ""coesaoMetodos"": numero,
  ""evitacaoCodigoMorto"": numero,
  ""justificativas"": {
    ""nomenclaturaVariaveis"": ""Descrição do critério de nomenclatura"",
    ""tamanhoFuncoes"": ""Descrição do critério de tamanho"",
    ""usoComentariosRelevantes"": ""Descrição do critério de comentários"",
    ""coesaoMetodos"": ""Descrição do critério de coesão"",
    ""evitacaoCodigoMorto"": ""Descrição do critério de código morto""
  }
}";
        
        /// <summary>
        /// Template para geração de recomendações
        /// </summary>
        public string Recomendacoes { get; set; } = @"
Você é um tutor de programação experiente. Com base na análise de código abaixo, gere recomendações educativas para ajudar o desenvolvedor a melhorar seu código e aprender melhores práticas.

Linguagem: {{LINGUAGEM}}

Análise do código:
{{ANALISE}}

Código analisado:
```
{{CODIGO}}
```

Forneça 3-5 recomendações específicas, priorizando os aspectos que mais precisam de melhoria conforme indicado na análise. Cada recomendação deve:
1. Focar em um problema ou oportunidade de melhoria específica
2. Explicar o impacto positivo da mudança
3. Incluir um exemplo concreto de como implementar a melhoria
4. Fornecer links ou recursos para aprendizado adicional

IMPORTANTE: Responda APENAS em formato JSON, sem texto adicional antes ou depois. Forneça apenas o array JSON puro, sem formatação markdown ou explicações.

Formato único e esperado (não fuja desse formato do JSON):
[
  {
    ""titulo"": ""Melhore a nomenclatura de variáveis"",
    ""descricao"": ""Variáveis como 'x' e 'temp' não comunicam seu propósito. Nomes descritivos melhoram a legibilidade e manutenção do código."",
    ""exemplo"": ""Em vez de 'int x = calcularTotal();', use 'int totalProdutos = calcularTotalProdutos();'"",
    ""prioridade"": ""Alta"",
    ""tipo"": ""Nomenclatura"",
    ""dificuldade"": ""Fácil"",
    ""referenciaArquivo"": ""linha 25-30"",
    ""recursosEstudo"": [""https://cleancoders.com/resources/naming-variables"", ""https://refactoring.guru/renaming""]
  }
]";

        /// <summary>
        /// Template para geração de recomendações genéricas (sem código específico)
        /// </summary>
        public string RecomendacoesGenericas { get; set; } = @"
Você é um tutor de programação experiente. Com base na análise de código abaixo, gere recomendações educativas genéricas para ajudar o desenvolvedor a melhorar seu código e aprender melhores práticas.

Linguagem: {{LINGUAGEM}}

Análise do código:
{{ANALISE}}

Forneça 3-5 recomendações genéricas, priorizando os aspectos que mais precisam de melhoria conforme indicado na análise. Como você não tem acesso ao código específico, foque em princípios gerais e boas práticas para a linguagem mencionada.

Cada recomendação deve:
1. Focar em um problema ou oportunidade de melhoria que provavelmente existe no código
2. Explicar o impacto positivo da mudança
3. Incluir um exemplo genérico de como implementar a melhoria
4. Fornecer links ou recursos para aprendizado adicional

IMPORTANTE: Responda APENAS em formato JSON, sem texto adicional antes ou depois. Forneça apenas o array JSON puro, sem formatação markdown ou explicações.

Formato único e esperado (não fuja desse formato do JSON):
[
  {
    ""titulo"": ""Melhore a nomenclatura de variáveis"",
    ""descricao"": ""Variáveis com nomes pouco descritivos não comunicam seu propósito. Nomes descritivos melhoram a legibilidade e manutenção do código."",
    ""exemplo"": ""Em vez de 'int x = calcularTotal();', use 'int totalProdutos = calcularTotalProdutos();'"",
    ""prioridade"": ""Alta"",
    ""tipo"": ""Nomenclatura"",
    ""dificuldade"": ""Fácil"",
    ""referenciaArquivo"": ""Diversos arquivos"",
    ""recursosEstudo"": [""https://cleancoders.com/resources/naming-variables"", ""https://refactoring.guru/renaming""]
  }
]";
    }
    
    /// <summary>
    /// Modelo de requisição para o Ollama API
    /// </summary>
    public class OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }
        
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }
        
        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }
        
        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
        
        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }
    
    /// <summary>
    /// Modelo de resposta do Ollama API
    /// </summary>
    public class OllamaResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }
        
        [JsonPropertyName("response")]
        public string Response { get; set; }
        
        [JsonPropertyName("total_duration")]
        public long TotalDuration { get; set; }
        
        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }
        
        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }
    
    /// <summary>
    /// Modelo para a resposta da listagem de modelos disponíveis
    /// </summary>
    public class OllamaModelosResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo> Models { get; set; }
    }
    
    /// <summary>
    /// Informações sobre um modelo no Ollama
    /// </summary>
    public class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("modified_at")]
        public string ModifiedAt { get; set; }
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
    
    #endregion
} 