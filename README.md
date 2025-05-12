# RefactorScore

Sistema de avaliação automatizada de commits com modelos de linguagem natural locais (LLM).

## Sobre o Projeto

O RefactorScore analisa automaticamente commits de código realizados nas últimas 24 horas em um repositório Git local, avaliando sua qualidade com base em boas práticas de Clean Code. O sistema utiliza um modelo de linguagem natural local (LLM) executado via Ollama, e armazena os resultados estruturados no MongoDB.

A arquitetura do sistema é escalável e segmentada para lidar com limitações de memória e restrições de tamanho de prompt, possibilitando avaliações fracionadas, armazenadas temporariamente no Redis, e agregadas para compor uma análise final estruturada e persistente.

## Arquitetura do Sistema

O sistema segue uma arquitetura Clean Architecture com as seguintes camadas:

- **Core**: Entidades de domínio e interfaces base
- **Application**: Casos de uso e lógica de aplicação
- **Infrastructure**: Implementações concretas de repositórios e serviços
- **WebApi**: API REST para interação com o sistema
- **WorkerService**: Serviço de processamento em background

## Pré-requisitos

- Docker e Docker Compose
- .NET 8.0 SDK
- Git instalado no sistema

## Configuração e Execução

1. Clone o repositório:
   ```bash
   git clone https://github.com/seu-usuario/RefactorScore.git
   cd RefactorScore
   ```

2. Inicie os serviços Docker:
   ```bash
   docker-compose up -d
   ```

3. Crie o modelo personalizado para análise:
   ```bash
   # Aguarde o Ollama inicializar
   docker exec refactorscore-ollama ollama create refactorscore -f /ModelFiles/Modelfile
   ```

4. Execute o serviço Worker:
   ```bash
   dotnet run --project src/WorkerService/RefactorScore.WorkerService.csproj
   ```

## Componentes do Sistema

### Docker Compose

O arquivo `docker-compose.yml` orquestra os seguintes serviços:

- **Ollama**: Para execução do modelo de linguagem local
- **MongoDB**: Para armazenamento persistente das análises
- **Redis**: Para armazenamento temporário e contexto de processamento
- **Mongo Express**: Interface web para visualização dos dados no MongoDB
- **Redis Commander**: Interface web para visualização dos dados no Redis

### Modelo LLM

O sistema utiliza modelos de linguagem locais via Ollama, com configurações personalizadas para análise de código (ver `ModelFiles/Modelfile`).

### Armazenamento de Dados

- **Redis**: Armazena análises parciais temporárias
- **MongoDB**: Armazena os resultados finais das análises

## Análise de Clean Code

O sistema avalia os seguintes aspectos de Clean Code (com notas de 0 a 10):

- Nomenclatura de variáveis
- Tamanho das funções
- Uso de comentários relevantes
- Coesão entre métodos
- Evitação de código morto ou redundante

## Visualização dos Resultados

- **MongoDB Express**: http://localhost:8081
- **Redis Commander**: http://localhost:8082

## Licença

Este projeto está licenciado sob a licença MIT - veja o arquivo LICENSE para detalhes. 