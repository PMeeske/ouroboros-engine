# Ouroboros Engine

[![CI](https://github.com/PMeeske/ouroboros-engine/actions/workflows/ci.yml/badge.svg)](https://github.com/PMeeske/ouroboros-engine/actions/workflows/ci.yml)
[![Mutation Testing](https://github.com/PMeeske/ouroboros-engine/actions/workflows/mutation.yml/badge.svg)](https://github.com/PMeeske/ouroboros-engine/actions/workflows/mutation.yml)

**Ouroboros Engine** is the core execution layer of the Ouroboros cognitive AI system, providing composable AI pipelines, agent orchestration, provider integrations, and network capabilities.

## Overview

This repository is part of the larger [Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2) ecosystem and contains the engine layer components that power AI workflows. It follows functional programming principles with type-safe, composable operations using monadic patterns.

### What's in This Repository

The `ouroboros-engine` contains four main projects:

- **Ouroboros.Agent** - AI orchestration and Meta-AI layer for autonomous agent execution
- **Ouroboros.Pipeline** - Functional programming-based AI pipeline system with monadic composition
- **Ouroboros.Providers** - Integrations with AI providers (OpenAI, Anthropic, Ollama, etc.)
- **Ouroboros.Network** - Network communication and distributed system capabilities

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

### Test Coverage

The repository maintains a minimum test coverage threshold of 60%. To generate a coverage report:

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
│   └── Ouroboros.Network/      # Network & distributed capabilities
├── tests/
│   ├── Ouroboros.Agent.Tests/
│   ├── Ouroboros.Pipeline.Tests/
│   ├── Ouroboros.Providers.Tests/
│   ├── Ouroboros.Network.Tests/
│   ├── Ouroboros.Learning.Tests/
│   ├── Ouroboros.Safety.Tests/
│   ├── Ouroboros.Meta.Tests/
│   ├── Ouroboros.Memory.Tests/
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
