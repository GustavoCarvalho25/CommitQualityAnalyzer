using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço responsável por construir prompts para análise de código
    /// </summary>
    public class PromptBuilderService
    {
        private readonly ILogger<PromptBuilderService> _logger;

        public PromptBuilderService(ILogger<PromptBuilderService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Constrói o prompt para análise de diferenças de código
        /// </summary>
        public string BuildDiffAnalysisPrompt(string filePath, string diffText, CommitInfo commit)
        {
            _logger.LogInformation("Construindo prompt Clean Code para {FilePath}", filePath);

            var sb = new StringBuilder();

            // Contexto do commit
            sb.AppendLine($"Arquivo: {filePath}  (commit {commit.Sha.Substring(0, 8)})");
            sb.AppendLine();

            // Diferenças de código
            sb.AppendLine("### Diferenças de código");
            sb.AppendLine("```diff");
            sb.AppendLine(diffText);
            sb.AppendLine("```");
            sb.AppendLine();

            // Instruções
            sb.AppendLine("Você é um especialista em Clean Code em C#. Avalie o trecho acima APENAS sob a ótica de Clean Code.");
            sb.AppendLine("Dê uma nota inteira de 0 a 10 e um breve comentário para os aspectos listados.");
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
            sb.AppendLine("  \"SugestaoRefatoracao\": \"...\"");
            sb.AppendLine("}");
            sb.AppendLine("```");

            sb.AppendLine();
            sb.AppendLine("Cada *nota* deve ser um número inteiro.");

            return sb.ToString();
        }

        /// <summary>
        /// Constrói o prompt para análise de trechos de código
        /// </summary>
        public string BuildCodeAnalysisPrompt(string filePath, string codeContent, CommitInfo commit, int partIndex = 0)
        {
            _logger.LogInformation("Construindo prompt para análise de código para {FilePath} (parte {PartIndex})", filePath, partIndex);

            var sb = new StringBuilder();

            // Contexto do commit e parte
            sb.AppendLine($"Arquivo: {filePath}  (commit {commit.Sha.Substring(0, 8)}, parte {partIndex})");
            sb.AppendLine();

            // Código para análise
            sb.AppendLine("### Código para análise");
            sb.AppendLine("```csharp");
            sb.AppendLine(codeContent);
            sb.AppendLine("```");
            sb.AppendLine();

            // Instruções
            sb.AppendLine("Você é um especialista em Clean Code em C#. Avalie o trecho acima APENAS sob a ótica de Clean Code.");
            sb.AppendLine("Dê uma nota inteira de 0 a 10 e um breve comentário para os aspectos listados.");
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
            sb.AppendLine("  \"SugestaoRefatoracao\": \"...\"");
            sb.AppendLine("}");
            sb.AppendLine("```");

            sb.AppendLine();
            sb.AppendLine("Cada *nota* deve ser um número inteiro.");

            return sb.ToString();
        }
    }
}
