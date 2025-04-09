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
            _logger.LogInformation("Construindo prompt para análise de diferenças em {FilePath}", filePath);
            
            var promptBuilder = new StringBuilder();
            
            // Adicionar informações do commit
            promptBuilder.AppendLine("# Análise de Qualidade de Código");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine($"## Informações do Commit");
            promptBuilder.AppendLine($"- **ID do Commit**: {commit.Sha}");
            promptBuilder.AppendLine($"- **Autor**: {commit.AuthorName}");
            promptBuilder.AppendLine($"- **Data**: {commit.AuthorDate}");
            promptBuilder.AppendLine($"- **Mensagem**: {commit.Message}");
            promptBuilder.AppendLine($"- **Arquivo**: {filePath}");
            promptBuilder.AppendLine();
            
            // Adicionar as diferenças de código
            promptBuilder.AppendLine("## Diferenças de Código");
            promptBuilder.AppendLine("```diff");
            promptBuilder.AppendLine(diffText);
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine();
            
            // Adicionar instruções para análise
            promptBuilder.AppendLine("## Instruções");
            promptBuilder.AppendLine("Analise as diferenças de código acima e avalie a qualidade do código em relação aos seguintes critérios:");
            promptBuilder.AppendLine("1. **Clean Code**: Legibilidade, nomes significativos, funções pequenas, etc.");
            promptBuilder.AppendLine("2. **SOLID**: Princípios de design orientado a objetos (Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion).");
            promptBuilder.AppendLine("3. **Design Patterns**: Uso adequado de padrões de design.");
            promptBuilder.AppendLine("4. **Testabilidade**: Facilidade para escrever testes unitários.");
            promptBuilder.AppendLine("5. **Segurança**: Práticas de segurança no código.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Para cada critério, atribua uma nota de 0 a 100 e forneça um comentário justificando a nota.");
            promptBuilder.AppendLine("Além disso, forneça um comentário geral sobre a qualidade do código e calcule uma nota final (média das notas dos critérios).");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Se houver oportunidades de melhoria, forneça uma proposta de refatoração com o código original e o código refatorado.");
            promptBuilder.AppendLine();
            
            // Adicionar formato de saída esperado
            promptBuilder.AppendLine("## Formato de Saída");
            promptBuilder.AppendLine("Forneça sua análise no formato JSON a seguir:");
            promptBuilder.AppendLine("```json");
            promptBuilder.AppendLine("{");
            promptBuilder.AppendLine("  \"analiseGeral\": {");
            promptBuilder.AppendLine("    \"CleanCode\": {");
            promptBuilder.AppendLine("      \"nota\": 0,");
            promptBuilder.AppendLine("      \"comentario\": \"\"");
            promptBuilder.AppendLine("    },");
            promptBuilder.AppendLine("    \"SOLID\": {");
            promptBuilder.AppendLine("      \"nota\": 0,");
            promptBuilder.AppendLine("      \"comentario\": \"\"");
            promptBuilder.AppendLine("    },");
            promptBuilder.AppendLine("    \"DesignPatterns\": {");
            promptBuilder.AppendLine("      \"nota\": 0,");
            promptBuilder.AppendLine("      \"comentario\": \"\"");
            promptBuilder.AppendLine("    },");
            promptBuilder.AppendLine("    \"Testabilidade\": {");
            promptBuilder.AppendLine("      \"nota\": 0,");
            promptBuilder.AppendLine("      \"comentario\": \"\"");
            promptBuilder.AppendLine("    },");
            promptBuilder.AppendLine("    \"Seguranca\": {");
            promptBuilder.AppendLine("      \"nota\": 0,");
            promptBuilder.AppendLine("      \"comentario\": \"\"");
            promptBuilder.AppendLine("    }");
            promptBuilder.AppendLine("  },");
            promptBuilder.AppendLine("  \"notaFinal\": 0,");
            promptBuilder.AppendLine("  \"comentarioGeral\": \"\",");
            promptBuilder.AppendLine("  \"propostaRefatoracao\": {");
            promptBuilder.AppendLine("    \"titulo\": \"\",");
            promptBuilder.AppendLine("    \"descricao\": \"\",");
            promptBuilder.AppendLine("    \"codigoOriginal\": \"\",");
            promptBuilder.AppendLine("    \"codigoRefatorado\": \"\"");
            promptBuilder.AppendLine("  }");
            promptBuilder.AppendLine("}");
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("REGRAS OBRIGATÓRIAS:");
            promptBuilder.AppendLine("1. Use EXATAMENTE os nomes de campos mostrados acima: \"AnaliseGeral\", \"CleanCode\", \"SOLID\", \"DesignPatterns\", \"Testabilidade\", \"Seguranca\", \"Nota\", \"Comentario\", \"ComentarioGeral\".");
            promptBuilder.AppendLine("2. As notas devem ser números decimais entre 0.0 e 100.0 (use ponto como separador decimal).");
            promptBuilder.AppendLine("3. A nota final deve ser a média aritmética das notas dos critérios.");
            promptBuilder.AppendLine("4. Coloque sua resposta dentro do bloco ```json``` para que possa ser processada corretamente.");
            promptBuilder.AppendLine("5. Não adicione explicações fora do bloco JSON.");
            
            return promptBuilder.ToString();
        }
    }
}
