# Ouroboros Engine

[![CI](https://github.com/PMeeske/ouroboros-engine/actions/workflows/ci.yml/badge.svg)](https://github.com/PMeeske/ouroboros-engine/actions/workflows/ci.yml)
[![Mutation Testing](https://github.com/PMeeske/ouroboros-engine/actions/workflows/mutation.yml/badge.svg)](https://github.com/PMeeske/ouroboros-engine/actions/workflows/mutation.yml)
![Tests](https://img.shields.io/badge/tests-2%2C556%20passed%20%7C%2026%20known%20failures-brightgreen)
![Coverage](https://img.shields.io/badge/line%20coverage-see%20breakdown-blue)

**Ouroboros Engine** is the core execution layer of the Ouroboros cognitive AI system, providing composable AI pipelines, agent orchestration, provider integrations, and network capabilities.

## Test Coverage

> Coverage is measured by CI via [Coverlet](https://github.com/coverlet-coverage/coverlet) + [ReportGenerator](https://github.com/danielpalme/ReportGenerator). See the latest [CI run](https://github.com/PMeeske/ouroboros-engine/actions/workflows/ci.yml) for the full HTML report artifact.

| Project | Line Coverage | Status |
| ------- | :----------: | :----: |
| Ouroboros.Providers (McpServer) | 97.5% | ![97.5%](https://img.shields.io/badge/97.5%25-brightgreen) |
| Ouroboros.Pipeline (LangChain) | 100% | ![100%](https://img.shields.io/badge/100%25-brightgreen) |
| Ouroboros.Pipeline (SemanticKernel) | 83.1% | ![83.1%](https://img.shields.io/badge/83.1%25-brightgreen) |
| Ouroboros.Agent | -- | *See CI report* |
| Ouroboros.Network | -- | *See CI report* |

> **Note:** 26 pre-existing test failures are API-dependent (Providers, Safety, Network, Meta) and are not caused by code changes. Full per-assembly coverage is available in the CI coverage report artifact.

## Overview

This repository is part of the larger [Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2) ecosystem and contains the engine layer components that power AI workflows. It follows functional programming principles with type-safe, composable operations using monadic patterns.

### What's in This Repository

The `ouroboros-engine` contains seven main projects:

- **Ouroboros.Agent** - AI orchestration and Meta-AI layer for autonomous agent execution
- **Ouroboros.Pipeline** - Functional programming-based AI pipeline system with monadic composition
- **Ouroboros.Providers** - Integrations with AI providers (OpenAI, Anthropic, Ollama, etc.)
- **Ouroboros.Network** - Network communication and distributed system capabilities
- **Ouroboros.LangChain** - LangChain integration bridge for chain composition and tool calling
- **Ouroboros.McpServer** - MCP (Model Context Protocol) server implementation exposing tools via stdio transport
- **Ouroboros.SemanticKernel** - Microsoft Semantic Kernel integration for plugin-based orchestration

### Key Features

- **Functional-First Architecture** - Built on monadic composition (Result<T>, Option<T>) and Kleisli arrows
- **Type-Safe Pipelines** - Compile-time guarantees for AI workflow composition
- **Multi-Provider Support** - Unified interface for various AI providers
- **Event Sourcing** - Complete audit trail with replay capability
- **Extensible Design** - Plugin architecture for tools and providers

## Relationship to Ouroboros Ecosystem

This repository is the **engine layer** in the Ouroboros architecture:

```
Ouroboros-v2 (Main System)
├── foundation     (Core abstractions & domain models)
├── engine        (This repository - AI pipelines & orchestration)
└── app           (Application layer & user interfaces)
```

**Dependencies:**
- Requires `ouroboros-foundation` for core abstractions (Ouroboros.Core, Ouroboros.Domain, Ouroboros.Tools)
- Uses `ouroboros-build` for shared build configurations

**Related Repositories:**
- [Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2) - Main Ouroboros system
- [ouroboros-foundation](https://github.com/PMeeske/ouroboros-foundation) - Core domain and abstractions
- [ouroboros-build](https://github.com/PMeeske/ouroboros-build) - Shared build infrastructure

## Prerequisites

- **.NET 10.0 SDK** or later
- **C# 14** language features
- Access to `ouroboros-foundation` repository (dependency)

## Building

Build all projects:

```bash
dotnet build
```

Build a specific project:

```bash
dotnet build src/Ouroboros.Agent/Ouroboros.Agent.csproj
dotnet build src/Ouroboros.Pipeline/Ouroboros.Pipeline.csproj
dotnet build src/Ouroboros.Providers/Ouroboros.Providers.csproj
dotnet build src/Ouroboros.Network/Ouroboros.Network.csproj
dotnet build src/Ouroboros.LangChain/Ouroboros.LangChain.csproj
dotnet build src/Ouroboros.McpServer/Ouroboros.McpServer.csproj
dotnet build src/Ouroboros.SemanticKernel/Ouroboros.SemanticKernel.csproj
```

## Testing

Run all tests:

```bash
dotnet test
```

Run tests for a specific project:

```bash
dotnet test tests/Ouroboros.Agent.Tests/
dotnet test tests/Ouroboros.Pipeline.Tests/
dotnet test tests/Ouroboros.Providers.Tests/
dotnet test tests/Ouroboros.Network.Tests/
```

Run BDD tests:

```bash
dotnet test tests/Ouroboros.Engine.BDD/
```

### Generating Coverage Locally

The repository maintains a minimum test coverage threshold of 80%. To generate a coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Project Structure

```
ouroboros-engine/
├── src/
│   ├── Ouroboros.Agent/        # Agent orchestration & Meta-AI
│   ├── Ouroboros.Pipeline/     # AI pipeline system
│   ├── Ouroboros.Providers/    # AI provider integrations
│   ├── Ouroboros.Network/      # Network & distributed capabilities
│   ├── Ouroboros.LangChain/    # LangChain integration bridge
│   ├── Ouroboros.McpServer/    # MCP server implementation
│   └── Ouroboros.SemanticKernel/ # Semantic Kernel integration
├── tests/
│   ├── Ouroboros.Agent.Tests/
│   ├── Ouroboros.Pipeline.Tests/
│   ├── Ouroboros.Providers.Tests/
│   ├── Ouroboros.Network.Tests/
│   └── Ouroboros.Engine.BDD/   # Behavior-driven tests
└── docs/                       # Technical documentation
```

## Documentation

- **[Documentation Index](docs/README.md)** - Complete documentation navigation
- **[Architecture](docs/ARCHITECTURE.md)** - System architecture and design principles
- **[Contributing Guide](docs/CONTRIBUTING.md)** - Guidelines for contributors
- **[API Documentation](docs/api/README.md)** - API reference

### Key Documentation

- **Architecture & Design**
  - [Architectural Layers](docs/ARCHITECTURAL_LAYERS.md) - Visual system architecture
  - [Cognitive Architecture](docs/COGNITIVE_ARCHITECTURE.md) - AI cognitive patterns
  - [Orchestration Guide](docs/ORCHESTRATION_GUIDE.md) - Agent orchestration

- **Development**
  - [GitHub Copilot Development Loop](docs/COPILOT_DEVELOPMENT_LOOP.md) - AI-assisted development
  - [Feature Engineering](docs/FEATURE_ENGINEERING.md) - Feature extraction approaches

## CI/CD

This repository uses GitHub Actions for continuous integration:

- **CI Pipeline** (`.github/workflows/ci.yml`) - Build, test, and coverage checks on all PRs and main branch
- **Mutation Testing** (`.github/workflows/mutation.yml`) - Stryker.NET mutation testing

The CI pipeline leverages reusable workflows from [ouroboros-build](https://github.com/PMeeske/ouroboros-build).

## Contributing

Contributions are welcome! Please read our [Contributing Guide](docs/CONTRIBUTING.md) for details on:

- Code style and standards
- Development workflow
- Testing requirements
- Pull request process

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support & Contact

- **Issues**: [GitHub Issues](https://github.com/PMeeske/ouroboros-engine/issues)
- **Main Project**: [Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2)

---

Part of the **Ouroboros** cognitive AI system.
