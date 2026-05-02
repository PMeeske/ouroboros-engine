// <copyright file="GenaiConfigRetargeter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.HermesOnnx;

/// <summary>
/// Idempotent CUDA-&gt;DirectML provider rewriter for ORT-GenAI <c>genai_config.json</c>.
/// Hermes-4 INT4 model ships configured for CUDA EP; the RX 9060 XT has no CUDA EP,
/// so the file must be retargeted to <c>DML</c> before <c>new Model(modelPath)</c>
/// can succeed. Path A from <c>.planning/phases/263-hermes-onnx-extra-mode/263-RESEARCH.md</c>
/// Section 7.
/// </summary>
internal static class GenaiConfigRetargeter
{
    /// <summary>
    /// Rewrites <c>genai_config.json</c>'s <c>provider_options</c> entries from
    /// <c>cuda</c> (legacy or new-form) to <c>{ "name": "DML", "options": [] }</c>.
    /// Idempotent — already-DML configs are left untouched.
    /// Writes a one-time <c>genai_config.json.cuda.bak</c> backup on first rewrite.
    /// Returns silently if the file is missing (graceful degradation in DI).
    /// </summary>
    /// <param name="modelDir">Absolute path to the model directory containing <c>genai_config.json</c>.</param>
    /// <param name="logger">Optional logger for retarget telemetry.</param>
    public static void EnsureDirectMlProvider(string modelDir, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDir);

        string configPath = Path.Combine(modelDir, "genai_config.json");
        if (!File.Exists(configPath))
        {
            logger?.LogWarning("[HermesOnnx] genai_config.json not found at {Path}; retarget skipped", configPath);
            return;
        }

        string raw = File.ReadAllText(configPath);
        JsonNode? root = JsonNode.Parse(raw);
        if (root is null)
        {
            logger?.LogWarning("[HermesOnnx] genai_config.json is not valid JSON; retarget skipped");
            return;
        }

        JsonArray? providerOptions = root["model"]?["decoder"]?["session_options"]?["provider_options"]?.AsArray();
        if (providerOptions is null)
        {
            logger?.LogDebug("[HermesOnnx] genai_config.json has no model.decoder.session_options.provider_options; retarget skipped");
            return;
        }

        bool changed = false;
        for (int i = 0; i < providerOptions.Count; i++)
        {
            JsonObject? entry = providerOptions[i] as JsonObject;
            if (entry is null)
            {
                continue;
            }

            // Legacy form: { "cuda": { ... } }
            if (entry.ContainsKey("cuda"))
            {
                providerOptions[i] = new JsonObject { ["name"] = "DML", ["options"] = new JsonArray() };
                changed = true;
                continue;
            }

            // New form: { "name": "cuda", "options": [...] }
            string? name = entry["name"]?.GetValue<string>();
            if (name is not null && string.Equals(name, "cuda", StringComparison.OrdinalIgnoreCase))
            {
                entry["name"] = "DML";
                entry["options"] = new JsonArray();
                changed = true;
            }
        }

        if (changed)
        {
            string backupPath = configPath + ".cuda.bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(configPath, backupPath, overwrite: false);
            }

            JsonSerializerOptions writeOptions = new() { WriteIndented = true };
            File.WriteAllText(configPath, root.ToJsonString(writeOptions));
            logger?.LogInformation("[HermesOnnx] Retargeted genai_config.json: cuda -> DML (backup: {Backup})", backupPath);
        }
        else
        {
            logger?.LogDebug("[HermesOnnx] genai_config.json already targets DML; no rewrite needed");
        }
    }
}
