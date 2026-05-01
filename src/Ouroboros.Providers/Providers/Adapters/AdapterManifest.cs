// <copyright file="AdapterManifest.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Adapters;

/// <summary>
/// Immutable manifest describing a LoRA adapter on disk. Loaded from a sibling
/// <c>*.adapter.json</c> file by <see cref="OgaAdapterRegistry"/>.
/// </summary>
/// <param name="Name">Stable, unique identifier used for activation lookup.</param>
/// <param name="BaseModelHash">Hash of the base model the adapter was trained against (compatibility gate).</param>
/// <param name="TrainingDataHash">Hash of the training corpus, when known (provenance).</param>
/// <param name="EvalScores">Named eval metric scores (e.g. "perplexity", "win_rate"); values are double.</param>
/// <param name="Capability">Approval tier — see <see cref="CapabilityClass"/>.</param>
/// <param name="CreatedAt">UTC timestamp the adapter was produced.</param>
/// <param name="Author">Optional author identifier (e.g. "iaret-self-train", a username).</param>
/// <param name="AdapterPath">Absolute path to the adapter weights / directory.</param>
public sealed record AdapterManifest(
    string Name,
    string BaseModelHash,
    Option<string> TrainingDataHash,
    IReadOnlyDictionary<string, double> EvalScores,
    CapabilityClass Capability,
    DateTimeOffset CreatedAt,
    Option<string> Author,
    string AdapterPath);
