using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    public class CodigoLimpo
    {
        public int NomenclaturaVariaveis { get; set; }
        public int TamanhoFuncoes { get; set; }
        public int UsoComentariosRelevantes { get; set; }
        public int CoesaoMetodos { get; set; }
        public int EvitacaoCodigoMorto { get; set; }
        public double NotaGeral => CalcularNotaGeral();
        public Dictionary<string, string> Justificativas { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> CriteriosAdicionais { get; set; } = new Dictionary<string, object>();
        private double CalcularNotaGeral()
        {
            return (NomenclaturaVariaveis + TamanhoFuncoes + UsoComentariosRelevantes + 
                   CoesaoMetodos + EvitacaoCodigoMorto) / 5.0;
        }
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