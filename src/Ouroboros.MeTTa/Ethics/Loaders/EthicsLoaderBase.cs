// <copyright file="EthicsLoaderBase.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Shared base for the 9 tradition-specific loaders plus
/// <see cref="EthicsAtomLoader"/>. Subclasses declare their tradition,
/// resource name, and a small set of fingerprint substrings used to
/// validate the loaded file is the expected one.
/// </summary>
public abstract class EthicsLoaderBase : IEthicsLoader
{
    private static readonly Assembly EmbeddedAssembly = typeof(EthicsLoaderBase).Assembly;

    /// <inheritdoc/>
    public abstract EthicsTradition Tradition { get; }

    /// <summary>
    /// Gets the name of the embedded resource for this tradition.
    /// </summary>
    protected abstract string ResourceName { get; }

    /// <summary>
    /// Gets case-insensitive substrings that must appear in the loaded
    /// MeTTa source for the load to be considered authentic.
    /// </summary>
    protected abstract IReadOnlyList<string> Fingerprints { get; }

    /// <inheritdoc/>
    public Result<EthicsLoadResult, string> Load(IAtomSpace space)
    {
        ArgumentNullException.ThrowIfNull(space);

        string? source = ReadEmbeddedResource(this.ResourceName);
        if (source is null)
        {
            return Result<EthicsLoadResult, string>.Failure(
                $"Embedded resource not found: {this.ResourceName}");
        }

        bool fingerprintsMatched = ValidateFingerprints(source, this.Fingerprints);

        IReadOnlyList<Atom> atoms;
        try
        {
            atoms = MeTTaLineParser.Parse(source);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<EthicsLoadResult, string>.Failure(
                $"Parse failed for {this.Tradition}: {ex.Message}");
        }

        int loaded = 0;
        foreach (Atom atom in atoms)
        {
            if (space.Add(atom))
            {
                loaded++;
            }
        }

        return Result<EthicsLoadResult, string>.Success(
            new EthicsLoadResult(this.Tradition, loaded, fingerprintsMatched, this.ResourceName));
    }

    private static string? ReadEmbeddedResource(string resourceName)
    {
        using Stream? stream = EmbeddedAssembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static bool ValidateFingerprints(string source, IReadOnlyList<string> fingerprints)
    {
        if (fingerprints.Count == 0)
        {
            return true;
        }

        foreach (string fp in fingerprints)
        {
            if (source.IndexOf(fp, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }
}
