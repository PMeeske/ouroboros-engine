namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ResponseCandidateTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var candidate = ResponseCandidate<string>.Create("hello", "gpt-4", TimeSpan.FromMilliseconds(100));

        candidate.Value.Should().Be("hello");
        candidate.Source.Should().Be("gpt-4");
        candidate.Latency.Should().Be(TimeSpan.FromMilliseconds(100));
        candidate.Score.Should().Be(0.0);
        candidate.IsValid.Should().BeTrue();
        candidate.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_SetsIsValidFalse()
    {
        var candidate = ResponseCandidate<string>.Invalid("broken-model");

        candidate.IsValid.Should().BeFalse();
        candidate.Source.Should().Be("broken-model");
    }

    [Fact]
    public void WithScore_ReturnsNewInstanceWithScore()
    {
        var original = ResponseCandidate<string>.Create("test", "src", TimeSpan.Zero);

        var scored = original.WithScore(0.95);

        scored.Score.Should().Be(0.95);
        scored.Value.Should().Be("test");
    }

    [Fact]
    public void WithMetrics_ReturnsNewInstanceWithMetrics()
    {
        var original = ResponseCandidate<string>.Create("test", "src", TimeSpan.Zero);
        var metrics = new Dictionary<string, double> { ["quality"] = 0.9 };

        var withMetrics = original.WithMetrics(metrics);

        withMetrics.Metrics.Should().ContainKey("quality");
        withMetrics.Metrics["quality"].Should().Be(0.9);
    }

    [Fact]
    public void Select_ValidCandidate_TransformsValue()
    {
        var candidate = ResponseCandidate<int>.Create(5, "src", TimeSpan.Zero);

        var mapped = candidate.Select(x => x * 2);

        mapped.Value.Should().Be(10);
        mapped.IsValid.Should().BeTrue();
        mapped.Source.Should().Be("src");
    }

    [Fact]
    public void Select_InvalidCandidate_ReturnsInvalid()
    {
        var candidate = ResponseCandidate<int>.Invalid("src");

        var mapped = candidate.Select(x => x * 2);

        mapped.IsValid.Should().BeFalse();
        mapped.Source.Should().Be("src");
    }

    [Fact]
    public void SelectMany_ValidCandidate_ChainsOperation()
    {
        var candidate = ResponseCandidate<int>.Create(5, "src", TimeSpan.Zero);

        var result = candidate.SelectMany(x =>
            ResponseCandidate<string>.Create(x.ToString(), "derived", TimeSpan.FromMilliseconds(50)));

        result.IsValid.Should().BeTrue();
        result.Value.Should().Be("5");
    }

    [Fact]
    public void SelectMany_InvalidCandidate_ReturnsInvalid()
    {
        var candidate = ResponseCandidate<int>.Invalid("src");

        var result = candidate.SelectMany(x =>
            ResponseCandidate<string>.Create(x.ToString(), "derived", TimeSpan.Zero));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SelectMany_LinqComposition_CombinesLatencies()
    {
        var candidate = ResponseCandidate<int>.Create(5, "src", TimeSpan.FromMilliseconds(100));

        var result = candidate.SelectMany(
            x => ResponseCandidate<int>.Create(x + 10, "src2", TimeSpan.FromMilliseconds(200)),
            (a, b) => a + b);

        result.IsValid.Should().BeTrue();
        result.Value.Should().Be(20); // 5 + (5+10)
        result.Latency.Should().Be(TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public void SelectMany_LinqComposition_InvalidOriginal_ReturnsInvalid()
    {
        var candidate = ResponseCandidate<int>.Invalid("src");

        var result = candidate.SelectMany(
            x => ResponseCandidate<int>.Create(x + 10, "src2", TimeSpan.Zero),
            (a, b) => a + b);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SelectMany_LinqComposition_InvalidIntermediate_ReturnsInvalid()
    {
        var candidate = ResponseCandidate<int>.Create(5, "src", TimeSpan.Zero);

        var result = candidate.SelectMany(
            _ => ResponseCandidate<int>.Invalid("src2"),
            (a, b) => a + b);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Where_PredicateTrue_ReturnsSameCandidate()
    {
        var candidate = ResponseCandidate<int>.Create(10, "src", TimeSpan.Zero);

        var filtered = candidate.Where(x => x > 5);

        filtered.IsValid.Should().BeTrue();
        filtered.Value.Should().Be(10);
    }

    [Fact]
    public void Where_PredicateFalse_ReturnsInvalid()
    {
        var candidate = ResponseCandidate<int>.Create(3, "src", TimeSpan.Zero);

        var filtered = candidate.Where(x => x > 5);

        filtered.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Where_AlreadyInvalid_ReturnsInvalid()
    {
        var candidate = ResponseCandidate<int>.Invalid("src");

        var filtered = candidate.Where(x => x > 0);

        filtered.IsValid.Should().BeFalse();
    }
}
