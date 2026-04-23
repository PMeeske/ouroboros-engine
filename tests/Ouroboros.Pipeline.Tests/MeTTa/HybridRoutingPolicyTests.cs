using Ouroboros.Pipeline.MeTTa;

namespace Ouroboros.Tests.Pipeline.MeTTa;

[Trait("Category", "Unit")]
public class HybridRoutingPolicyTests
{
    [Fact]
    public void ShouldRouteRemote_PatternMatch_AlwaysTrue()
    {
        var policy = HybridRoutingPolicy.Default;
        Assert.True(policy.ShouldRouteRemote("any", QueryKind.PatternMatch));
    }

    [Fact]
    public void ShouldRouteRemote_PlnInfer_AlwaysTrue()
    {
        var policy = HybridRoutingPolicy.Default;
        Assert.True(policy.ShouldRouteRemote("any", QueryKind.PlnInfer));
    }

    [Theory]
    [InlineData(QueryKind.ExecuteQuery)]
    [InlineData(QueryKind.AddFact)]
    [InlineData(QueryKind.ApplyRule)]
    [InlineData(QueryKind.VerifyPlan)]
    public void ShouldRouteRemote_SimpleShortQuery_Local(QueryKind kind)
    {
        var policy = HybridRoutingPolicy.Default;
        Assert.False(policy.ShouldRouteRemote("(= $x 42)", kind));
    }

    [Fact]
    public void ShouldRouteRemote_ComplexityThreshold_Exceeds()
    {
        var policy = new HybridRoutingPolicy { LocalComplexityThreshold = 50 };
        var longQuery = new string('a', 51);
        Assert.True(policy.ShouldRouteRemote(longQuery, QueryKind.ExecuteQuery));
    }

    [Theory]
    [InlineData("(PlnInfer $a $b)")]
    [InlineData("(match &space $x $y)")]
    [InlineData("(Distinction A B)")]
    [InlineData("(Grounding $x)")]
    [InlineData("(CrossDomain $a $b)")]
    [InlineData("(! (AddAtom $x))")]
    [InlineData("(superpose (A B C))")]
    [InlineData("(collapse (A B C))")]
    public void ShouldRouteRemote_KeywordTriggers_Remote(string query)
    {
        var policy = HybridRoutingPolicy.Default;
        Assert.True(policy.ShouldRouteRemote(query, QueryKind.ExecuteQuery));
    }

    [Fact]
    public void HealthCheckCache_Fresh_ReturnsCached()
    {
        var policy = HybridRoutingPolicy.Default;
        policy.RecordHealthCheck(true);
        Assert.True(policy.IsHealthCheckFresh);
        Assert.True(policy.LastHealthCheck);
    }

    [Fact]
    public void HealthCheckCache_Stale_AfterDuration()
    {
        var policy = new HybridRoutingPolicy { HealthCheckCacheDuration = TimeSpan.FromTicks(1) };
        policy.RecordHealthCheck(true);
        // Force cache expiry by spinning until stale (max 1s to avoid hung test)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (policy.IsHealthCheckFresh && sw.ElapsedMilliseconds < 1000)
        {
            // Busy-wait for cache to expire
        }
        Assert.False(policy.IsHealthCheckFresh);
    }
}
