# Dynamic LLM-Backed ANTLR .NET Parser with MeTTa Logic Transfer Objects

## Architecture Overview

This module enables **dynamically generating, validating, and evolving programming language grammars at runtime** using **Logic Transfer Objects (LTOs)** — MeTTa atoms that carry formally verifiable logic over the gRPC wire, not just data.

- **MeTTa Atoms as Formal Specifications** — The LLM generates MeTTa grammar spec atoms (`MkGrammar`, `MkProduction`, `MkTerminal`), NOT raw .g4 text
- **Hyperon Sidecar validates + converts** — Atoms are validated, corrected, and deterministically converted to ANTLR grammars
- **ANTLR4 + Roslyn** — Industry-standard parser generator compiled on the fly
- **Ollama (Local LLMs)** — Generates MeTTa atom specifications from natural language descriptions

### Logic Transfer Objects (LTOs)

Unlike DTOs which carry data, LTOs carry **formally verifiable logic** over the wire. MeTTa atoms serve as the specification language between the LLM and code generation:

```
LLM → MeTTa spec atoms (LTOs) → gRPC wire → Hyperon validates+converts → .g4 → ANTLR+Roslyn → working code
```

## Component Layout

```
tools/hyperon-sidecar/           ← Python gRPC server wrapping upstream Hyperon
  ├── grammar_service.py         ← gRPC service + LTO operations (atoms_to_g4, etc.)
  ├── grammar_atoms.metta        ← Full MeTTa grammar validation/correction rules
  ├── requirements.txt           ← Python dependencies
  └── Dockerfile                 ← Container for the sidecar

protos/
  └── hyperon_grammar.proto      ← gRPC service definition (incl. LTO RPCs)

src/Ouroboros.Pipeline/
├── MeTTa/Schemas/
│   └── GrammarAtoms.metta       ← Grammar atom type definitions + LTO traceability
├── Pipeline/Grammar/
│   ├── IGrammarValidator.cs     ← Interface (incl. AtomsToGrammar, ValidateAtoms, CorrectAtoms)
│   ├── HyperonGrpcGrammarValidator.cs ← gRPC client to Hyperon sidecar
│   ├── DynamicParserFactory.cs  ← ANTLR + Roslyn runtime compilation
│   ├── AdaptiveParserPipeline.cs ← LTO feedback loop orchestration
│   ├── GrammarEvolutionStep.cs  ← Pipeline step for composition
│   ├── GrammarValidationStep.cs ← Validation pipeline step
│   ├── SandboxedCompilationContext.cs ← Collectible AssemblyLoadContext
│   ├── GrammarAtomConverter.cs  ← .NET ↔ MeTTa atom bridging
│   └── (model records)          ← GrammarIssue, ParseFailureInfo, etc.
```

## Pipeline Flow

```
Description → Ollama → MeTTa Atoms (LTOs) → gRPC Wire → Validate → Correct → .g4 → Roslyn → Live Parser
                  ↑                                                                            |
                  └── Parse Failure → MeTTa Atom Refinement ───────────────────────────────────┘
```

## Key Design Decisions

1. **MeTTa atoms ARE the specification** — The LLM generates formal specs, not string patches. The sidecar deterministically converts atoms to .g4
2. **Logic Transfer Objects over the wire** — Atoms carry verifiable logic, not just data. Validation happens at the atom level before .g4 generation
3. **Upstream Hyperon via gRPC** — Full MeTTa feature set (backward chaining, higher-order functions) at ~1-5ms latency per call
4. **Collectible AssemblyLoadContext** — Prevents memory leaks from repeated grammar compilations
5. **Existing Ollama adapters reused** — No new provider code needed
6. **LTO Traceability** — Every compiled grammar is linked back to its generating MeTTa source via `SpecAtoms` relation

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
