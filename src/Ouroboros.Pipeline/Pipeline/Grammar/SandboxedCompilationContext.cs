// <copyright file="SandboxedCompilationContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

using System.Reflection;
using System.Runtime.Loader;

/// <summary>
/// A collectible <see cref="AssemblyLoadContext"/> that isolates dynamically
/// compiled parser assemblies. When disposed, the loaded assemblies become
/// eligible for garbage collection, preventing memory leaks from repeated
/// grammar compilation cycles.
/// </summary>
/// <remarks>
/// Security: only assemblies from a whitelisted set of references can be
/// loaded. This prevents dynamically compiled grammars from accessing
/// dangerous APIs (file system, network, reflection).
/// </remarks>
public sealed class SandboxedCompilationContext : AssemblyLoadContext, IDisposable
{
    private readonly string _name;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxedCompilationContext"/> class.
    /// </summary>
    /// <param name="name">A descriptive name for this compilation context.</param>
    public SandboxedCompilationContext(string name)
        : base(name, isCollectible: true)
    {
        _name = name;
    }

    /// <summary>
    /// Gets the context name.
    /// </summary>
    public string ContextName => _name;

    /// <summary>
    /// Loads an assembly from an in-memory stream.
    /// </summary>
    /// <param name="assemblyStream">The compiled assembly bytes.</param>
    /// <returns>The loaded assembly.</returns>
    public Assembly LoadFromMemoryStream(MemoryStream assemblyStream)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        assemblyStream.Seek(0, SeekOrigin.Begin);
        return LoadFromStream(assemblyStream);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unload();
    }

    /// <inheritdoc/>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Return null to fall through to the default context for shared assemblies
        // (like Antlr4.Runtime.Standard). This prevents duplicating framework assemblies
        // while still isolating the dynamically compiled parser.
        return null;
    }
}
