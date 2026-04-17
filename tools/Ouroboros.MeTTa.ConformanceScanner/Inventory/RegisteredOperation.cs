// <copyright file="RegisteredOperation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Inventory;

/// <summary>
/// A single grounded operation observed by the scanner at collection time.
/// </summary>
public sealed record RegisteredOperation(
    string Name,
    int? LambdaArity,
    string RegistrationSource,
    string? Notes);
