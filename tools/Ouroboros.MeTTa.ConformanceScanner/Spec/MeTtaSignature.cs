// <copyright file="MeTtaSignature.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Spec;

/// <summary>
/// Type-annotation form: (: name (-> In1 In2 ... Out)).
/// </summary>
/// <param name="Name">The operation name (e.g. "cons-atom").</param>
/// <param name="TypeExpression">Serialized type expression as it appeared in the spec.</param>
/// <param name="Arity">Count of input slots for (-> ...) forms; -1 when not a (-> ...) type.</param>
public sealed record MeTtaSignature(string Name, string TypeExpression, int Arity);
