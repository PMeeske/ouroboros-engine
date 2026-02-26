using System.Collections.Immutable;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class TransitionEdgeTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var edge = new TransitionEdge(
            id,
            ImmutableArray.Create(inputId),
            outputId,
            "Critique",
            "{}",
            now,
            confidence: 0.95,
            durationMs: 150);

        edge.Id.Should().Be(id);
        edge.InputIds.Should().ContainSingle().Which.Should().Be(inputId);
        edge.OutputId.Should().Be(outputId);
        edge.OperationName.Should().Be("Critique");
        edge.OperationSpecJson.Should().Be("{}");
        edge.CreatedAt.Should().Be(now);
        edge.Confidence.Should().Be(0.95);
        edge.DurationMs.Should().Be(150);
        edge.Hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Ctor_EmptyInputIds_Throws()
    {
        FluentActions.Invoking(() => new TransitionEdge(
                Guid.NewGuid(),
                ImmutableArray<Guid>.Empty,
                Guid.NewGuid(),
                "op",
                "{}",
                DateTimeOffset.UtcNow))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullOperationName_Throws()
    {
        FluentActions.Invoking(() => new TransitionEdge(
                Guid.NewGuid(),
                ImmutableArray.Create(Guid.NewGuid()),
                Guid.NewGuid(),
                null!,
                "{}",
                DateTimeOffset.UtcNow))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void VerifyHash_ReturnsTrueForValidEdge()
    {
        var edge = TransitionEdge.CreateSimple(
            Guid.NewGuid(), Guid.NewGuid(), "Op", new { Param = "value" });

        edge.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void Create_SetsAllFields()
    {
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();

        var edge = TransitionEdge.Create(
            ImmutableArray.Create(inputId),
            outputId,
            "Transform",
            new { Config = true },
            0.8,
            200);

        edge.InputIds.Should().ContainSingle();
        edge.OutputId.Should().Be(outputId);
        edge.OperationName.Should().Be("Transform");
        edge.Confidence.Should().Be(0.8);
        edge.DurationMs.Should().Be(200);
    }

    [Fact]
    public void CreateSimple_SetsCorrectInputIds()
    {
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();

        var edge = TransitionEdge.CreateSimple(inputId, outputId, "Op", "spec");

        edge.InputIds.Should().ContainSingle().Which.Should().Be(inputId);
    }

    [Fact]
    public void DeserializeOperationSpec_ValidJson_ReturnsSome()
    {
        var edge = TransitionEdge.CreateSimple(
            Guid.NewGuid(), Guid.NewGuid(), "Op", new { Key = "value" });

        var result = edge.DeserializeOperationSpec<Dictionary<string, object>>();
        result.HasValue.Should().BeTrue();
    }
}
