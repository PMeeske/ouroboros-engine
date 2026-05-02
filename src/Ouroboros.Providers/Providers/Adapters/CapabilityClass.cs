// <copyright file="CapabilityClass.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Adapters;

/// <summary>
/// Capability tier for a LoRA adapter — gates whether the adapter can be activated
/// without human review. Used by Phase D approval gates.
/// </summary>
public enum CapabilityClass
{
    /// <summary>Adapter is safe to auto-activate (e.g. style or domain shift only).</summary>
    AutoApprove = 0,

    /// <summary>Adapter requires human approval before activation (capability change or risk).</summary>
    HumanApprove = 1,

    /// <summary>Adapter must never be activated automatically (quarantined / failed eval).</summary>
    Forbidden = 2,
}
