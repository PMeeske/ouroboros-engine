// <copyright file="MeTTaRulePersistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Ouroboros.MeTTa.Persistence;

/// <summary>
/// Qdrant-backed implementation of <see cref="IMeTTaRulePersistence"/>.
/// Replaces the prior in-memory stub by writing rules to a dedicated
/// Qdrant collection (<c>metta-rules</c> by default) and reading them back
/// via filter-based scroll.
/// </summary>
/// <remarks>
/// <para>
/// The collection schema:
/// <list type="bullet">
///   <item>Vector size: 32 (deterministic SHA-256 derived stub vector).</item>
///   <item>Distance: Cosine.</item>
///   <item>Payload: atom_text, session_id, step, quality_score, timestamp.</item>
/// </list>
/// </para>
/// <para>
/// The "vector" is a deterministic 32-float fingerprint of the atom text.
/// This is intentional: we want round-trip persistence and filter queries,
/// not semantic similarity. Callers that need similarity should compose
/// this persistence with an embedding-aware decorator.
/// </para>
/// </remarks>
public sealed class MeTTaRulePersistence : IMeTTaRulePersistence
{
    /// <summary>
    /// Gets the default Qdrant collection name.
    /// </summary>
    public const string DefaultCollectionName = "metta-rules";

    private const int FingerprintVectorSize = 32;

    private readonly QdrantClient _client;
    private readonly string _collection;
    private readonly ILogger<MeTTaRulePersistence> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="client">The Qdrant client (typically DI-provided).</param>
    /// <param name="logger">Logger (optional — falls back to <see cref="NullLogger{T}"/>).</param>
    /// <param name="collectionName">Override the default collection name.</param>
    public MeTTaRulePersistence(
        QdrantClient client,
        ILogger<MeTTaRulePersistence>? logger = null,
        string collectionName = DefaultCollectionName)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _logger = logger ?? NullLogger<MeTTaRulePersistence>.Instance;
        _collection = string.IsNullOrWhiteSpace(collectionName) ? DefaultCollectionName : collectionName;
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> PersistAsync(MeTTaRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        try
        {
            await EnsureCollectionAsync(ct).ConfigureAwait(false);

            float[] vector = ComputeFingerprintVector(rule.AtomText);

            PointStruct point = new()
            {
                Id = new PointId { Uuid = ComputePointUuid(rule) },
                Vectors = vector,
                Payload =
                {
                    ["atom_text"] = rule.AtomText,
                    ["session_id"] = rule.SessionId,
                    ["step"] = rule.Step,
                    ["quality_score"] = rule.QualityScore,
                    ["timestamp"] = rule.Timestamp.ToUnixTimeMilliseconds(),
                    ["timestamp_iso"] = rule.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                },
            };

            await _client.UpsertAsync(_collection, new[] { point }, cancellationToken: ct).ConfigureAwait(false);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "PersistAsync failed for session {SessionId} step {Step}", rule.SessionId, rule.Step);
            return Result<Unit, string>.Failure($"PersistAsync failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<MeTTaRule>, string>> RestoreAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Result<IReadOnlyList<MeTTaRule>, string>.Failure("sessionId must be non-empty.");
        }

        try
        {
            bool exists = await _client.CollectionExistsAsync(_collection, ct).ConfigureAwait(false);
            if (!exists)
            {
                return Result<IReadOnlyList<MeTTaRule>, string>.Success(Array.Empty<MeTTaRule>());
            }

            Filter sessionFilter = new()
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "session_id",
                            Match = new Match { Keyword = sessionId },
                        },
                    },
                },
            };

            List<MeTTaRule> results = new();
            PointId? offset = null;

            // Scroll until we drain the matching points. Use generous chunk
            // size so a typical session resolves in a single round-trip.
            const uint chunkSize = 256;
            int safetyCounter = 0;
            const int safetyLimit = 1024;

            while (safetyCounter++ < safetyLimit)
            {
                ScrollResponse response = await _client.ScrollAsync(
                    _collection,
                    filter: sessionFilter,
                    limit: chunkSize,
                    offset: offset,
                    cancellationToken: ct).ConfigureAwait(false);

                foreach (RetrievedPoint point in response.Result)
                {
                    MeTTaRule? rule = TryHydrate(point);
                    if (rule is not null)
                    {
                        results.Add(rule);
                    }
                }

                if (response.NextPageOffset is null
                    || string.IsNullOrEmpty(response.NextPageOffset.ToString())
                    || response.Result.Count == 0)
                {
                    break;
                }

                offset = response.NextPageOffset;
            }

            results.Sort(static (a, b) => a.Step.CompareTo(b.Step));
            return Result<IReadOnlyList<MeTTaRule>, string>.Success(results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RestoreAsync failed for session {SessionId}", sessionId);
            return Result<IReadOnlyList<MeTTaRule>, string>.Failure($"RestoreAsync failed: {ex.Message}");
        }
    }

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            bool exists = await _client.CollectionExistsAsync(_collection, ct).ConfigureAwait(false);
            if (!exists)
            {
                await _client.CreateCollectionAsync(
                    _collection,
                    new VectorParams
                    {
                        Size = FingerprintVectorSize,
                        Distance = Distance.Cosine,
                    },
                    cancellationToken: ct).ConfigureAwait(false);
                _logger.LogInformation("Created Qdrant collection {Collection} for MeTTa rule persistence.", _collection);
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static MeTTaRule? TryHydrate(RetrievedPoint point)
    {
        if (point.Payload is null)
        {
            return null;
        }

        string atomText = ReadString(point.Payload, "atom_text");
        string sessionId = ReadString(point.Payload, "session_id");

        if (string.IsNullOrEmpty(atomText) || string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        int step = ReadInt(point.Payload, "step");
        double quality = ReadDouble(point.Payload, "quality_score");
        long ts = ReadLong(point.Payload, "timestamp");
        DateTimeOffset timestamp = ts > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(ts)
            : DateTimeOffset.MinValue;

        return new MeTTaRule(atomText, sessionId, step, quality, timestamp);
    }

    private static string ReadString(IReadOnlyDictionary<string, Value> payload, string key)
    {
        if (!payload.TryGetValue(key, out Value? value) || value is null)
        {
            return string.Empty;
        }

        return value.KindCase switch
        {
            Value.KindOneofCase.StringValue => value.StringValue ?? string.Empty,
            _ => string.Empty,
        };
    }

    private static int ReadInt(IReadOnlyDictionary<string, Value> payload, string key)
    {
        if (!payload.TryGetValue(key, out Value? value) || value is null)
        {
            return 0;
        }

        return value.KindCase switch
        {
            Value.KindOneofCase.IntegerValue => (int)value.IntegerValue,
            Value.KindOneofCase.DoubleValue => (int)value.DoubleValue,
            _ => 0,
        };
    }

    private static long ReadLong(IReadOnlyDictionary<string, Value> payload, string key)
    {
        if (!payload.TryGetValue(key, out Value? value) || value is null)
        {
            return 0;
        }

        return value.KindCase switch
        {
            Value.KindOneofCase.IntegerValue => value.IntegerValue,
            Value.KindOneofCase.DoubleValue => (long)value.DoubleValue,
            _ => 0,
        };
    }

    private static double ReadDouble(IReadOnlyDictionary<string, Value> payload, string key)
    {
        if (!payload.TryGetValue(key, out Value? value) || value is null)
        {
            return 0.0;
        }

        return value.KindCase switch
        {
            Value.KindOneofCase.DoubleValue => value.DoubleValue,
            Value.KindOneofCase.IntegerValue => value.IntegerValue,
            _ => 0.0,
        };
    }

    /// <summary>
    /// Computes a deterministic 32-float fingerprint vector from the atom
    /// text via SHA-256. The vector exists so Qdrant can store the point;
    /// it is NOT a semantic embedding.
    /// </summary>
    /// <param name="atomText">Source text.</param>
    /// <returns>A 32-element float vector.</returns>
    public static float[] ComputeFingerprintVector(string atomText)
    {
        ArgumentNullException.ThrowIfNull(atomText);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(atomText));
        float[] result = new float[FingerprintVectorSize];

        // 32 bytes -> 32 floats in [-1, 1].
        for (int i = 0; i < FingerprintVectorSize; i++)
        {
            byte b = hash[i % hash.Length];
            result[i] = (b / 127.5f) - 1.0f;
        }

        return result;
    }

    /// <summary>
    /// Generates a deterministic UUID-like point id from session/step/text
    /// so re-persisting the same rule is idempotent (upsert semantics).
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>A UUID-formatted point id.</returns>
    public static string ComputePointUuid(MeTTaRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        string composite = $"{rule.SessionId}|{rule.Step}|{rule.AtomText}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(composite));

        // Build a v4-shaped UUID from the first 16 bytes of SHA-256.
        // We force the version (4) and variant bits to keep Qdrant happy.
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40); // version 4
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant

        Guid guid = new(bytes);
        return guid.ToString("D");
    }
}
