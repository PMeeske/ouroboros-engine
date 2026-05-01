// <copyright file="IAdapterRegistry.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Adapters;

/// <summary>
/// Registry of LoRA adapters discovered on disk. Owns the currently-active adapter
/// (if any) and brokers Activate/Deactivate against the underlying OGA <c>Model</c>
/// once Phase A.4 wires the lower-level Adapters API.
/// </summary>
/// <remarks>
/// Phase A scope: load-from-disk + list/get only. Activate/Deactivate currently
/// return <see cref="Result{TValue,TError}.Failure"/> with a "not yet wired" error.
/// </remarks>
public interface IAdapterRegistry
{
    /// <summary>
    /// Gets the name of the currently-active adapter, or <see cref="Option{T}.None"/> when none is active.
    /// </summary>
    Option<string> Active { get; }

    /// <summary>
    /// Scans the configured adapter root directory for <c>*.adapter.json</c> manifests
    /// and indexes them by name. Idempotent — safe to call multiple times; the latest
    /// scan replaces the previous index atomically.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the scan finishes.</returns>
    Task LoadFromDiskAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all currently-known manifests.</summary>
    /// <returns>An immutable snapshot of all known adapter manifests.</returns>
    IReadOnlyList<AdapterManifest> List();

    /// <summary>Looks up a manifest by name.</summary>
    /// <param name="name">The manifest name.</param>
    /// <returns><see cref="Option{T}.Some"/> when a manifest with the given name exists; otherwise <see cref="Option{T}.None"/>.</returns>
    Option<AdapterManifest> Get(string name);

    /// <summary>
    /// Activates the named adapter against the underlying model. Phase A: not yet wired —
    /// returns a Failure with a descriptive error.
    /// </summary>
    /// <param name="name">The adapter name to activate.</param>
    /// <returns>A <see cref="Result{TValue,TError}"/> indicating success or a descriptive failure string.</returns>
    Result<Unit, string> Activate(string name);

    /// <summary>
    /// Deactivates the currently-active adapter. Phase A: not yet wired — returns a Failure.
    /// </summary>
    /// <returns>A <see cref="Result{TValue,TError}"/> indicating success or a descriptive failure string.</returns>
    Result<Unit, string> Deactivate();
}
