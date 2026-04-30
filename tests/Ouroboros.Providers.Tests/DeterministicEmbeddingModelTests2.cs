namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class DeterministicEmbeddingModelTests2
{
    [Fact]
    public async Task CreateEmbeddingsAsync_ReturnsCorrectDimension()
    {
        var model = new DeterministicEmbeddingModel();
        var result = await model.CreateEmbeddingsAsync("test input");

        result.Should().HaveCount(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_CustomDimension_ReturnsCorrectSize()
    {
        var model = new DeterministicEmbeddingModel(128);
        var result = await model.CreateEmbeddingsAsync("test");

        result.Should().HaveCount(128);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_SameInput_ReturnsSameOutput()
    {
        var model = new DeterministicEmbeddingModel();
        var result1 = await model.CreateEmbeddingsAsync("test input");
        var result2 = await model.CreateEmbeddingsAsync("test input");

        result1.Should().BeEquivalentTo(result2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_DifferentInput_ReturnsDifferentOutput()
    {
        var model = new DeterministicEmbeddingModel();
        var result1 = await model.CreateEmbeddingsAsync("input A");
        var result2 = await model.CreateEmbeddingsAsync("input B");

        result1.Should().NotBeEquivalentTo(result2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_NullInput_HandlesGracefully()
    {
        var model = new DeterministicEmbeddingModel();
        var result = await model.CreateEmbeddingsAsync(null!);

        result.Should().HaveCount(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_EmptyInput_ReturnsVector()
    {
        var model = new DeterministicEmbeddingModel();
        var result = await model.CreateEmbeddingsAsync("");

        result.Should().HaveCount(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_VeryLongInput_HandlesCompression()
    {
        var model = new DeterministicEmbeddingModel();
        var longInput = new string('a', 5000);
        var result = await model.CreateEmbeddingsAsync(longInput);

        result.Should().HaveCount(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_IsNormalized()
    {
        var model = new DeterministicEmbeddingModel();
        var result = await model.CreateEmbeddingsAsync("test text");

        // Check L2 norm is approximately 1
        double magnitude = Math.Sqrt(result.Sum(v => v * v));
        magnitude.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_ZeroDimension_UsesDefault()
    {
        var model = new DeterministicEmbeddingModel(0);
        var result = await model.CreateEmbeddingsAsync("test");

        result.Should().HaveCount(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_NegativeDimension_UsesDefault()
    {
        var model = new DeterministicEmbeddingModel(-5);
        var result = await model.CreateEmbeddingsAsync("test");

        result.Should().HaveCount(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public void DefaultDimension_Is768()
    {
        DeterministicEmbeddingModel.DefaultDimension.Should().Be(768);
    }
}
