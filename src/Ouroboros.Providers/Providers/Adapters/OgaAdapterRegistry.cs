// <copyright file="OgaAdapterRegistry.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Adapters;

/// <summary>
/// Disk-scanning <see cref="IAdapterRegistry"/> implementation. Scans a configured
/// adapter root directory for <c>*.adapter.json</c> manifest files, deserializes
/// them, and indexes by name. Activate/Deactivate delegate to a bound
/// <see cref="OgaModelSession"/> when present.
/// </summary>
/// <remarks>
/// When constructed without a session, <see cref="Activate"/> returns a Failure
/// indicating no model session is bound — useful for staged DI wiring where the
/// model path is supplied later.
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
    private readonly OgaModelSession? _session;
    private readonly object _sync = new();

    private ImmutableDictionary<string, AdapterManifest> _byName = ImmutableDictionary<string, AdapterManifest>.Empty;
    private Option<string> _active = Option<string>.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="OgaAdapterRegistry"/> class
    /// without a bound model session. <see cref="Activate"/> will return a
    /// "no session bound" Failure until a session is supplied via the three-arg
    /// constructor overload.
    /// </summary>
    /// <param name="adapterRootPath">Absolute path to the directory containing <c>*.adapter.json</c> manifests.</param>
    /// <param name="logger">Logger instance.</param>
    public OgaAdapterRegistry(string adapterRootPath, ILogger<OgaAdapterRegistry> logger)
        : this(adapterRootPath, logger, session: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OgaAdapterRegistry"/> class with an
    /// optional <see cref="OgaModelSession"/> binding. When <paramref name="session"/> is
    /// <c>null</c>, <see cref="Activate"/> returns a Failure indicating no model session
    /// is bound — useful for staged DI wiring and tests.
    /// </summary>
    /// <param name="adapterRootPath">Absolute path to the directory containing <c>*.adapter.json</c> manifests.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="session">Optional model session that owns the underlying ORT-GenAI Model + Adapters pair.</param>
    public OgaAdapterRegistry(
        string adapterRootPath,
        ILogger<OgaAdapterRegistry> logger,
        OgaModelSession? session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterRootPath);
        ArgumentNullException.ThrowIfNull(logger);

        _adapterRoot = adapterRootPath;
        _logger = logger;
        _session = session;
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

        AdapterManifest? manifest;
        lock (_sync)
        {
            if (!_byName.TryGetValue(name, out manifest!))
            {
                return Result<Unit, string>.Failure($"adapter not registered: '{name}'");
            }
        }

        OgaModelSession? session = _session;
        if (session is null)
        {
            return Result<Unit, string>.Failure(
                "OgaModelSession is not bound to this registry; cannot activate adapters. Pass a modelPath to AddOgaAdapterRegistry or supply a session via the constructor overload.");
        }

        Result<Unit, string> register = session.RegisterAdapter(manifest.Name, manifest.AdapterPath);
        if (register.IsFailure)
        {
            return register;
        }

        Result<Unit, string> setActive = session.SetActive(name);
        if (setActive.IsFailure)
        {
            return setActive;
        }

        lock (_sync)
        {
            _active = Option<string>.Some(name);
        }

        _logger.LogInformation("Activated LoRA adapter '{Name}' from '{Path}'", manifest.Name, manifest.AdapterPath);
        return Result<Unit, string>.Success(Unit.Value);
    }

    /// <inheritdoc/>
    public Result<Unit, string> Deactivate()
    {
        Option<string> previous;
        lock (_sync)
        {
            previous = _active;
        }

        if (!previous.HasValue)
        {
            return Result<Unit, string>.Success(Unit.Value);
        }

        OgaModelSession? session = _session;
        if (session is null)
        {
            // Defensive: if the session vanished (e.g. mid-test), still clear local state.
            lock (_sync)
            {
                _active = Option<string>.None;
            }

            _logger.LogWarning("Deactivate called with no session bound; cleared local active marker only.");
            return Result<Unit, string>.Success(Unit.Value);
        }

        Result<Unit, string> clear = session.ClearActive();
        if (clear.IsFailure)
        {
            return clear;
        }

        lock (_sync)
        {
            _active = Option<string>.None;
        }

        _logger.LogInformation("Deactivated LoRA adapter '{Name}' (kept loaded for fast re-activation).", previous.Value);
        return Result<Unit, string>.Success(Unit.Value);
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
