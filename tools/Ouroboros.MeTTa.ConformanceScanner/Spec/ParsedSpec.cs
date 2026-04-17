// <copyright file="ParsedSpec.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Spec;

/// <summary>
/// Root parse output for a stdlib.metta snapshot.
/// </summary>
public sealed record ParsedSpec(
    IReadOnlyDictionary<string, SpecSchema> Operations,
    int TotalForms,
    int UnparseableLines,
    string SourceSha256);
