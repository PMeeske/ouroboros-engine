// <copyright file="EthicsTradition.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics;

/// <summary>
/// The 9 ethical traditions encoded as engine-layer MeTTa atom files.
/// Each enum value maps to an embedded resource at
/// <c>Ouroboros.MeTTa.Ethics.{name}.metta</c>.
/// </summary>
public enum EthicsTradition
{
    /// <summary>Care ethics — Gilligan, Noddings, Held.</summary>
    CoreEthics,

    /// <summary>Non-harm — Jain, Gandhi.</summary>
    Ahimsa,

    /// <summary>Duty / dharma — Bhagavad Gita.</summary>
    BhagavadGita,

    /// <summary>Categorical imperative — Kant.</summary>
    Kantian,

    /// <summary>Ethics of the Other — Levinas.</summary>
    Levinas,

    /// <summary>Emptiness / dependent co-arising — Madhyamaka.</summary>
    Nagarjuna,

    /// <summary>Communal personhood — Ubuntu.</summary>
    Ubuntu,

    /// <summary>Value pluralism — Berlin, Williams.</summary>
    WisdomOfDisagreement,

    /// <summary>Self-reference and irresolvable paradox — Russell, Goedel, Spencer-Brown.</summary>
    Paradox,
}
