// <copyright file="HlslShaderLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// Reads compiled DXIL shader bytecode embedded in the assembly manifest
/// by the Phase 188.1.1 plan 02 MSBuild DXC target. Every logical shader
/// name maps to a resource of the form
/// <c>Ouroboros.Tensor.Shaders.{name}.dxil</c>.
/// </summary>
/// <remarks>
/// <para>
/// Returns <see langword="null"/> when a resource is missing — the caller
/// (<see cref="DirectComputeGaussianRasterizer"/>) treats a null return as
/// a hard gate that latches the CPU fallback for the whole class. DXC may
/// have been unavailable at build time (non-Windows host, SDK not on PATH)
/// or the runtime-compile path (<c>Avatar:Rasterizer:UseRuntimeCompile</c>)
/// was never exercised — either way, CPU is the correct response, not a
/// throw that tears down the DI graph.
/// </para>
/// </remarks>
public sealed class HlslShaderLoader
{
    private readonly Assembly _assembly;
    private readonly ILogger _logger;

    /// <summary>Creates a loader bound to <see cref="HlslShaderLoader"/>'s own assembly.</summary>
    public HlslShaderLoader(ILogger<HlslShaderLoader>? logger = null)
        : this(typeof(HlslShaderLoader).Assembly, logger)
    {
    }

    /// <summary>Creates a loader bound to an arbitrary assembly (test seam).</summary>
    public HlslShaderLoader(Assembly assembly, ILogger<HlslShaderLoader>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _assembly = assembly;
        _logger = logger ?? NullLogger<HlslShaderLoader>.Instance;
    }

    /// <summary>
    /// Loads the compiled DXIL bytecode for <paramref name="shaderName"/> — the
    /// file-stem of the original <c>.hlsl</c> source (e.g. <c>"gaussian_project"</c>).
    /// </summary>
    /// <param name="shaderName">Logical shader name (no extension, no path).</param>
    /// <returns>Byte buffer containing the DXIL, or <see langword="null"/> when the resource is missing.</returns>
    public byte[]? LoadDxil(string shaderName)
    {
        ArgumentException.ThrowIfNullOrEmpty(shaderName);

        string resourceName = $"Ouroboros.Tensor.Shaders.{shaderName}.dxil";
        using Stream? stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _logger.LogWarning(
                "[HlslShaderLoader] missing embedded resource '{Resource}' — rasterizer will latch CPU fallback",
                resourceName);
            return null;
        }

        using var ms = new MemoryStream((int)Math.Min(stream.Length, int.MaxValue));
        stream.CopyTo(ms);
        byte[] buffer = ms.ToArray();
        _logger.LogDebug(
            "[HlslShaderLoader] loaded DXIL '{Resource}' — {Bytes} bytes",
            resourceName, buffer.Length);
        return buffer;
    }

    /// <summary>
    /// Returns <see langword="true"/> when every name in <paramref name="required"/>
    /// resolves to a non-null DXIL buffer. Used by the rasterizer's init gate
    /// to decide availability in one atomic check before allocating D3D12
    /// resources.
    /// </summary>
    public bool TryLoadAll(IReadOnlyList<string> required, out IReadOnlyDictionary<string, byte[]> loaded)
    {
        ArgumentNullException.ThrowIfNull(required);

        var result = new Dictionary<string, byte[]>(required.Count);
        foreach (string name in required)
        {
            byte[]? dxil = LoadDxil(name);
            if (dxil is null)
            {
                loaded = result;
                return false;
            }
            result[name] = dxil;
        }

        loaded = result;
        return true;
    }
}
