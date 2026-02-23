# Ouroboros.Agent Architecture

## Module Structure

The Agent project is organized into logical modules within a single assembly.
A future project split is planned once circular dependencies between modules
are resolved (OrchestratorBase ↔ MetaAI).

### Module Boundary Map

```
Ouroboros.Agent (Facade & Composition)
├── Agent/                          19 files — OrchestratorBase, factories, composers
├── Agent/ConsolidatedMind/         15 files — Society of Mind multiplexer
├── Agent/Dispatch/                  7 files — CQRS command/query dispatch
├── Agent/MetaAI/                  228 files — Self-improving AI orchestration
│   ├── Affect/                     10 files — Emotional valence & priority
│   ├── Evolution/                   3 files — Genetic optimization
│   ├── MetaLearning/                2 files — Meta-learning algorithms
│   ├── SelfImprovement/            44 files — Self-evolution capabilities
│   ├── SelfModel/                  15 files — Agent identity & workspace
│   └── WorldModel/                 15 files — State prediction (MLP/Transformer/GNN)
├── Agent/MeTTa/                     2 files — MeTTa memory bridge
├── Agent/MeTTaAgents/               8 files — MeTTa agent runtime
├── Agent/NeuralSymbolic/           14 files — Neural-symbolic reasoning bridge
├── Agent/Resilience/                1 file  — Resilience patterns
├── Agent/TemporalReasoning/         2 files — Temporal reasoning
├── Agent/TheoryOfMind/              8 files — Agent modeling & belief tracking
└── Agent/WorldModel/                3 files — High-level world model
```

### Planned Split (Future)

| Target Project              | Modules                                     | Blocker                          |
|-----------------------------|---------------------------------------------|----------------------------------|
| Ouroboros.Agent.Abstractions| OrchestratorBase, IComposable, interfaces   | Extract base types first         |
| Ouroboros.Agent.MetaAI      | MetaAI/**, Evolution, MetaLearning          | Depends on Abstractions          |
| Ouroboros.Agent.MeTTa       | MeTTa/, MeTTaAgents/, NeuralSymbolic/       | Depends on Abstractions          |
| Ouroboros.Agent.Orchestration| ConsolidatedMind/, Dispatch/, TheoryOfMind/ | Depends on MetaAI + MeTTa        |
| Ouroboros.Agent             | Root facade + remaining support modules     | Re-exports from sub-projects     |

### Circular Dependency: OrchestratorBase ↔ MetaAI

Root → MetaAI: `OrchestratorBase.cs`, `SmartModelOrchestrator.cs`, `OrchestratorArrows.cs`
MetaAI → Root: `OuroborosOrchestrator : OrchestratorBase<string, OuroborosResult>`

Resolution: extract `OrchestratorBase<TIn, TOut>` and related interfaces into
`Ouroboros.Agent.Abstractions` (or Foundation) before splitting.
