# Dynamic LLM-Backed ANTLR .NET Parser with MeTTa Atom Correction

## Architecture Overview

This module enables **dynamically generating, validating, and evolving programming language grammars at runtime** by combining:

- **ANTLR4 (.NET)** — industry-standard parser generator, compiled on the fly via Roslyn
- **MeTTa / Hyperon Atomspace** — symbolic reasoning for grammar validation, correction, and composition
- **Ollama (Local LLMs)** — grammar candidate generation from natural language descriptions

## Component Layout

```
tools/hyperon-sidecar/           ← Python gRPC server wrapping upstream Hyperon
  ├── grammar_service.py         ← gRPC service implementation
  ├── grammar_atoms.metta        ← Full MeTTa grammar validation/correction rules
  ├── requirements.txt           ← Python dependencies
  └── Dockerfile                 ← Container for the sidecar

protos/
  └── hyperon_grammar.proto      ← gRPC service definition

src/Ouroboros.Pipeline/
├── MeTTa/Schemas/
│   └── GrammarAtoms.metta       ← Grammar atom type definitions for the engine
├── Pipeline/Grammar/
│   ├── IGrammarValidator.cs     ← Interface for validation service
│   ├── HyperonGrpcGrammarValidator.cs ← gRPC client to Hyperon sidecar
│   ├── DynamicParserFactory.cs  ← ANTLR + Roslyn runtime compilation
│   ├── AdaptiveParserPipeline.cs ← Feedback loop orchestration
│   ├── GrammarEvolutionStep.cs  ← Pipeline step for composition
│   ├── GrammarValidationStep.cs ← Validation pipeline step
│   ├── SandboxedCompilationContext.cs ← Collectible AssemblyLoadContext
│   ├── GrammarAtomConverter.cs  ← .NET ↔ MeTTa atom bridging
│   └── (model records)          ← GrammarIssue, ParseFailureInfo, etc.
```

## Pipeline Flow

```
Description → Ollama → MeTTa Validation → ANTLR Tool → Roslyn → Live Parser
                  ↑                                            |
                  └── Parse Failure → MeTTa Refinement ────────┘
```

## Key Design Decisions

1. **Upstream Hyperon via gRPC** — Full MeTTa feature set (backward chaining, higher-order functions) at ~1-5ms latency per call
2. **Collectible AssemblyLoadContext** — Prevents memory leaks from repeated grammar compilations
3. **Existing Ollama adapters reused** — No new provider code needed
4. **Grammar atoms stored in both spaces** — Local C# AtomSpace for observability, upstream Hyperon for reasoning

## Dependencies Added

- `Antlr4.Runtime.Standard` 4.13.2
- `Microsoft.CodeAnalysis.CSharp` 4.12.0
- `Grpc.Net.Client` 2.67.0
- `Google.Protobuf` 3.29.3
- `Grpc.Tools` 2.67.0

## See Also

- [MeTTa Neuro-Symbolic Architecture](METTA_NEURO_SYMBOLIC_ARCHITECTURE.md)
- [Hyperon Module](hyperon/README.md)
- [Ollama Cloud Integration](OLLAMA_CLOUD_INTEGRATION.md)
