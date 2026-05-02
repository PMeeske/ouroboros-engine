// <copyright file="LoaderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Ouroboros.MeTTa.Ethics;
using Ouroboros.MeTTa.Ethics.Loaders;
using Xunit;

namespace Ouroboros.MeTTa.Tests.Ethics;

public sealed class LoaderTests
{
    public static IEnumerable<object[]> AllLoaders()
    {
        yield return new object[] { (IEthicsLoader)new CoreEthicsLoader() };
        yield return new object[] { (IEthicsLoader)new AhimsaLoader() };
        yield return new object[] { (IEthicsLoader)new BhagavadGitaLoader() };
        yield return new object[] { (IEthicsLoader)new KantianLoader() };
        yield return new object[] { (IEthicsLoader)new LevinasLoader() };
        yield return new object[] { (IEthicsLoader)new MadhyamakaLoader() };
        yield return new object[] { (IEthicsLoader)new UbuntuLoader() };
        yield return new object[] { (IEthicsLoader)new WisdomOfDisagreementLoader() };
        yield return new object[] { (IEthicsLoader)new ParadoxHandler() };
    }

    [Theory]
    [MemberData(nameof(AllLoaders))]
    public void Loader_ParsesAndLoadsAtoms(IEthicsLoader loader)
    {
        AtomSpace space = new();

        var result = loader.Load(space);

        result.IsSuccess.Should().BeTrue($"loader for {loader.Tradition} should succeed");
        result.Value.AtomsLoaded.Should().BeGreaterThan(0);
        result.Value.FingerprintsMatched.Should().BeTrue($"fingerprints for {loader.Tradition} must match the embedded resource");
    }

    [Fact]
    public void EthicsAtomLoader_LoadsAllNineTraditions()
    {
        AtomSpace space = new();
        EthicsAtomLoader aggregate = new();

        var report = aggregate.LoadAll(space);

        report.Loaded.Count.Should().Be(9);
        report.Failed.Should().BeEmpty();
        report.TotalAtoms.Should().BeGreaterThan(50);
        report.PhiProxy.Should().BeApproximately(1.0, 1e-9);
    }
}
