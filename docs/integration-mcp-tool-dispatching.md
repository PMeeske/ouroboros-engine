# MCP Tool Dispatching Integration Guide

This document describes how to integrate the MeTTa-atom-based MCP tool dispatching system
with the application layer (ouroboros-app).

## Overview

The system provides Ollama models with tool-calling capabilities through three mechanisms:

| Priority | Method | When Used |
|----------|--------|-----------|
| 1 | **Native Ollama Chat Tools** | Model supports `/api/chat` with tools |
| 2 | **SK Auto-Function-Calling** | Via `IChatClient` + MEAI pipeline |
| 3 | **ANTLR McpToolCallParser** | LLM outputs XML/JSON/Bracket tool calls in text |
| 4 | **MeTTa Backward Chaining** | `(solve InputType OutputType)` for tool discovery |
| 5 | **Regex Fallback** | Legacy `[TOOL:name args]` pattern |

When a tool call fails, the **Evolutionary Retry Policy** uses a genetic algorithm
with fitness-evaluated chromosomes to select and evolve mutation strategies, rather than
simply retrying the identical failing request.

## Quick Start

```csharp
// 1. Register tools
var registry = new ToolRegistry();
registry.Register(new MyCustomTool());

// 2. Create Ollama adapter with tool support (includes Polly resilience + cost tracking)
var adapter = new OllamaToolChatAdapter(
    endpoint: "https://ollama-cloud.example.com",
    model: "mistral:latest",
    tools: registry,
    parser: new McpToolCallParser(),
    retryPolicy: EvolutionaryRetryPolicyBuilder.ForToolCallsWithEvolution().Build(),
    apiKey: "your-api-key");

// 3. Wire into Semantic Kernel
var kernel = KernelFactory.CreateKernel(adapter, registry);

// 4. Invoke with auto-function-calling
var settings = new PromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};
var result = await kernel.InvokePromptAsync("What's the weather in Berlin?", settings);
```

## DI Registration

```csharp
services.AddSingleton<McpToolCallParser>();
services.AddSingleton<ToolRegistry>(sp =>
{
    var registry = new ToolRegistry();
    registry.Register(new WeatherTool());
    registry.Register(new SearchTool());
    return registry;
});

// Evolutionary retry with GA-based chromosome evolution
services.AddSingleton(sp =>
    EvolutionaryRetryPolicyBuilder.ForToolCallsWithEvolution()
        .WithMaxGenerations(5)
        .Build());

// Ollama with MCP tool support + Polly resilience + NeuralPathway + CostTracker
services.AddSingleton<OllamaToolChatAdapter>(sp => new OllamaToolChatAdapter(
    endpoint: config["Ollama:Endpoint"]!,
    model: config["Ollama:Model"]!,
    tools: sp.GetRequiredService<ToolRegistry>(),
    parser: sp.GetRequiredService<McpToolCallParser>(),
    retryPolicy: sp.GetRequiredService<EvolutionaryRetryPolicy<ToolCallContext>>(),
    apiKey: config["Ollama:ApiKey"]));

// SK Kernel
services.AddSemanticKernel();
```

## Evolutionary Retry with Genetic Algorithm

The retry policy uses real genetic algorithm infrastructure (analogous to
`PlanStrategyChromosome` from `Ouroboros.Agent.MetaAI.Evolution`):

### Chromosome-based Strategy Selection

```csharp
// Genes encode mutation strategy parameters
var chromosome = ToolCallMutationChromosome.CreateDefault();
// Genes: FormatHintAggression=0.50, TemperatureAmplitude=0.30,
//         SimplificationRate=0.40, FormatSwitchPreference=0.50,
//         LlmVariatorWeight=0.20

// The policy evolves the chromosome based on retry outcomes
var policy = EvolutionaryRetryPolicyBuilder.ForToolCalls()
    .WithStrategy(new FormatHintMutation())
    .WithStrategy(new FormatSwitchMutation())
    .WithStrategy(new ToolSimplificationMutation())
    .WithStrategy(new TemperatureMutation())
    .WithStrategy(new LlmVariatorMutation(variatorModel)) // LLM-based prompt rephrasing
    .WithChromosome(chromosome)
    .WithFitnessFunction(new ToolCallMutationFitness(
        successWeight: 0.5, costWeight: 0.2, speedWeight: 0.3))
    .WithMaxGenerations(7)
    .Build();

// After each execution, the chromosome evolves via crossover + mutation
policy.OnChromosomeEvolved += (_, args) =>
{
    Console.WriteLine($"Fitness: {args.Fitness:F3}, Generations: {args.GenerationsUsed}");
};
```

### LLM-based Variator

The `LlmVariatorMutation` uses an LLM model to intelligently rephrase prompts:

```csharp
// Use a small, fast model for prompt variation
var variatorModel = new OllamaCloudChatModel(
    endpoint, apiKey, "mistral:7b");

var llmVariator = new LlmVariatorMutation(
    variatorModel,
    timeout: TimeSpan.FromSeconds(15));
```

## Custom Mutation Strategies

Register domain-specific mutation strategies for the evolutionary retry:

```csharp
public sealed class DomainRephraseMutation : IMutationStrategy<ToolCallContext>
{
    public string Name => "domain-rephrase";
    public int Priority => 15; // between format-hint (10) and format-switch (20)

    public bool CanMutate(ToolCallContext context, Exception lastError)
        => !context.Prompt.Contains("Using our internal API");

    public ToolCallContext Mutate(ToolCallContext context, int generation)
    {
        context.Prompt = $"Using our internal API tools: {context.Prompt}";
        context.Generation = generation;
        return context;
    }
}

// Registration
var policy = EvolutionaryRetryPolicyBuilder.ForToolCalls()
    .WithStrategy(new FormatHintMutation())
    .WithStrategy(new DomainRephraseMutation()) // custom
    .WithStrategy(new FormatSwitchMutation())
    .WithStrategy(new ToolSimplificationMutation())
    .WithStrategy(new TemperatureMutation())
    .WithMaxGenerations(7)
    .Build();
```

## MeTTa AtomSpace Integration

Tool call intents can be tracked and reasoned about via MeTTa atoms.
Two modes: string generation (for logging) and direct HyperonMeTTaEngine recording:

```csharp
// ── String generation (for display/logging) ──
var parser = new McpToolCallParser();
var intents = parser.Parse(llmOutput);
string atoms = McpToolCallAtomConverter.ToAtoms(intents);

// Permission check
string check = McpToolCallAtomConverter.ToPermissionCheck(intents[0], "ReadOnly");
// → (ToolCallAllowed (MkToolCall "search" "{}") ReadOnly)

// Track evolutionary retry mutations
string mutation = McpToolCallAtomConverter.ToRetryMutationAtom(
    "attempt-1", "FormatHint", 2, "MutationEvolved");

// Tool chain discovery via backward chaining (ToolSignatures.metta)
string chain = McpToolCallAtomConverter.ToChainAtom(intents);

// ── Direct AtomSpace recording (for queries + backward chaining) ──
var engine = new HyperonMeTTaEngine();

// Record tool calls directly to AtomSpace
McpToolCallAtomConverter.RecordToolCall(engine, intents[0]);
McpToolCallAtomConverter.RecordToolChain(engine, intents);
McpToolCallAtomConverter.RecordToolResult(engine, "search", "42 results found", isError: false);

// Record evolutionary retry events
McpToolCallAtomConverter.RecordRetryMutation(engine, "attempt-1", "format-hint", 1, "MutationEvolved");
McpToolCallAtomConverter.RecordFitnessEvaluation(engine, "attempt-1", fitness: 0.85, succeeded: true, generationsUsed: 2);

// Record permission checks
McpToolCallAtomConverter.RecordPermissionCheck(engine, intents[0], "ReadOnly", allowed: true);
```

## Reactive Tool Execution Events

`ToolAwareChatModel` exposes an IObservable event stream:

```csharp
var toolAware = new ToolAwareChatModel(llm, registry, parser);

// Subscribe to tool execution events
toolAware.ToolExecutions.Subscribe(evt =>
{
    Console.WriteLine($"Tool: {evt.ToolName}, Success: {evt.Success}, Elapsed: {evt.Elapsed}");

    // Record to AtomSpace
    McpToolCallAtomConverter.RecordToolResult(engine, evt.ToolName, evt.Output, !evt.Success);
});
```

## Configuration

```json
{
  "Ollama": {
    "Endpoint": "https://ollama-cloud.example.com",
    "ApiKey": "...",
    "Model": "mistral:latest",
    "EvolutionaryRetry": {
      "MaxGenerations": 5,
      "Strategies": ["format-hint", "format-switch", "tool-simplification", "temperature", "llm-variator"]
    }
  },
  "McpServer": {
    "ToolFilter": ["next_node", "semantic_search", "run_tests"]
  }
}
```

## Architecture

```
ouroboros-app
  │
  ├─ DI Container
  │   ├─ ToolRegistry (domain tools)
  │   ├─ McpToolCallParser (multi-format)
  │   ├─ EvolutionaryRetryPolicy<ToolCallContext>
  │   │   ├─ ToolCallMutationChromosome (GA genes)
  │   │   ├─ ToolCallMutationFitness (weighted evaluation)
  │   │   ├─ FormatHintMutation (priority 10)
  │   │   ├─ FormatSwitchMutation (priority 20)
  │   │   ├─ LlmVariatorMutation (priority 25, LLM-based)
  │   │   ├─ ToolSimplificationMutation (priority 30)
  │   │   ├─ TemperatureMutation (priority 40)
  │   │   └─ [Custom app mutations]
  │   ├─ OllamaToolChatAdapter
  │   │   ├─ Polly AsyncPolicyWrap (retry + circuit breaker)
  │   │   ├─ NeuralPathway (health tracking)
  │   │   └─ LlmCostTracker (per-request cost metrics)
  │   └─ SK Kernel
  │
  ├─ Request Flow
  │   User → App Controller → SK Kernel
  │     → OllamaToolChatAdapter
  │       → Polly resilience (retry + circuit breaker)
  │       → Try native /api/chat tools
  │       → Fallback: ANTLR parser (XML/JSON/Bracket/Markdown)
  │       → EvolutionaryRetry on failure (chromosome-driven mutation → fitness eval → evolve)
  │       → ToolRegistry.InvokeAsync()
  │     → Response with tool results
  │
  └─ Observability
      ├─ MeTTa AtomSpace: ToolCallAtoms + RetryMutation + FitnessResult history
      ├─ NeuralPathway: activation/inhibition weight tracking
      ├─ LlmCostTracker: per-request token + cost metrics
      ├─ IObservable<ToolExecutionEvent>: reactive event stream
      └─ AfterInvoke hooks: metrics + event bus
```

## Key Types

| Type | Namespace | Purpose |
|------|-----------|---------|
| `OllamaToolChatAdapter` | `Ouroboros.Providers` | Ollama /api/chat with native tools + Polly + NeuralPathway + CostTracker |
| `McpToolCallParser` | `Ouroboros.Providers` | Multi-format tool call extraction from LLM text |
| `ToolCallIntent` | `Ouroboros.Providers` | Parsed tool call (name, args, format) |
| `McpToolCallAtomConverter` | `Ouroboros.Pipeline` | .NET ↔ MeTTa atom conversion + HyperonMeTTaEngine recording |
| `EvolutionaryRetryPolicy<T>` | `Ouroboros.Providers.Resilience` | GA-based mutation retry with chromosome evolution |
| `ToolCallMutationChromosome` | `Ouroboros.Providers.Resilience` | Evolvable gene set for mutation strategy parameters |
| `ToolCallMutationGene` | `Ouroboros.Providers.Resilience` | Single evolvable parameter (analogous to PlanStrategyGene) |
| `ToolCallMutationFitness` | `Ouroboros.Providers.Resilience` | Fitness evaluation (success rate, cost, speed) |
| `LlmVariatorMutation` | `Ouroboros.Providers.Resilience` | LLM-based prompt rephrasing mutation |
| `IMutationStrategy<T>` | `Ouroboros.Providers.Resilience` | Strategy pattern for mutations |
| `ToolCallContext` | `Ouroboros.Providers.Resilience` | Mutable execution context |
| `ToolExecutionEvent` | `Ouroboros.Providers` | Reactive event for tool executions |
| `KernelFactory` | `Ouroboros.SemanticKernel` | SK Kernel builder with Ollama overload |

## MeTTa Schema Files

| File | Purpose |
|------|---------|
| `ToolCallAtoms.metta` | Tool call intents, MCP protocol, permissions, retry tracking, fitness |
| `ToolSignatures.metta` | Tool type signatures + backward chaining |
| `ActionTypes.metta` | Permission guard rails (SafeContext) |
| `GrammarAtoms.metta` | Grammar lifecycle (for reference) |
