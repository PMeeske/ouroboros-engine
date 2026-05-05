# CLAUDE.md — Ouroboros Engine

This file provides context for AI assistants working in this repository.

## Project Overview

Ouroboros Engine is the core execution layer of the Ouroboros cognitive AI system. It provides composable AI pipeline orchestration with multi-provider support, agent orchestration, event sourcing, and neural-symbolic reasoning.

- **Language:** C# 14 (.NET 10.0)
- **Paradigm:** Functional-first with monadic composition (Result<T>, Option<T>, Kleisli arrows)
- **License:** MIT
- **Ecosystem:** Part of Ouroboros-v2 (meta-repo: foundation → engine → app)

## Repository Structure

```
ouroboros-engine/
├── src/                        # Source projects
│   ├── Ouroboros.Agent/        # AI orchestration, MetaAI, Society of Mind, CQRS dispatch
│   ├── Ouroboros.Pipeline/     # Functional pipeline composition, reasoning, ingestion, GraphRAG
│   ├── Ouroboros.Providers/    # Multi-provider integrations (OpenAI, Anthropic, Azure, Ollama, etc.)
│   ├── Ouroboros.Network/      # Distributed network state, Merkle-DAG reasoning history
│   ├── Ouroboros.SemanticKernel/ # Semantic Kernel adapter
│   └── Ouroboros.McpServer/    # Model Context Protocol server
├── tests/                      # Test projects (mirrors src/ structure)
│   ├── Ouroboros.Agent.Tests/
│   ├── Ouroboros.Learning.Tests/
│   ├── Ouroboros.McpServer.Tests/
│   ├── Ouroboros.Memory.Tests/
│   ├── Ouroboros.Meta.Tests/
│   ├── Ouroboros.Network.Tests/
│   ├── Ouroboros.Pipeline.Tests/
│   ├── Ouroboros.Providers.Tests/
│   ├── Ouroboros.Safety.Tests/
│   ├── Ouroboros.SemanticKernel.Tests/
│   └── Ouroboros.Engine.BDD/   # Behavior-driven tests (Reqnroll/Gherkin)
├── libs/foundation/            # Git submodule → ouroboros-foundation (develop branch)
├── protos/                     # gRPC protobuf definitions (HyperonGrammarService)
├── tools/                      # Utilities (hyperon-sidecar, tapo_gateway)
├── docs/                       # Technical documentation (70+ files)
└── comic-generator/            # Standalone comic-strip generator
```

### Key Source Modules

**Ouroboros.Agent** — The largest project with 228+ files in MetaAI alone:
- `MetaAI/` — Self-improving AI orchestration (affect, evolution, meta-learning, self-model, world-model)
- `ConsolidatedMind/` — Society of Mind multiplexer with collective routing
- `Dispatch/` — CQRS command/query separation
- `NeuralSymbolic/` — Bridge between neural (LLM) and symbolic (MeTTa) reasoning
- `TheoryOfMind/` — Agent belief tracking and modeling

**Ouroboros.Pipeline** — Core pipeline framework:
- `Reasoning/` — Kleisli arrow composition for pipeline steps
- `Grammar/` — ANTLR grammar evolution with Roslyn compilation
- `GraphRAG/` — Knowledge graph retrieval-augmented generation
- `Prompts/` — YAML prompt templates (embedded resources)
- `MeTTa/` — MeTTa schema definitions

**Ouroboros.Providers** — 15+ provider integrations:
- `Anthropic/`, `DeepSeek/`, `Docker/`, `DuckDuckGo/`, `Firecrawl/`, `Kubernetes/`, `LoadBalancing/`, `Routing/`, `SpeechToText/`, `TextToSpeech/`, `Tapo/`

## Build & Development

### Prerequisites

- .NET 10.0 SDK
- Git submodules initialized (`git submodule update --init --recursive`)

### Common Commands

```bash
# Build all projects
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Ouroboros.Pipeline.Tests

# Run mutation testing (Stryker.NET)
dotnet tool restore
dotnet stryker
```

### Build Configuration

The build system uses a priority-based `Directory.Build.props` chain:
1. Meta-repo parent `.build` (when building from Ouroboros-v2)
2. `libs/foundation/.build` (standalone CI)
3. Local `.build` submodule
4. Peer checkout `../ouroboros-build`
5. Fallback: minimal defaults (net10.0, C# 14, nullable, implicit usings)

Global usings for the engine layer are defined in `src/Directory.Build.props` and include `Ouroboros.Core.Monads`, `Ouroboros.Core.Kleisli`, `Ouroboros.Domain.*`, and `Ouroboros.Tools`.

### Foundation Submodule

The `libs/foundation/` submodule provides core abstractions:
- `Ouroboros.Abstractions` — Interfaces and contracts
- `Ouroboros.Core` — Monads (Result, Option), Kleisli arrows, learning, memory, synthesis
- `Ouroboros.Domain` — Events, states, vectors, autonomous entities
- `Ouroboros.Tools` — MeTTa integration, genetic algorithms

## Coding Conventions

### Style

- **Namespaces:** File-scoped (`namespace Ouroboros.Pipeline.Grammar;`)
- **Classes:** PascalCase, prefer `sealed` where appropriate
- **Methods:** PascalCase, async methods suffixed with `Async`
- **Parameters:** camelCase
- **Private fields:** `_camelCase` prefix
- **Records:** Use for immutable DTOs (`public sealed record ParseFailureInfo(...)`)
- **Nullable:** Enabled globally — all reference types must handle nullability

### Functional Programming Patterns

This codebase uses functional programming patterns extensively. Follow these conventions:

- **Result<T> monad** for error handling in pipelines — avoid throwing exceptions for expected failures
- **Option<T>** for optional values — avoid returning null
- **Kleisli arrow composition** for pipeline step chaining: `Func<Input, Task<Output>>`
- **Pure functions** preferred; isolate side effects at boundaries
- **Immutable data structures** (ImmutableCollections) where possible

### Documentation

- XML documentation (`/// <summary>`) required for all public APIs
- Include `<param>`, `<returns>`, and `<remarks>` tags for design rationale
- Tests are exempt from XML doc requirements (suppressed via `CS1591;SA0001`)

### Error Handling

- Custom sealed exception classes with context properties (e.g., `GrammarCompilationException`)
- Pipeline operations return `Result<T>` — do not throw for recoverable errors
- Use `ArgumentNullException.ThrowIfNull()` for parameter validation
- All I/O operations must be async with `CancellationToken` support

## Testing

### Frameworks

- **xUnit** 2.9.3 — test framework
- **FluentAssertions** 8.8.0 — readable assertions
- **Moq** — mocking (most projects)
- **NSubstitute** — mocking (Pipeline.Tests)
- **AutoFixture** / **Bogus** — test data generation
- **Reqnroll** — BDD/Gherkin (Engine.BDD)
- **Coverlet** — code coverage collection
- **Stryker.NET** — mutation testing

### Test Pattern

Follow AAA (Arrange, Act, Assert):

```csharp
[Fact]
public async Task MethodName_WithCondition_ExpectedBehavior()
{
    // Arrange
    var sut = new SystemUnderTest(mockDependency.Object);

    // Act
    var result = await sut.DoSomethingAsync(input);

    // Assert
    result.Should().BeSuccess();
}
```

### Coverage

- CI enforces **60% minimum** coverage threshold
- New code should target **80%+ coverage**
- Run coverage: `dotnet test --collect:"XPlat Code Coverage"`

### Test Organization

- Test classes use `[Trait("Category", "Unit")]` for categorization
- One test class per production class
- Mock helper classes for complex domain objects
- Comprehensive edge-case coverage (nulls, empty collections, cancellation)

## CI/CD

### GitHub Actions Workflows

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `ci.yml` | Push to main/develop, PRs | Build, test (60% coverage), BDD, coverage badge |
| `mutation.yml` | Weekly (Sunday 3am UTC) | Stryker.NET mutation testing per project |
| `update-submodules.yml` | Dispatch | Foundation submodule pointer updates |
| `notify-downstream.yml` | Dispatch | Notify dependent repos of changes |

CI uses reusable workflows from `PMeeske/ouroboros-build`.

## Commit Conventions

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `perf`, `chore`

Examples:
```
feat(agent): implement GoalSplitter with self-modification governance
fix(mcp): use empty object for null inputSchema per spec
chore: update libs/foundation pointer
```

## Architecture Notes

### Design Patterns

- **Hexagonal (Ports & Adapters):** Core logic in Agent/Pipeline, Providers as adapters
- **Society of Mind:** ConsolidatedMind multiplexer with collective agent routing
- **Event Sourcing:** Immutable audit trails with replay capability
- **CQRS:** Command/query separation in Agent dispatch
- **Resilience:** Polly policies for retry, timeout, and circuit breaking

### Dependency Flow

```
Ouroboros-v2 (meta-repo)
  └── ouroboros-app (Application layer)
        └── ouroboros-engine (this repo — Engine layer)
              ├── Agent → Pipeline, Providers, Foundation
              ├── Pipeline → Foundation
              ├── Providers → Foundation
              └── Network → Foundation
                    └── ouroboros-foundation (Foundation layer — submodule)
```

### Known Issues

- **Circular dependency:** `OrchestratorBase` ↔ `MetaAI` (planned split into separate projects)
- **~26 known test failures** in Providers, Safety, Network, and Meta projects (API-dependent or environment-specific)
- **MockChatModel**: Some tests reference `Ouroboros.Tests.Mocks.MockChatModel` — ensure the mock exists in the relevant test project

### Gotchas

- **FluentAssertions 8.x**: Use `BeGreaterThanOrEqualTo` (not `BeGreaterOrEqualTo`)
- **`.feature.cs` files** are auto-generated by Reqnroll — safe to delete in merge conflicts
- **German locale CI output**: "Fehler" = errors, "Bestanden" = passed, "Buildvorgang" = build process

### Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.AI | 10.3.0 | Unified AI provider abstraction |
| Anthropic.SDK | 5.10.0 | Claude API integration |
| Microsoft.SemanticKernel | 1.72.0 | Semantic Kernel bridge |
| Qdrant.Client | 1.17.0 | Vector database |
| Polly | 8.6.5 | Resilience patterns |
| System.Reactive | 6.1.0 | Reactive extensions |
| Antlr4.Runtime.Standard | 4.13.1 | Grammar parsing |
| OllamaSharp | 5.4.18 | Local model integration |
