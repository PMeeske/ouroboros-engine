using Ouroboros.Abstractions;
using Ouroboros.Pipeline.MeTTa;

namespace Ouroboros.Tests.Pipeline.MeTTa;

[Trait("Category", "Unit")]
public class HybridMeTTaEngineTests
{
    [Fact]
    public void Constructor_NullLocal_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HybridMeTTaEngine(null!));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        using var local = new FakeLocalEngine();
        using var engine = new HybridMeTTaEngine(local);
        engine.Dispose();
        Assert.True(local.DisposeCalled);
    }

    [Fact]
    public void IsRemoteAvailable_NoRemote_ReturnsFalse()
    {
        using var local = new FakeLocalEngine();
        using var engine = new HybridMeTTaEngine(local);
        Assert.False(engine.IsRemoteAvailable);
    }

    [Fact]
    public async Task ExecuteQuery_SimpleQuery_RoutesLocal()
    {
        using var local = new FakeLocalEngine { QueryResult = "local-result" };
        using var engine = new HybridMeTTaEngine(local);

        var result = await engine.ExecuteQueryAsync("(= $x 42)");

        Assert.True(result.IsSuccess);
        Assert.Equal("local-result", result.Value);
        Assert.Equal(1, local.QueryCount);
    }

    [Fact]
    public async Task AddFactAsync_AlwaysAddsToLocal()
    {
        using var local = new FakeLocalEngine();
        using var engine = new HybridMeTTaEngine(local);

        var result = await engine.AddFactAsync("(Human Socrates)");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, local.AddFactCount);
    }

    [Fact]
    public async Task VerifyPlanAsync_SimplePlan_RoutesLocal()
    {
        using var local = new FakeLocalEngine { VerifyResult = true };
        using var engine = new HybridMeTTaEngine(local);

        var result = await engine.VerifyPlanAsync("(plan step1 step2)");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(1, local.VerifyCount);
    }

    [Fact]
    public async Task ResetAsync_ClearsLocal()
    {
        using var local = new FakeLocalEngine();
        using var engine = new HybridMeTTaEngine(local);

        var result = await engine.ResetAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, local.ResetCount);
    }

    [Fact]
    public async Task PatternMatchAsync_NoRemote_ReturnsFailure()
    {
        using var local = new FakeLocalEngine();
        using var engine = new HybridMeTTaEngine(local);

        var result = await engine.PatternMatchAsync("$x", "(A B)");

        Assert.True(result.IsFailure);
        Assert.Contains("requires remote", result.Error);
    }

    [Fact]
    public async Task PlnInferAsync_NoRemote_ReturnsFailure()
    {
        using var local = new FakeLocalEngine();
        using var engine = new HybridMeTTaEngine(local);

        var result = await engine.PlnInferAsync("(Human Socrates)", "(-> Human Mortal)");

        Assert.True(result.IsFailure);
        Assert.Contains("requires remote", result.Error);
    }

    // --- Fake local engine for testing ---

    private sealed class FakeLocalEngine : IMeTTaEngine
    {
        public string QueryResult { get; set; } = "ok";
        public bool VerifyResult { get; set; } = true;

        public int QueryCount { get; private set; }
        public int AddFactCount { get; private set; }
        public int ApplyRuleCount { get; private set; }
        public int VerifyCount { get; private set; }
        public int ResetCount { get; private set; }
        public bool DisposeCalled { get; private set; }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            QueryCount++;
            return Task.FromResult(Result<string, string>.Success(QueryResult));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            AddFactCount++;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            ApplyRuleCount++;
            return Task.FromResult(Result<string, string>.Success("applied"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            VerifyCount++;
            return Task.FromResult(Result<bool, string>.Success(VerifyResult));
        }

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
        {
            ResetCount++;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public void Dispose() => DisposeCalled = true;
    }
}
