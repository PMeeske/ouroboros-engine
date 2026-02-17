// <copyright file="WalEntry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Network.Persistence;

/// <summary>
/// Represents a single entry in the Write-Ahead Log.
/// Each entry captures either a node or edge addition with its serialized payload.
/// </summary>
public sealed record WalEntry(
    WalEntryType Type,
    DateTimeOffset Timestamp,
    string PayloadJson);
