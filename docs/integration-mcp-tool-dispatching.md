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

When a tool call fails, the **Evolutionary Retry Policy** mutates the request
(format hints, format switching, tool simplification, temperature adjustment)
instead of retrying the identical failing request.

## Quick Start

```csharp
// 1. Register tools
var registry = new ToolRegistry();
registry.Register(new MyCustomTool());

// 2. Create Ollama adapter with tool support
var adapter = new OllamaToolChatAdapter(
    endpoint: "https://ollama-cloud.example.com",
    model: "mistral:latest",
    tools: registry,
    parser: new McpToolCallParser(),
    retryPolicy: EvolutionaryRetryPolicyBuilder.ForToolCallsWithDefaults().Build(),
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

// Evolutionary retry with all default mutation strategies
services.AddSingleton(sp =>
    EvolutionaryRetryPolicyBuilder.ForToolCallsWithDefaults()
        .WithMaxGenerations(5)
        .Build());

// Ollama with MCP tool support
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

Tool call intents can be tracked and reasoned about via MeTTa atoms:

```csharp
// Convert tool calls to MeTTa atoms
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
// (solve Text TestResult) → (chain (chain summarize_tool generate_code_tool) run_tests_tool)
string chain = McpToolCallAtomConverter.ToChainAtom(intents);
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
      "Strategies": ["format-hint", "format-switch", "tool-simplification", "temperature"]
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
  │   │   ├─ FormatHintMutation (priority 10)
  │   │   ├─ FormatSwitchMutation (priority 20)
  │   │   ├─ ToolSimplificationMutation (priority 30)
  │   │   ├─ TemperatureMutation (priority 40)
  │   │   └─ [Custom app mutations]
  │   ├─ OllamaToolChatAdapter
  │   └─ SK Kernel
  │
  ├─ Request Flow
  │   User → App Controller → SK Kernel
  │     → OllamaToolChatAdapter
  │       → Try native /api/chat tools
  │       → Fallback: ANTLR parser (XML/JSON/Bracket/Markdown)
  │       → EvolutionaryRetry on failure (mutate → retry)
  │       → ToolRegistry.InvokeAsync()
  │     → Response with tool results
  │
  └─ Observability
      ├─ MeTTa AtomSpace: ToolCallAtoms + RetryMutation history
      ├─ NeuralPathway: weight/health tracking
      └─ AfterInvoke hooks: metrics + event bus
```

## Key Types

| Type | Namespace | Purpose |
|------|-----------|---------|
| `OllamaToolChatAdapter` | `Ouroboros.Providers` | Ollama /api/chat with native tools + fallback |
| `McpToolCallParser` | `Ouroboros.Providers` | Multi-format tool call extraction from LLM text |
| `ToolCallIntent` | `Ouroboros.Providers` | Parsed tool call (name, args, format) |
| `McpToolCallAtomConverter` | `Ouroboros.Pipeline` | .NET ↔ MeTTa atom conversion |
| `EvolutionaryRetryPolicy<T>` | `Ouroboros.Providers.Resilience` | Mutation-based retry |
| `IMutationStrategy<T>` | `Ouroboros.Providers.Resilience` | Strategy pattern for mutations |
| `ToolCallContext` | `Ouroboros.Providers.Resilience` | Mutable execution context |
| `KernelFactory` | `Ouroboros.SemanticKernel` | SK Kernel builder with Ollama overload |

## MeTTa Schema Files

| File | Purpose |
|------|---------|
| `ToolCallAtoms.metta` | Tool call intents, MCP protocol, permissions, retry tracking |
| `ToolSignatures.metta` | Tool type signatures + backward chaining |
| `ActionTypes.metta` | Permission guard rails (SafeContext) |
| `GrammarAtoms.metta` | Grammar lifecycle (for reference) |
