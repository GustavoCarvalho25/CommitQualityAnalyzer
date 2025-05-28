# RefactorScore

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Docker](https://img.shields.io/badge/Docker-Required-blue.svg)

RefactorScore is an advanced system for analyzing Git commit quality using local Large Language Models (LLMs). The system provides detailed analysis of code changes based on Clean Code principles, helping developers and teams improve their code quality over time.

## 📝 Table of Contents

- [Features](#-features)
- [System Architecture](#-system-architecture)
- [Technology Stack](#-technology-stack)
- [Installation](#-installation)
- [Configuration](#-configuration)
- [Usage](#-usage)
- [Code Analysis](#-code-analysis)
- [Project Structure](#-project-structure)
- [Testing](#-testing)
- [Development](#-development)
- [License](#-license)

## ✨ Features

- **Automated Git Commit Analysis**: Automatically analyzes commits from the past 24 hours to evaluate code quality.
- **Clean Code Evaluation**: Assesses code changes against key Clean Code principles.
- **Local LLM Integration**: Utilizes locally hosted language models (via Ollama) for privacy and customization.
- **Smart Processing**: Handles large files using a "window sliding" approach to manage context limitations.
- **Change Type Detection**: Adapts analysis based on the type of code changes (added, modified, or renamed).
- **File Type Filtering**: Focuses analysis on code files while ignoring binary files and resources.
- **Persistent Results**: Stores analysis results in MongoDB for historical tracking and review.
- **Caching Layer**: Uses Redis to cache intermediate results for improved performance.
- **Background Processing**: Processes commits in the background with configurable schedules.
- **Extensible Design**: Clean Architecture enables easy extension and adaptation to different needs.

## 🏗 System Architecture

RefactorScore follows a Clean Architecture design, with well-separated layers:

1. **Core**: Contains all entities, interfaces, and domain rules. This layer has no dependencies on external frameworks.
   - Entities (CommitInfo, CommitFileChange, CodeAnalysis)
   - Interfaces (IGitRepository, ILLMService, IAnalysisRepository, ICacheService, ICodeAnalyzerService)
   - Specifications (Result pattern for error handling)

2. **Application**: Contains business logic and orchestrates the flow of data.
   - CodeAnalyzerService (manages the analysis workflow)
   - Service registration extensions

3. **Infrastructure**: Contains implementations of interfaces from the Core layer.
   - GitRepository (using LibGit2Sharp)
   - OllamaService (for LLM integration)
   - MongoDbAnalysisRepository (for persistent storage)
   - RedisCacheService (for caching)

4. **WorkerService**: Background service that periodically scans repositories for new commits.
   - CommitAnalysisWorker (background service)
   - Configuration with Serilog for logging

## 🔧 Technology Stack

- **Language and Framework**: C# and .NET 8.0
- **LLM Integration**: Ollama (local LLM server)
- **Storage**:
  - MongoDB (persistent storage)
  - Redis (caching)
- **Git Integration**: LibGit2Sharp
- **Containerization**: Docker and Docker Compose
- **Logging**: Serilog
- **Testing**: xUnit, Moq

## 📦 Installation

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://docs.docker.com/get-docker/) and [Docker Compose](https://docs.docker.com/compose/install/)
- [Git](https://git-scm.com/downloads)

### Clone the Repository

```bash
git clone https://github.com/GustavoCarvalho25/RefactorScore
cd RefactorScore
```

### Start Required Services

Start the required services using Docker Compose:

```bash
docker-compose up -d
```

This will start:
- Ollama (LLM server)
- MongoDB (database)
- Redis (cache)
- Mongo Express (MongoDB admin UI)
- Redis Commander (Redis admin UI)

### Create Custom LLM Model

Create the custom model for code analysis:

```bash
# Wait for Ollama to start
docker exec refactorscore-ollama ollama create refactorscore -f ./ModelFiles/Modelfile
```

### Configure the Application

Update the `appsettings.json` file in the WorkerService project:

```json
{
  "GitRepository": {
    "RepositoryPath": "C:\\path\\to\\your\\git\\repository"
  }
}
```

## ⚙️ Configuration

RefactorScore can be configured through the `appsettings.json` files:

### Git Repository

```json
"GitRepository": {
  "RepositoryPath": "C:\\your\\repo\\path\\here"
}
```

### Ollama LLM

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434/",
  "DefaultModel": "refactorscore",
  "Temperature": 0.1,
  "MaxTokens": 2048,
  "TopP": 0.9,
  "TopK": 40
}
```

### Redis Cache

```json
"RedisCache": {
  "ConnectionString": "localhost:6379",
  "KeyPrefix": "refactorscore",
  "DatabaseId": 0,
  "DefaultExpiryHours": 24
}
```

### MongoDB

```json
"MongoDB": {
  "ConnectionString": "mongodb://admin:admin123@localhost:27017/?authSource=admin",
  "DatabaseName": "RefactorScore",
  "AnaliseCommitCollectionName": "AnaliseDeCommits",
  "AnaliseArquivoCollectionName": "AnaliseDeArquivos",
  "RecomendacoesCollectionName": "Recomendacoes"
}
```

### Worker Service

```json
"Worker": {
  "ScanIntervalMinutes": 60,
  "MaxProcessingCommits": 10
}
```

## 🚀 Usage

### Run the Worker Service

```bash
dotnet run --project src/WorkerService/RefactorScore.WorkerService.csproj
```

This will start the worker service, which will:
1. Connect to your specified Git repository
2. Analyze commits from the past 24 hours
3. Store the analysis results in MongoDB
4. Run periodically based on the configured interval

### View Analysis Results

You can access the analysis results through:

- **MongoDB Express**: http://localhost:8081
  - Navigate to the "RefactorScore" database and view the following collections:
    - `AnaliseDeCommits`: Full commit analyses
    - `AnaliseDeArquivos`: Individual file analyses
    - `Recomendacoes`: Detailed recommendations for improving code

- **Redis Commander**: http://localhost:8082
  - View cached analyses and intermediate results

## 📊 Code Analysis

RefactorScore evaluates the following aspects of Clean Code (on a scale of 0-10):

1. **Variable Naming**: Assesses whether variable names are clear, descriptive, and follow naming conventions.
2. **Function Size**: Evaluates if functions are small, focused, and have a single responsibility.
3. **Comment Usage**: Checks for the presence and quality of helpful comments (not self-explanatory code).
4. **Method Cohesion**: Analyzes if methods do one thing and if they're logically organized.
5. **Dead Code Avoidance**: Identifies and penalizes redundant or unused code.

Each analysis produces:
- Individual scores (0-10) for each criterion
- An overall score (average of all criteria)
- Textual justification explaining the assessment

## 📂 Project Structure

```
RefactorScore/
├── docker-compose.yml        # Docker services configuration
├── ModelFiles/               # LLM model configuration files
│   └── Modelfile             # Custom Ollama model definition
├── src/                      # Source code
│   ├── Core/                 # Core entities and interfaces
│   │   ├── Entities/         # Domain entities
│   │   ├── Interfaces/       # Domain interfaces
│   │   └── Specifications/   # Result pattern
│   ├── Application/          # Business logic and use cases
│   │   └── Services/         # Application services
│   ├── Infrastructure/       # External implementations
│   │   ├── GitIntegration/   # Git repository implementation
│   │   ├── MongoDB/          # MongoDB repository
│   │   ├── Ollama/           # LLM service
│   │   └── RedisCache/       # Redis cache service
│   └── WorkerService/        # Background service
│       └── Workers/          # Worker implementations
└── tests/                    # Test projects
    ├── Core.Tests/           # Unit tests for Core layer
    ├── Application.Tests/    # Unit tests for Application layer
    └── Integration.Tests/    # Integration tests
```

## 🧪 Testing

The project includes comprehensive tests:

### Unit Tests

```bash
dotnet test tests/Core.Tests/RefactorScore.Core.Tests.csproj
dotnet test tests/Application.Tests/RefactorScore.Application.Tests.csproj
```

### Integration Tests

```bash
dotnet test tests/Integration.Tests/RefactorScore.Integration.Tests.csproj
```

Note: Integration tests require running infrastructure (MongoDB, Redis, and Ollama).

## 🛠 Development

### Building the Project

```bash
dotnet build
```

### Running with Different Configurations

For development environment:

```bash
dotnet run --project src/WorkerService/RefactorScore.WorkerService.csproj --environment Development
```

### Extending the System

To add new analysis criteria:
1. Extend the `CleanCodeAnalysis` class in the Core layer
2. Update the LLM prompt in the `Modelfile`
3. Modify the `CodeAnalyzerService` to handle the new criteria

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 📚 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
