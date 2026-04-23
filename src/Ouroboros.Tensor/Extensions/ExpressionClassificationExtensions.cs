// <copyright file="ExpressionClassificationExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Tensor.Adapters;
using Ouroboros.Tensor.Orchestration;

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

    /// <summary>
    /// Registers <see cref="OnnxExpressionClassifier"/> as the
    /// <see cref="IExpressionClassifier"/> implementation. Throws at first
    /// resolution if the model file is missing — prefer
    /// <see cref="AddExpressionClassifierWithOnnxFallback"/> for a graceful
    /// fallback to the stub.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="modelPath">Absolute path to the FER+ ONNX model file.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddOnnxExpressionClassifier(
        this IServiceCollection services,
        string modelPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        services.TryAddSingleton<IExpressionClassifier>(sp =>
        {
            var scheduler = sp.GetRequiredService<GpuScheduler>();
            var logger = sp.GetService<ILogger<OnnxExpressionClassifier>>();
            return new OnnxExpressionClassifier(modelPath, scheduler, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IExpressionClassifier"/> with file-existence-based
    /// selection: <see cref="OnnxExpressionClassifier"/> when the FER+ model file
    /// is found at the resolved path, otherwise <see cref="StubExpressionClassifier"/>.
    /// File-existence is probed at the first resolution; the selected
    /// implementation is cached as a singleton.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="modelPath">
    ///   Resolved absolute path to <c>emotion-ferplus-8.onnx</c>. May be null or
    ///   missing — fallback to stub is silent except for a single startup INFO log.
    /// </param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddExpressionClassifierWithOnnxFallback(
        this IServiceCollection services,
        string? modelPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExpressionClassifier>(sp =>
        {
            var logger = sp.GetService<ILoggerFactory>()?
                .CreateLogger("Ouroboros.Tensor.Adapters.ExpressionClassifierFactory");

            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                try
                {
                    var scheduler = sp.GetRequiredService<GpuScheduler>();
                    var classifierLogger = sp.GetService<ILogger<OnnxExpressionClassifier>>();
                    logger?.LogInformation(
                        "IExpressionClassifier → OnnxExpressionClassifier (model={ModelPath})",
                        modelPath);
                    return new OnnxExpressionClassifier(modelPath, scheduler, classifierLogger);
                }
#pragma warning disable CA1031 // ONNX session creation failure must fall back to stub
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    logger?.LogWarning(
                        ex,
                        "IExpressionClassifier → OnnxExpressionClassifier failed to initialize; falling back to StubExpressionClassifier");
                }
            }
            else
            {
                logger?.LogInformation(
                    "IExpressionClassifier → StubExpressionClassifier (FER+ model not found at '{ModelPath}')",
                    modelPath ?? "<unset>");
            }

            var stubLogger = sp.GetService<ILogger<StubExpressionClassifier>>();
            return new StubExpressionClassifier(stubLogger);
        });

        return services;
    }
}
