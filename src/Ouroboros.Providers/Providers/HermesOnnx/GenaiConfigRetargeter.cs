// <copyright file="GenaiConfigRetargeter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.HermesOnnx;

/// <summary>
/// Idempotent provider rewriter for ORT-GenAI <c>genai_config.json</c>. The Hermes-4 INT4
/// checkpoint ships configured for CUDA EP, which doesn't exist on the RX 9060 XT, so the
/// file must be retargeted before <c>new Model(modelPath)</c> can succeed. Path A from
/// <c>.planning/phases/263-hermes-onnx-extra-mode/263-RESEARCH.md</c> Section 7.
/// </summary>
internal static class GenaiConfigRetargeter
{
    /// <summary>
    /// Rewrites <c>provider_options</c> entries from <c>cuda</c> to the requested execution
    /// provider. Mirrors the cuda layout (<c>{ "&lt;ep&gt;": { ... } }</c>) — ORT-GenAI's parser
    /// rejects <c>{ "name": "DML" }</c> with <c>Unknown value 'name'</c>. CPU EP is encoded as
    /// an empty <c>provider_options</c> array (the only shape ORT-GenAI accepts for the
    /// default CPU provider). Idempotent — already-targeted configs are left untouched.
    /// </summary>
    /// <param name="modelDir">Absolute path to the model directory.</param>
    /// <param name="executionProvider">Target EP: <c>dml</c> (default) or <c>cpu</c>. Anything
    /// else falls back to <c>dml</c> with a warning.</param>
    /// <param name="logger">Optional logger for retarget telemetry.</param>
    public static void EnsureProvider(string modelDir, string executionProvider = "dml", ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDir);

        string ep = (executionProvider ?? "dml").Trim().ToLowerInvariant();
        if (ep != "dml" && ep != "cpu")
        {
            logger?.LogWarning("[HermesOnnx] Unknown execution provider '{Ep}'; falling back to 'dml'", executionProvider);
            ep = "dml";
        }

        string configPath = Path.Combine(modelDir, "genai_config.json");
        if (!File.Exists(configPath))
        {
            logger?.LogWarning("[HermesOnnx] genai_config.json not found at {Path}; retarget skipped", configPath);
            return;
        }

        JsonNode? root = JsonNode.Parse(File.ReadAllText(configPath));
        JsonArray? providerOptions = root?["model"]?["decoder"]?["session_options"]?["provider_options"]?.AsArray();
        if (root is null || providerOptions is null)
        {
            logger?.LogDebug("[HermesOnnx] genai_config.json missing or has no provider_options; retarget skipped");
            return;
        }



        bool changed = ep switch
        {
            "cpu" => RetargetToCpu(providerOptions),
            _ => RetargetToDml(providerOptions),
        };

        if (changed)
        {
            string backupPath = configPath + ".cuda.bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(configPath, backupPath, overwrite: false);
            }

            JsonSerializerOptions writeOptions = new() { WriteIndented = true };
            File.WriteAllText(configPath, root.ToJsonString(writeOptions));
            logger?.LogInformation("[HermesOnnx] Retargeted genai_config.json -> {Ep} (backup: {Backup})", ep, backupPath);
        }
        else
        {
            logger?.LogDebug("[HermesOnnx] genai_config.json already targets {Ep}; no rewrite needed", ep);
        }
    }

    /// <summary>
    /// Backwards-compatible alias for callers that don't yet pass <c>executionProvider</c>.
    /// </summary>
    public static void EnsureDirectMlProvider(string modelDir, ILogger? logger = null) =>
        EnsureProvider(modelDir, "dml", logger);

    private static bool RetargetToDml(JsonArray providerOptions)
    {
        bool changed = false;
        for (int i = 0; i < providerOptions.Count; i++)
        {
            JsonObject? entry = providerOptions[i] as JsonObject;
            if (entry is null)
            {
                continue;
            }

            // Already-correct dml form: { "dml": { ... } } — leave it.
            if (entry.ContainsKey("dml"))
            {
                continue;
            }

            // CUDA form: { "cuda": { ... } } — replace with sibling-keyed dml form, NOT
            // { "name": "DML" }. ORT-GenAI 0.13.x rejects the name-keyed shape with
            // "Unknown value 'name' at line N index M".
            if (entry.ContainsKey("cuda"))
            {
                providerOptions[i] = new JsonObject { ["dml"] = new JsonObject() };
                changed = true;
                continue;
            }

            // Legacy buggy shape we may have written ourselves: { "name": "DML" } — heal it.
            string? name = entry["name"]?.GetValue<string>();
            if (name is not null && (string.Equals(name, "DML", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(name, "cuda", StringComparison.OrdinalIgnoreCase)))
            {
                providerOptions[i] = new JsonObject { ["dml"] = new JsonObject() };
                changed = true;
            }
        }

        return changed;
    }

    private static bool RetargetToCpu(JsonArray providerOptions)
    {
        // CPU EP for ORT-GenAI is encoded as the absence of any provider entries. Passing
        // { "cpu": {} } yields "Unknown provider name 'cpu'". Just clear the array.
        if (providerOptions.Count == 0)
        {
            return false;
        }

        providerOptions.Clear();
        return true;
    }
}
