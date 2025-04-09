# CommitQualityAnalyzer

Um sistema que analisa commits de código para avaliar sua qualidade usando modelos de linguagem (Ollama).

## Sobre o Projeto

O CommitQualityAnalyzer analisa commits de código e fornece métricas de qualidade baseadas em:
- Clean Code
- Princípios SOLID
- Design Patterns
- Testabilidade
- Segurança

O sistema extrai as diferenças (diffs) entre versões de código em um commit e envia para um modelo de linguagem avaliar, processando a resposta para extrair métricas de qualidade em formato JSON padronizado.

## Configuração

### Pré-requisitos

- Docker e Docker Compose
- .NET 8.0 SDK
- Git

### Instalação

1. Clone o repositório:
   ```bash
   git clone https://github.com/seu-usuario/CommitQualityAnalyzer.git
   cd CommitQualityAnalyzer
   ```

2. Inicie os containers Docker:
   ```bash
   docker-compose up -d
   ```

3. Crie os modelos personalizados com contexto maior:
   ```bash
   # Aguarde alguns segundos para o Ollama inicializar
   docker exec ollama ollama create codellama-extended -f /Modelfile
   docker exec ollama ollama create deepseek-extended -f /DeepseekModelfile
   ```

4. Configure o repositório Git a ser analisado no arquivo `appsettings.json`:
   ```json
   "GitRepository": {
     "Path": "caminho/para/seu/repositorio"
   }
   ```

5. Execute o analisador:
   ```bash
   dotnet run --project src/CommitQualityAnalyzer.Worker
   ```

### Configuração dos Modelos

O projeto inclui dois modelos personalizados:

1. **codellama-extended**: Modelo CodeLlama com contexto de 8192 tokens
2. **deepseek-extended**: Modelo DeepSeek Coder com contexto de 16384 tokens

Para alternar entre os modelos, edite o arquivo `appsettings.json`:

```json
"Ollama": {
  "ModelName": "deepseek-extended",  // ou "codellama-extended"
  "ContextLength": 16384,  // ou 8192 para CodeLlama
}
```

## Arquivos de Configuração

Os arquivos de configuração dos modelos estão incluídos no projeto:

- **Modelfile**: Configuração para o modelo CodeLlama
- **DeepseekModelfile**: Configuração para o modelo DeepSeek Coder

Você pode personalizar esses arquivos para ajustar parâmetros como temperatura, contexto, etc.

## Persistência de Dados

Os dados dos modelos e do MongoDB são persistidos em volumes Docker:
- `ollama_data`: Armazena os modelos e configurações do Ollama
- `mongodb_data`: Armazena os resultados das análises

Isso garante que suas configurações e dados sejam preservados mesmo após reiniciar os containers.

## Tratamento de Prompts Longos

O sistema implementa estratégias para lidar com prompts longos:
- Divisão inteligente de prompts em partes menores
- Análise apenas das diferenças relevantes entre versões
- Cabeçalhos informativos para cada parte do prompt

## Visualização dos Resultados

Os resultados das análises podem ser visualizados através do MongoDB Express:
- URL: http://localhost:8081
- Banco de dados: commitanalyzer
- Coleção: codeanalyses
