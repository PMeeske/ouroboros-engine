// <copyright file="QdrantSkillRegistry.Sync.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Diagnostics;
using System.Text.Json;
using Qdrant.Client.Grpc;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class QdrantSkillRegistry
{
    private int? _detectedVectorSize;

    private async Task EnsureCollectionExistsAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
            if (!collectionExists)
            {
                // Detect vector size from embedding model if available
                int vectorSize = _config.VectorSize;
                if (_embedding != null)
                {
                    try
                    {
                        var sampleEmbedding = await _embedding.CreateEmbeddingsAsync("sample text for dimension detection", ct);
                        vectorSize = sampleEmbedding.Length;
                        _detectedVectorSize = vectorSize;
                        Trace.TraceInformation("[qdrant] Detected embedding dimension: {0}", vectorSize);
                    }
                    catch
                    {
                        // Use default if detection fails
                    }
                }

                await _client.CreateCollectionAsync(
                    _config.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)vectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);

                Trace.TraceInformation("[qdrant] Created skills collection: {0}", _config.CollectionName);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Trace.TraceWarning("[WARN] Failed to ensure Qdrant collection: {0}", ex.Message);
        }
    }

    private async Task SaveSkillToQdrantAsync(AgentSkill skill, CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            // Create searchable text from skill
            string searchText = $"{skill.Name}: {skill.Description}. Category: {skill.Category}. Tags: {string.Join(", ", skill.Tags)}. Preconditions: {string.Join(", ", skill.Preconditions)}. Effects: {string.Join(", ", skill.Effects)}";

            // Generate embedding
            float[] embedding;
            if (_embedding != null)
            {
                embedding = await _embedding.CreateEmbeddingsAsync(searchText, ct);
            }
            else
            {
                // Use a simple hash-based "embedding" as fallback (not for semantic search)
                int vectorSize = _detectedVectorSize ?? _config.VectorSize;
                embedding = GenerateFallbackEmbedding(searchText, vectorSize);
            }

            // Serialize skill data for storage
            var skillJson = JsonSerializer.Serialize(new SerializableSkillData(
                skill.Id,
                skill.Name,
                skill.Description,
                skill.Category,
                skill.Preconditions.ToList(),
                skill.Effects.ToList(),
                skill.SuccessRate,
                skill.UsageCount,
                skill.AverageExecutionTime,
                skill.Tags.ToList()
            ), JsonOptions);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = GenerateSkillId(skill.Id) },
                Vectors = embedding,
                Payload =
                {
                    ["skill_id"] = skill.Id,
                    ["skill_name"] = skill.Name,
                    ["description"] = skill.Description,
                    ["category"] = skill.Category,
                    ["success_rate"] = skill.SuccessRate,
                    ["usage_count"] = skill.UsageCount,
                    ["average_execution_time"] = skill.AverageExecutionTime,
                    ["skill_data"] = skillJson,
                    ["type"] = "skill"
                }
            };

            await _client.UpsertAsync(_config.CollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Trace.TraceWarning("[WARN] Failed to save skill '{0}' to Qdrant: {1}", skill.Name, ex.Message);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task LoadSkillsFromQdrantAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
            if (!collectionExists)
            {
                return;
            }

            // Scroll through all points in the collection using the proper API
            var scrollResult = await _client.ScrollAsync(
                _config.CollectionName,
                payloadSelector: true,
                limit: 1000,
                cancellationToken: ct);

            foreach (var point in scrollResult.Result)
            {
                try
                {
                    if (point.Payload.TryGetValue("skill_data", out var skillDataValue) &&
                        !string.IsNullOrWhiteSpace(skillDataValue.StringValue))
                    {
                        var skillData = JsonSerializer.Deserialize<SerializableSkillData>(
                            skillDataValue.StringValue, JsonOptions);

                        if (skillData != null)
                        {
                            var skillId = string.IsNullOrWhiteSpace(skillData.Id)
                                ? null
                                : skillData.Id.Trim();
                            if (string.IsNullOrWhiteSpace(skillId))
                            {
                                Trace.TraceWarning("[WARN] Skipping Qdrant skill with missing id");
                                continue;
                            }

                            var skillName = string.IsNullOrWhiteSpace(skillData.Name)
                                ? skillId
                                : skillData.Name.Trim();
                            var description = skillData.Description?.Trim() ?? string.Empty;
                            var category = string.IsNullOrWhiteSpace(skillData.Category)
                                ? "general"
                                : skillData.Category.Trim();

                            var preconditions = skillData.Preconditions?
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.Trim())
                                .ToList()
                                ?? new List<string>();

                            var effects = skillData.Effects?
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.Trim())
                                .ToList()
                                ?? new List<string>();

                            var tags = skillData.Tags?
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.Trim())
                                .ToList()
                                ?? new List<string>();

                            var skill = new AgentSkill(
                                skillId,
                                skillName,
                                description,
                                category,
                                preconditions,
                                effects,
                                skillData.SuccessRate,
                                skillData.UsageCount,
                                skillData.AverageExecutionTime,
                                tags);

                            _skillsCache[skill.Id] = skill;
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Trace.TraceWarning("[WARN] Failed to deserialize skill from Qdrant: {0}", ex.Message);
                }
            }

            if (_skillsCache.Count > 0)
            {
                Trace.TraceInformation("[qdrant] Loaded {0} skills from {1}", _skillsCache.Count, _config.CollectionName);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Trace.TraceWarning("[WARN] Failed to load skills from Qdrant: {0}", ex.Message);
        }
    }

    private static string GenerateSkillId(string skillName)
    {
        // Create a deterministic UUID from the skill name
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"skill_{skillName}"));
        return new Guid(hash).ToString();
    }

    private static string NormalizeConnectionString(string? rawConnectionString)
    {
        var endpoint = (rawConnectionString ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "http://localhost:6334";
        }

        var schemeSeparatorCount = endpoint.Split("://", StringSplitOptions.None).Length - 1;
        if (schemeSeparatorCount > 1)
        {
            return "http://localhost:6334";
        }

        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            endpoint = $"http://{endpoint}";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return "http://localhost:6334";
        }

        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return "http://localhost:6334";
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Host.Contains("://", StringComparison.Ordinal))
        {
            return "http://localhost:6334";
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static float[] GenerateFallbackEmbedding(string text, int size)
    {
        // Generate a simple hash-based vector as fallback when no embedding model is available
        // This is NOT for semantic search - just for storage
        var embedding = new float[size];
        var hash = text.GetHashCode();
        var random = new Random(hash);
        for (int i = 0; i < size; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // -1 to 1
        }
        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < size; i++)
                embedding[i] /= magnitude;
        }
        return embedding;
    }
}
