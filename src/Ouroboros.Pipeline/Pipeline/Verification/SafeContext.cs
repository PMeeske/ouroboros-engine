// <copyright file="SafeContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Represents the security context for pipeline execution.
/// Used by MeTTa symbolic guard rails to enforce access control.
/// </summary>
public enum SafeContext
{
    /// <summary>
    /// Read-only context that blocks all write operations.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Full access context that allows all operations.
    /// </summary>
    FullAccess,
}