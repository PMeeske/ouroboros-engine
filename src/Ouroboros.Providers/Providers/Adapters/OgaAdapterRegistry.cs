// <copyright file="OgaAdapterRegistry.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Adapters;

/// <summary>
/// Disk-scanning <see cref="IAdapterRegistry"/> implementation. Scans a configured
/// adapter root directory for <c>*.adapter.json</c> manifest files, deserializes
/// them, and indexes by name.
/// </summary>
/// <remarks>
/// Phase A scaffold: <see cref="Activate"/> and <see cref="Deactivate"/> return
/// <see cref="Result{TValue,TError}.Failure"/> with a Phase A.4 hint string. The
/// lower-level OGA <c>Model.RegisterAdapter</c> + <c>Generator.SetActiveAdapter</c>
/// plumbing is intentionally deferred to Phase A.4.
/// </remarks>
public sealed class OgaAdapterRegistry : IAdapterRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _adapterRoot;
    private readonly ILogger<OgaAdapterRegistry> _logger;
    private readonly object _sync = new();

    private ImmutableDictionary<string, AdapterManifest> _byName = ImmutableDictionary<string, AdapterManifest>.Empty;
    private Option<string> _active = Option<string>.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="OgaAdapterRegistry"/> class.
    /// </summary>
    /// <param name="adapterRootPath">Absolute path to the directory containing <c>*.adapter.json</c> manifests.</param>
    /// <param name="logger">Logger instance.</param>
    public OgaAdapterRegistry(string adapterRootPath, ILogger<OgaAdapterRegistry> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterRootPath);
        ArgumentNullException.ThrowIfNull(logger);

        _adapterRoot = adapterRootPath;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Option<string> Active => _active;

    /// <inheritdoc/>
    public async Task LoadFromDiskAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_adapterRoot))
        {
            _logger.LogWarning("Adapter root '{Path}' does not exist; registry is empty.", _adapterRoot);
            lock (_sync)
            {
                _byName = ImmutableDictionary<string, AdapterManifest>.Empty;
            }

            return;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, AdapterManifest>(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(_adapterRoot, "*.adapter.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            AdapterManifest? manifest = null;

#pragma warning disable CA1031 // Do not catch general exception types — per-file isolation: one bad manifest must not abort the scan.
            try
            {
                string json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                AdapterManifestDto? dto = JsonSerializer.Deserialize<AdapterManifestDto>(json, JsonOptions);
                if (dto is null)
                {
                    _logger.LogWarning("Adapter manifest '{File}' deserialized to null; skipping.", file);
                    continue;
                }

                manifest = TryProject(dto, file);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read or parse adapter manifest '{File}'; skipping.", file);
                continue;
            }
#pragma warning restore CA1031

            if (manifest is null)
            {
                continue;
            }

            if (builder.ContainsKey(manifest.Name))
            {
                _logger.LogWarning(
                    "Duplicate adapter manifest name '{Name}' in '{File}' — last-write-wins.",
                    manifest.Name,
                    file);
            }

            builder[manifest.Name] = manifest;
        }

        ImmutableDictionary<string, AdapterManifest> snapshot = builder.ToImmutable();

        lock (_sync)
        {
            _byName = snapshot;
        }

        _logger.LogInformation(
            "Loaded {Count} LoRA adapter manifests from '{Root}'",
            snapshot.Count,
            _adapterRoot);
    }

    /// <inheritdoc/>
    public IReadOnlyList<AdapterManifest> List()
    {
        ImmutableDictionary<string, AdapterManifest> snapshot;
        lock (_sync)
        {
            snapshot = _byName;
        }

        return snapshot.Values.ToImmutableArray();
    }

    /// <inheritdoc/>
    public Option<AdapterManifest> Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ImmutableDictionary<string, AdapterManifest> snapshot;
        lock (_sync)
        {
            snapshot = _byName;
        }

        return snapshot.TryGetValue(name, out AdapterManifest? manifest)
            ? Option<AdapterManifest>.Some(manifest)
            : Option<AdapterManifest>.None;
    }

    /// <inheritdoc/>
    public Result<Unit, string> Activate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Result<Unit, string>.Failure(
            "OGA adapter activation is not yet wired (Phase A.4 will register adapters via Model.RegisterAdapter and switch via Generator.SetActiveAdapter).");
    }

    /// <inheritdoc/>
    public Result<Unit, string> Deactivate()
    {
        return Result<Unit, string>.Failure(
            "OGA adapter activation is not yet wired (Phase A.4 will register adapters via Model.RegisterAdapter and switch via Generator.SetActiveAdapter).");
    }

    private AdapterManifest? TryProject(AdapterManifestDto dto, string manifestFile)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            _logger.LogWarning("Adapter manifest '{File}' is missing 'Name'; skipping.", manifestFile);
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.BaseModelHash))
        {
            _logger.LogWarning("Adapter manifest '{File}' is missing 'BaseModelHash'; skipping.", manifestFile);
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.AdapterPath))
        {
            _logger.LogWarning("Adapter manifest '{File}' is missing 'AdapterPath'; skipping.", manifestFile);
            return null;
        }

        string adapterPath = dto.AdapterPath!;
        if (!Path.IsPathRooted(adapterPath))
        {
            string? manifestDir = Path.GetDirectoryName(manifestFile);
            if (!string.IsNullOrEmpty(manifestDir))
            {
                adapterPath = Path.GetFullPath(Path.Combine(manifestDir, adapterPath));
            }
        }

        Option<string> trainingDataHash = string.IsNullOrWhiteSpace(dto.TrainingDataHash)
            ? Option<string>.None
            : Option<string>.Some(dto.TrainingDataHash!);

        Option<string> author = string.IsNullOrWhiteSpace(dto.Author)
            ? Option<string>.None
            : Option<string>.Some(dto.Author!);

        IReadOnlyDictionary<string, double> evalScores = dto.EvalScores
            ?? (IReadOnlyDictionary<string, double>)ImmutableDictionary<string, double>.Empty;

        return new AdapterManifest(
            dto.Name!,
            dto.BaseModelHash!,
            trainingDataHash,
            evalScores,
            dto.Capability,
            dto.CreatedAt,
            author,
            adapterPath);
    }

    private sealed record AdapterManifestDto(
        string? Name,
        string? BaseModelHash,
        string? TrainingDataHash,
        IReadOnlyDictionary<string, double>? EvalScores,
        CapabilityClass Capability,
        DateTimeOffset CreatedAt,
        string? Author,
        string? AdapterPath);
}
