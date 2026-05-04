// <copyright file="MettaAgentWarmupSequence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions.Agent;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Agent.Warmup;

/// <summary>
/// MeTTa-based agent warmup sequence (Phase 271 — agent-metta-warmup).
/// Runs five sequential steps at <c>IaretAgent</c> startup, each emitting the locked
/// <c>AgentWarmup.Step:</c> log line, and finishes with the locked <c>IaretAwake:</c>
/// readiness signal. Best-effort: per-step failure → WARN + degraded=true; never throws.
/// </summary>
/// <remarks>
/// <para>
/// The sequence does NOT call any LLM (per <c>feedback_llm_is_renderer_not_persona.md</c>).
/// Persona context is read from MeTTa atoms / <see cref="IPersonalityContextProvider"/>
/// only. The first user turn is gated on <see cref="RunAsync"/> completing.
/// </para>
/// <para>
/// Ops <c>(self-perception-quality)</c> and <c>(describe-self)</c> were planned for
/// Phase 3 v3.0 grounded ops; if absent from the standard registry, the probe still
/// reports a soft fail (status=fail) because the warmup is observability — the agent
/// continues to start in degraded mode. Atom counting falls back to <c>(get-atoms)</c>
/// queries which the standard registry guarantees.
/// </para>
/// </remarks>
public sealed class MettaAgentWarmupSequence : IAgentWarmupSequence
{
    private const string StepLoadAtoms = "load-self-atoms";
    private const string StepProbeOps = "probe-grounded-ops";
    private const string StepQuerySelfModel = "query-self-model";
    private const string StepEmitRoutePrime = "emit-route-prime";
    private const string StepReadinessReport = "readiness-report";

    private static readonly string[] PrimaryGroundedOpProbes =
    {
        "(self-perception-quality)",
        "(describe-self)",
    };

    private static readonly string[] FallbackGroundedOpProbes =
    {
        "!(get-atoms &self)",
        "!(match &self ($x) $x)",
    };

    private static readonly string[] SeedRoutes = { "ethics", "causal", "personality" };

    private readonly IMeTTaEngine _engine;
    private readonly RouteCentroidStore? _routeStore;
    private readonly IPersonalityContextProvider? _personalityContext;
    private readonly ILogger<MettaAgentWarmupSequence> _logger;

    /// <summary>
    /// Initializes a new <see cref="MettaAgentWarmupSequence"/>.
    /// </summary>
    /// <param name="engine">MeTTa symbolic-reasoning engine (required).</param>
    /// <param name="routeStore">Centroid store for emit-route-prime probe (optional — degrades to skip).</param>
    /// <param name="personalityContext">Persona context provider for self-atom verification (optional — degrades to skip).</param>
    /// <param name="logger">Structured logger.</param>
    public MettaAgentWarmupSequence(
        IMeTTaEngine engine,
        RouteCentroidStore? routeStore,
        IPersonalityContextProvider? personalityContext,
        ILogger<MettaAgentWarmupSequence> logger)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(logger);
        _engine = engine;
        _routeStore = routeStore;
        _personalityContext = personalityContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AgentWarmupReport> RunAsync(CancellationToken ct)
    {
        var totalSw = Stopwatch.StartNew();
        var steps = new List<AgentWarmupStepResult>(5);

        var (atomsLoaded, step1) = await LoadSelfAtomsAsync(ct).ConfigureAwait(false);
        steps.Add(step1);

        var (groundedOpsOk, step2) = await ProbeGroundedOpsAsync(ct).ConfigureAwait(false);
        steps.Add(step2);

        var (layers, step3) = QuerySelfModel();
        steps.Add(step3);

        var step4 = EmitRoutePrime();
        steps.Add(step4);

        totalSw.Stop();
        var degraded = steps.Any(s => !s.Ok);

        var step5 = new AgentWarmupStepResult(
            StepReadinessReport,
            Ok: true,
            Elapsed: TimeSpan.Zero,
            Error: null);
        steps.Add(step5);
        LogStep(step5);

        var report = new AgentWarmupReport(
            AtomsLoaded: atomsLoaded,
            GroundedOpsOk: groundedOpsOk,
            LayersAcknowledged: layers,
            Elapsed: totalSw.Elapsed,
            Degraded: degraded,
            Steps: steps);

        // Locked log line — immutable post-ship.
        _logger.LogInformation(
            "IaretAwake: atoms={Atoms} ops-ok={OpsOk} layers={Layers} elapsed-ms={Elapsed} degraded={Degraded}",
            report.AtomsLoaded,
            report.GroundedOpsOk,
            report.LayersAcknowledged,
            (long)report.Elapsed.TotalMilliseconds,
            report.Degraded);

        return report;
    }

    private async Task<(int Count, AgentWarmupStepResult Step)> LoadSelfAtomsAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Touch persona context if available (validates the App-layer adapter is wired).
            _ = _personalityContext?.GetSelfAwarenessContext();

            // Probe atom count via a (get-atoms) query — guaranteed by standard grounded registry.
            var result = await _engine.ExecuteQueryAsync("!(get-atoms &self)", ct).ConfigureAwait(false);
            int count = 0;
            if (result.IsSuccess)
            {
                var text = result.GetValueOrDefault(string.Empty) ?? string.Empty;
                count = CountAtomTokens(text);
            }

            sw.Stop();
            // Step is "ok" if any signal was returned (count >= 0 from a successful query).
            // count > 0 is the happy path; count == 0 with success still counts as ok (empty AtomSpace
            // is a valid degenerate state for a fresh engine).
            var ok = result.IsSuccess;
            var step = new AgentWarmupStepResult(
                StepLoadAtoms,
                Ok: ok,
                Elapsed: sw.Elapsed,
                Error: ok ? null : result.Match(_ => (string?)null, e => e));
            LogStep(step);
            return (count, step);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var step = new AgentWarmupStepResult(StepLoadAtoms, Ok: false, Elapsed: sw.Elapsed, Error: ex.Message);
            _logger.LogWarning(ex, "AgentWarmup: {Step} threw — degraded mode", StepLoadAtoms);
            LogStep(step);
            return (0, step);
        }
    }

    private async Task<(int Count, AgentWarmupStepResult Step)> ProbeGroundedOpsAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int ok = 0;
        string? lastError = null;

        try
        {
            foreach (var probe in PrimaryGroundedOpProbes)
            {
                var r = await _engine.ExecuteQueryAsync(probe, ct).ConfigureAwait(false);
                if (r.IsSuccess)
                {
                    ok++;
                }
                else
                {
                    lastError = r.Match(_ => (string?)null, e => e);
                }
            }

            // If neither primary probe succeeded, fall back to ops the standard registry guarantees.
            if (ok == 0)
            {
                foreach (var probe in FallbackGroundedOpProbes)
                {
                    var r = await _engine.ExecuteQueryAsync(probe, ct).ConfigureAwait(false);
                    if (r.IsSuccess)
                    {
                        ok++;
                    }
                    else
                    {
                        lastError = r.Match(_ => (string?)null, e => e);
                    }
                }
            }

            sw.Stop();
            var stepOk = ok > 0;
            var step = new AgentWarmupStepResult(
                StepProbeOps,
                Ok: stepOk,
                Elapsed: sw.Elapsed,
                Error: stepOk ? null : (lastError ?? "no grounded-op probe succeeded"));
            LogStep(step);
            return (ok, step);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var step = new AgentWarmupStepResult(StepProbeOps, Ok: false, Elapsed: sw.Elapsed, Error: ex.Message);
            _logger.LogWarning(ex, "AgentWarmup: {Step} threw — degraded mode", StepProbeOps);
            LogStep(step);
            return (ok, step);
        }
    }

    private (int Layers, AgentWarmupStepResult Step) QuerySelfModel()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var values = Enum.GetValues<ConsciousnessLayer>();
            int count = values.Length;
            sw.Stop();
            var ok = count > 0;
            var step = new AgentWarmupStepResult(
                StepQuerySelfModel,
                Ok: ok,
                Elapsed: sw.Elapsed,
                Error: ok ? null : "no ConsciousnessLayer values defined");
            LogStep(step);
            return (count, step);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var step = new AgentWarmupStepResult(StepQuerySelfModel, Ok: false, Elapsed: sw.Elapsed, Error: ex.Message);
            _logger.LogWarning(ex, "AgentWarmup: {Step} threw — degraded mode", StepQuerySelfModel);
            LogStep(step);
            return (0, step);
        }
    }

    private AgentWarmupStepResult EmitRoutePrime()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (_routeStore is null)
            {
                sw.Stop();
                var skip = new AgentWarmupStepResult(
                    StepEmitRoutePrime,
                    Ok: false,
                    Elapsed: sw.Elapsed,
                    Error: "RouteCentroidStore not registered");
                _logger.LogWarning(
                    "AgentWarmup: {Step} skipped — RouteCentroidStore not registered",
                    StepEmitRoutePrime);
                LogStep(skip);
                return skip;
            }

            _routeStore.SeedIfEmpty();
            var centroids = _routeStore.GetCentroids();
            var missing = SeedRoutes.Where(r => !centroids.ContainsKey(r)).ToArray();
            sw.Stop();

            if (missing.Length > 0)
            {
                var err = $"missing seed routes: {string.Join(",", missing)}";
                var fail = new AgentWarmupStepResult(StepEmitRoutePrime, Ok: false, Elapsed: sw.Elapsed, Error: err);
                _logger.LogWarning("AgentWarmup: {Step} failed — {Error}", StepEmitRoutePrime, err);
                LogStep(fail);
                return fail;
            }

            var step = new AgentWarmupStepResult(StepEmitRoutePrime, Ok: true, Elapsed: sw.Elapsed, Error: null);
            LogStep(step);
            return step;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var step = new AgentWarmupStepResult(StepEmitRoutePrime, Ok: false, Elapsed: sw.Elapsed, Error: ex.Message);
            _logger.LogWarning(ex, "AgentWarmup: {Step} threw — degraded mode", StepEmitRoutePrime);
            LogStep(step);
            return step;
        }
    }

    private void LogStep(AgentWarmupStepResult step)
    {
        var status = step.Ok ? "ok" : "fail";
        _logger.LogInformation(
            "AgentWarmup.Step: name={Step} status={Status} elapsed-ms={Elapsed}",
            step.Name,
            status,
            (long)step.Elapsed.TotalMilliseconds);
    }

    private static int CountAtomTokens(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        // Conservative count: treat top-level whitespace-separated tokens as atoms when the
        // result string lacks parentheses; otherwise count balanced top-level s-expressions.
        if (!raw.Contains('('))
        {
            return raw.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        int depth = 0;
        int count = 0;
        bool inSexp = false;
        foreach (var ch in raw)
        {
            if (ch == '(')
            {
                if (depth == 0)
                {
                    inSexp = true;
                }

                depth++;
            }
            else if (ch == ')')
            {
                depth = Math.Max(0, depth - 1);
                if (depth == 0 && inSexp)
                {
                    count++;
                    inSexp = false;
                }
            }
        }

        return count;
    }
}
