// <copyright file="StdlibMettaParserTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.MeTTa.ConformanceScanner.Spec;
using Xunit;

namespace Ouroboros.MeTTa.ConformanceScanner.Tests;

public sealed class StdlibMettaParserTests
{
    [Fact]
    public void Parse_classifies_colon_and_equals_forms()
    {
        var src = """
(: my-op (-> Atom Atom))
(= (my-op $x) $x)
""";
        var r = new StdlibMettaParser().Parse(src);
        Assert.True(r.IsSuccess);
        Assert.True(r.Value.Operations.ContainsKey("my-op"));
        var schema = r.Value.Operations["my-op"];
        Assert.Single(schema.Signatures);
        Assert.Equal("my-op", schema.Signatures[0].Name);
        Assert.Single(schema.Definitions);
        Assert.Equal("my-op", schema.Definitions[0].Head);
    }
}
