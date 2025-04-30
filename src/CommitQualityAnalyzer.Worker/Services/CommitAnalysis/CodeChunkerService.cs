using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço responsável por dividir um arquivo C# em "pedaços coerentes" (classes ou blocos grandes)
    /// respeitando um limite máximo de caracteres definido na configuração em Ollama:MaxPartLength.
    /// A divisão nunca corta uma declaração de classe no meio; caso um único bloco exceda o limite,
    /// ele é retornado inteiro mesmo assim.
    /// </summary>
    public class CodeChunkerService
    {
        private readonly int _maxPartLength;

        public CodeChunkerService(IConfiguration configuration)
        {
            // usa 2500 como padrão para retro-compatibilidade caso a chave não exista
            _maxPartLength = configuration.GetValue<int>("Ollama:MaxPartLength", 2500);
        }

        /// <summary>
        /// Divide o conteúdo de um arquivo fonte C# em blocos coerentes.
        /// </summary>
        /// <param name="source">Conteúdo completo do arquivo .cs</param>
        /// <returns>Lista de partes de texto</returns>
        public IEnumerable<string> Split(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                yield break;
            }

            // Regex simples para início de declaração de classe/struct/record/enum.
            var classRegex = new Regex(@"^\s*(public|internal|protected|private|sealed|static|abstract|partial)?\s*(class|struct|record|enum)\s+", RegexOptions.Compiled | RegexOptions.Multiline);

            var lines = source.Split('\n');
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                // Novo bloco se encontrar início de classe E já existe conteúdo acumulado.
                if (classRegex.IsMatch(line) && sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }

                sb.AppendLine(line);

                if (sb.Length >= _maxPartLength)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
            {
                yield return sb.ToString();
            }
        }
    }
}
