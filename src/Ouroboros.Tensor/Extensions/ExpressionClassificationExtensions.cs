// <copyright file="ExpressionClassificationExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Tensor.Adapters;

namespace Ouroboros.Tensor.Extensions;

/// <summary>
/// DI registration helpers for the self-perception expression classifier seam
/// introduced by the 260424-00n drift-logging slice.
/// </summary>
public static class ExpressionClassificationExtensions
{
    /// <summary>
    /// Registers <see cref="StubExpressionClassifier"/> as the
    /// <see cref="IExpressionClassifier"/> implementation. Idempotent via
    /// <c>TryAddSingleton</c> — a later <see cref="IExpressionClassifier"/>
    /// registration (e.g. real FER in v14.0) can supersede without removing
    /// this call.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddStubExpressionClassifier(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IExpressionClassifier, StubExpressionClassifier>();
        return services;
    }
}
