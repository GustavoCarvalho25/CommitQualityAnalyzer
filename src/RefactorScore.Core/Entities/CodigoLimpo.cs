using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa a análise de código limpo para um arquivo ou trecho
    /// </summary>
    public class CodigoLimpo
    {
        /// <summary>
        /// Pontuação para nomenclatura de variáveis (0-10)
        /// </summary>
        public int NomenclaturaVariaveis { get; set; }
        
        /// <summary>
        /// Pontuação para tamanho das funções (0-10)
        /// </summary>
        public int TamanhoFuncoes { get; set; }
        
        /// <summary>
        /// Pontuação para uso de comentários relevantes (0-10)
        /// </summary>
        public int UsoComentariosRelevantes { get; set; }
        
        /// <summary>
        /// Pontuação para coesão dos métodos (0-10)
        /// </summary>
        public int CoesaoMetodos { get; set; }
        
        /// <summary>
        /// Pontuação para evitação de código morto (0-10)
        /// </summary>
        public int EvitacaoCodigoMorto { get; set; }
        
        /// <summary>
        /// Nota geral, calculada como média das pontuações individuais
        /// </summary>
        public double NotaGeral => CalcularNotaGeral();
        
        /// <summary>
        /// Justificativas para cada critério de avaliação
        /// </summary>
        public Dictionary<string, string> Justificativas { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Propriedades adicionais que podem ser usadas para armazenar critérios extras
        /// </summary>
        public Dictionary<string, object> CriteriosAdicionais { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Calcula a nota geral como média das pontuações individuais
        /// </summary>
        private double CalcularNotaGeral()
        {
            return (NomenclaturaVariaveis + TamanhoFuncoes + UsoComentariosRelevantes + 
                   CoesaoMetodos + EvitacaoCodigoMorto) / 5.0;
        }
        
        /// <summary>
        /// Converte a nota geral para um nível de qualidade descritivo
        /// </summary>
        public string NivelQualidade
        {
            get
            {
                return NotaGeral switch
                {
                    >= 9.0 => "Excelente",
                    >= 7.5 => "Muito Bom",
                    >= 6.0 => "Bom",
                    >= 5.0 => "Aceitável",
                    >= 3.5 => "Precisa de Melhorias",
                    _ => "Problemático"
                };
            }
        }
    }
} 