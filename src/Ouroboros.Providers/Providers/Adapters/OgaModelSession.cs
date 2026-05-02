// <copyright file="OgaModelSession.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgaAdapters = Microsoft.ML.OnnxRuntimeGenAI.Adapters;
using OgaModel = Microsoft.ML.OnnxRuntimeGenAI.Model;

namespace Ouroboros.Providers.Adapters;

/// <summary>
/// Owns a single ORT-GenAI <c>Model</c> and its companion <c>Adapters</c>
/// collection, and exposes a small register / unload / set-active surface for
/// <see cref="OgaAdapterRegistry"/> to call. The underlying <c>Model</c> is loaded
/// lazily on first use so that DI registration with a configured-but-not-yet-present
/// model path does not fail at startup.
/// </summary>
/// <remarks>
/// Phase A.4 scope: this type tracks the <em>desired-active</em> adapter name. Actual
/// activation happens at ORT-GenAI <c>Generator</c> construction time (the generator
/// is owned by the inference driver, not by this session) via
/// <c>generator.SetActiveAdapter(session.Adapters, session.Active)</c>. Generator
/// wiring is intentionally out of scope for this plan.
/// </remarks>
public sealed class OgaModelSession : IDisposable
{
    private readonly string _modelPath;
    private readonly ILogger<OgaModelSession> _logger;
    private readonly object _gate = new();
    private readonly HashSet<string> _registered = new(StringComparer.Ordinal);

    private OgaModel? _model;
    private OgaAdapters? _adapters;
    private Option<string> _active = Option<string>.None;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OgaModelSession"/> class.
    /// </summary>
    /// <param name="modelPath">Absolute path to the OGA model directory (containing <c>model.onnx</c> + companion files).</param>
    /// <param name="logger">Optional logger; when <c>null</c>, a <see cref="NullLogger{T}"/> is used.</param>
    public OgaModelSession(string modelPath, ILogger<OgaModelSession>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        _modelPath = modelPath;
        _logger = logger ?? NullLogger<OgaModelSession>.Instance;
    }

    /// <summary>
    /// Gets the desired-active adapter name (consumed at ORT-GenAI <c>Generator</c> construction).
    /// </summary>
    public Option<string> Active
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    /// <summary>
    /// Registers a LoRA adapter by name + on-disk path. Lazily loads the underlying
    /// ORT-GenAI <c>Model</c> on first call. Idempotent: re-registering the same name
    /// is a no-op success.
    /// </summary>
    /// <param name="name">Stable, unique adapter identifier.</param>
    /// <param name="adapterFilePath">Absolute path to the adapter weights / file.</param>
    /// <returns><see cref="Result{TValue,TError}.Success"/> on register or no-op; <see cref="Result{TValue,TError}.Failure"/> with a descriptive error otherwise.</returns>
    public Result<Unit, string> RegisterAdapter(string name, string adapterFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterFilePath);

        lock (_gate)
        {
            if (_disposed)
            {
                return Result<Unit, string>.Failure("OgaModelSession is disposed");
            }

            Result<Unit, string> ensured = EnsureLoaded();
            if (ensured.IsFailure)
            {
                return ensured;
            }

            if (_registered.Contains(name))
            {
                _logger.LogDebug("Adapter '{Name}' already registered; treating as no-op.", name);
                return Result<Unit, string>.Success(Unit.Value);
            }

#pragma warning disable CA1031 // Native ORT-GenAI calls can throw arbitrary exceptions; convert to Result for the caller.
            try
            {
                _adapters!.LoadAdapter(adapterFilePath, name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OGA LoadAdapter failed for '{Name}' at '{Path}'", name, adapterFilePath);
                return Result<Unit, string>.Failure(
                    $"OGA LoadAdapter failed for '{name}' at '{adapterFilePath}': {ex.Message}");
            }
#pragma warning restore CA1031

            _registered.Add(name);
            _logger.LogInformation("Registered LoRA adapter '{Name}' from '{Path}'", name, adapterFilePath);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <summary>
    /// Unloads a previously-registered LoRA adapter. Idempotent: unloading an unknown
    /// name is a no-op success. If the unloaded adapter was the active one, the
    /// desired-active marker is cleared.
    /// </summary>
    /// <param name="name">The adapter name to unload.</param>
    /// <returns><see cref="Result{TValue,TError}.Success"/> on unload or no-op; <see cref="Result{TValue,TError}.Failure"/> if the native unload throws.</returns>
    public Result<Unit, string> UnloadAdapter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
        {
            if (_disposed)
            {
                return Result<Unit, string>.Failure("OgaModelSession is disposed");
            }

            if (!_registered.Contains(name))
            {
                _logger.LogDebug("Adapter '{Name}' is not registered; UnloadAdapter is a no-op.", name);
                return Result<Unit, string>.Success(Unit.Value);
            }

            if (_adapters is null)
            {
                // Defensive: nothing to unload at the native level, but keep state consistent.
                _registered.Remove(name);
                if (_active.HasValue && string.Equals(_active.Value, name, StringComparison.Ordinal))
                {
                    _active = Option<string>.None;
                }

                return Result<Unit, string>.Success(Unit.Value);
            }

#pragma warning disable CA1031 // Native ORT-GenAI calls can throw arbitrary exceptions; convert to Result for the caller.
            try
            {
                _adapters.UnloadAdapter(name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OGA UnloadAdapter failed for '{Name}'", name);
                return Result<Unit, string>.Failure($"OGA UnloadAdapter failed for '{name}': {ex.Message}");
            }
#pragma warning restore CA1031

            _registered.Remove(name);
            if (_active.HasValue && string.Equals(_active.Value, name, StringComparison.Ordinal))
            {
                _active = Option<string>.None;
            }

            _logger.LogInformation("Unloaded LoRA adapter '{Name}'", name);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <summary>
    /// Sets the desired-active adapter name. The adapter must already be registered
    /// via <see cref="RegisterAdapter"/>. Activation is consumed at the next
    /// ORT-GenAI <c>Generator</c> construction.
    /// </summary>
    /// <param name="name">Name of an already-registered adapter to mark active.</param>
    /// <returns><see cref="Result{TValue,TError}.Success"/> on update; <see cref="Result{TValue,TError}.Failure"/> when the adapter is not registered or the session is disposed.</returns>
    public Result<Unit, string> SetActive(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
        {
            if (_disposed)
            {
                return Result<Unit, string>.Failure("OgaModelSession is disposed");
            }

            if (!_registered.Contains(name))
            {
                return Result<Unit, string>.Failure(
                    $"Adapter '{name}' is not registered; call RegisterAdapter first.");
            }

            _active = Option<string>.Some(name);
            _logger.LogDebug("Active LoRA adapter set to '{Name}' (will apply at next Generator).", name);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <summary>
    /// Clears the desired-active adapter marker. Loaded adapters remain registered
    /// (kept for fast re-activation).
    /// </summary>
    /// <returns><see cref="Result{TValue,TError}.Success"/> on clear; <see cref="Result{TValue,TError}.Failure"/> when the session is disposed.</returns>
    public Result<Unit, string> ClearActive()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return Result<Unit, string>.Failure("OgaModelSession is disposed");
            }

            _active = Option<string>.None;
            _logger.LogDebug("Active LoRA adapter cleared.");
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <summary>
    /// Disposes the underlying <c>Adapters</c> then <c>Model</c> in
    /// deterministic order. Idempotent and safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

#pragma warning disable CA1031 // Native handle disposal must never throw out of Dispose; degrade gracefully with a warning log.
            try
            {
                _adapters?.Dispose();
                _adapters = null;
                _model?.Dispose();
                _model = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception during OgaModelSession.Dispose; native handles may be in an inconsistent state.");
            }
#pragma warning restore CA1031
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Lazy-loads the underlying ORT-GenAI <c>Model</c> and <c>Adapters</c> on
    /// first use. Caller MUST already hold <c>_gate</c>.
    /// </summary>
    /// <returns><see cref="Result{TValue,TError}.Success"/> when the model is ready; <see cref="Result{TValue,TError}.Failure"/> with a clean error string on native load failure.</returns>
    private Result<Unit, string> EnsureLoaded()
    {
        if (_disposed)
        {
            return Result<Unit, string>.Failure("OgaModelSession is disposed");
        }

        if (_model is not null && _adapters is not null)
        {
            return Result<Unit, string>.Success(Unit.Value);
        }

#pragma warning disable CA1031 // Native ORT-GenAI loads can throw FileNotFoundException, DllNotFoundException, etc.; convert to Result.
        try
        {
            _model = new OgaModel(_modelPath);
            _adapters = new OgaAdapters(_model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load OGA model from '{Path}'", _modelPath);

            // Clean up partial state.
            _adapters?.Dispose();
            _adapters = null;
            _model?.Dispose();
            _model = null;

            return Result<Unit, string>.Failure(
                $"failed to load OGA model from '{_modelPath}': {ex.Message}");
        }
#pragma warning restore CA1031

        return Result<Unit, string>.Success(Unit.Value);
    }
}
