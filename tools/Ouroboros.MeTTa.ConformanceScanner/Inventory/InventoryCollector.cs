// <copyright file="InventoryCollector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reflection;
using Ouroboros.Core.Hyperon;
using AppEngine = Ouroboros.Application.Hyperon.HyperonMeTTaEngine;
using ToolsEngine = Ouroboros.Tools.MeTTa.HyperonMeTTaEngine;

namespace Ouroboros.MeTTa.ConformanceScanner.Inventory;

/// <summary>
/// Collects engine inventory from standard registry, tools engine, application factory, and late-bound catalogue.
/// </summary>
public sealed class InventoryCollector
{
    private static readonly IReadOnlyList<(string Name, string SourceRef)> LateBoundCatalogue = new[]
    {
        ("llm-infer", "HyperonMeTTaEngine.NeuralOps.cs:71"),
        ("llm-code", "HyperonMeTTaEngine.NeuralOps.cs:75"),
        ("llm-reason", "HyperonMeTTaEngine.NeuralOps.cs:79"),
        ("llm-summarize", "HyperonMeTTaEngine.NeuralOps.cs:83"),
        ("llm-tools", "HyperonMeTTaEngine.NeuralOps.cs:87"),
        ("llm-route", "HyperonMeTTaEngine.NeuralOps.cs:91"),
        ("llm-available", "HyperonMeTTaEngine.NeuralOps.cs:95"),
    };

    /// <summary>
    /// Collects inventory. Late-bound ops are omitted unless <paramref name="includeLateBound"/> is true.
    /// </summary>
    public EngineInventory Collect(bool includeLateBound = false)
    {
        var standard = this.CollectStandard();
        var tools = this.CollectToolsEngine(standard);
        var app = this.CollectApplicationLayer(standard);
        var late = includeLateBound ? this.CollectLateBoundCatalogue() : Array.Empty<RegisteredOperation>();
        return new EngineInventory(standard, tools, app, late);
    }

    public IReadOnlyList<RegisteredOperation> CollectStandard()
    {
        var registry = GroundedRegistry.CreateStandard();
        return registry.RegisteredNames
            .OrderBy(static n => n, StringComparer.Ordinal)
            .Select(static n => new RegisteredOperation(n, null, "standard", null))
            .ToList();
    }

    public IReadOnlyList<RegisteredOperation> CollectToolsEngine(IReadOnlyList<RegisteredOperation> standardBaseline)
    {
        using var engine = new ToolsEngine();
        var standardNames = standardBaseline.Select(static op => op.Name).ToHashSet(StringComparer.Ordinal);

        return engine.GroundedOps.RegisteredNames
            .Where(n => !standardNames.Contains(n))
            .OrderBy(static n => n, StringComparer.Ordinal)
            .Select(static n => new RegisteredOperation(n, null, "tools-engine", "Registered by Ouroboros.Tools.MeTTa.HyperonMeTTaEngine ctor"))
            .ToList();
    }

    public IReadOnlyList<RegisteredOperation> CollectApplicationLayer(IReadOnlyList<RegisteredOperation> standardBaseline)
    {
        var method = typeof(AppEngine).GetMethod(
            "CreateOuroborosGroundedOps",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method is null)
        {
            return
            [
                new RegisteredOperation(
                    "<application-layer-collection-failed>",
                    null,
                    "ouroboros-extension",
                    "CreateOuroborosGroundedOps not found via reflection — check Application.Hyperon.HyperonMeTTaEngine"),
            ];
        }

        if (method.Invoke(null, null) is not GroundedRegistry registry)
        {
            return
            [
                new RegisteredOperation(
                    "<application-layer-collection-failed>",
                    null,
                    "ouroboros-extension",
                    "CreateOuroborosGroundedOps returned null or non-registry value"),
            ];
        }

        var standardNames = standardBaseline.Select(static op => op.Name).ToHashSet(StringComparer.Ordinal);

        return registry.RegisteredNames
            .Where(n => !standardNames.Contains(n))
            .OrderBy(static n => n, StringComparer.Ordinal)
            .Select(static n => new RegisteredOperation(n, null, "ouroboros-extension", "Registered by Application.Hyperon.HyperonMeTTaEngine.CreateOuroborosGroundedOps"))
            .ToList();
    }

    public IReadOnlyList<RegisteredOperation> CollectLateBoundCatalogue()
    {
        return LateBoundCatalogue
            .Select(static entry => new RegisteredOperation(
                entry.Name,
                null,
                "late-bound",
                $"Late-bound by Application.Hyperon.HyperonMeTTaEngine.BindNeuralModels — {entry.SourceRef}"))
            .ToList();
    }
}
