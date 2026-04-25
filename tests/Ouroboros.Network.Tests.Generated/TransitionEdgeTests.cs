namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class TransitionEdgeTests
{
    #region Construction

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputIds = ImmutableArray.Create(Guid.NewGuid());
        var outputId = Guid.NewGuid();
        var operationName = "UseCritique";
        var operationSpecJson = "{\"param\":1}";
        var createdAt = DateTimeOffset.UtcNow;
        var confidence = 0.95;
        var durationMs = 150L;

        // Act
        var edge = new TransitionEdge(id, inputIds, outputId, operationName, operationSpecJson, createdAt, confidence, durationMs);

        // Assert
        edge.Id.Should().Be(id);
        edge.InputIds.Should().Equal(inputIds);
        edge.OutputId.Should().Be(outputId);
        edge.OperationName.Should().Be(operationName);
        edge.OperationSpecJson.Should().Be(operationSpecJson);
        edge.CreatedAt.Should().Be(createdAt);
        edge.Confidence.Should().Be(confidence);
        edge.DurationMs.Should().Be(durationMs);
        edge.Hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_DefaultOptionalParameters_AreNull()
    {
        // Act
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        edge.Confidence.Should().BeNull();
        edge.DurationMs.Should().BeNull();
    }

    [Fact]
    public void Constructor_EmptyInputIds_ThrowsArgumentException()
    {
        // Act
        Action act = () => new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray<Guid>.Empty,
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("inputIds");
    }

    [Fact]
    public void Constructor_NullOperationName_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            null!,
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("operationName");
    }

    [Fact]
    public void Constructor_NullOperationSpecJson_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "Op",
            null!,
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("operationSpecJson");
    }

    [Fact]
    public void Constructor_MultipleInputIds_IsValid()
    {
        // Arrange
        var inputIds = ImmutableArray.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            inputIds,
            Guid.NewGuid(),
            "Merge",
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        edge.InputIds.Should().HaveCount(3);
    }

    #endregion

    #region Create

    [Fact]
    public void Create_SerializesSpecAndCreatesEdge()
    {
        // Arrange
        var inputIds = ImmutableArray.Create(Guid.NewGuid());
        var outputId = Guid.NewGuid();
        var spec = new { Param = 42, Name = "test" };

        // Act
        var edge = TransitionEdge.Create(inputIds, outputId, "TestOp", spec, 0.8, 200L);

        // Assert
        edge.OperationSpecJson.Should().Contain("42");
        edge.OperationSpecJson.Should().Contain("test");
        edge.Confidence.Should().Be(0.8);
        edge.DurationMs.Should().Be(200L);
    }

    [Fact]
    public void Create_GeneratesNewGuid()
    {
        // Act
        var edge = TransitionEdge.Create(
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "Op",
            new { });

        // Assert
        edge.Id.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region CreateSimple

    [Fact]
    public void CreateSimple_CreatesEdgeWithSingleInput()
    {
        // Arrange
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var spec = new { X = 1 };

        // Act
        var edge = TransitionEdge.CreateSimple(inputId, outputId, "SimpleOp", spec);

        // Assert
        edge.InputIds.Should().ContainSingle().Which.Should().Be(inputId);
        edge.OutputId.Should().Be(outputId);
    }

    #endregion

    #region DeserializeOperationSpec

    [Fact]
    public void DeserializeOperationSpec_ValidJson_ReturnsSome()
    {
        // Arrange
        var spec = new TestSpec { Name = "Test", Value = 42 };
        var edge = TransitionEdge.Create(
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "Op",
            spec);

        // Act
        var result = edge.DeserializeOperationSpec<TestSpec>();

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Name.Should().Be("Test");
        result.Value.Value.Should().Be(42);
    }

    [Fact]
    public void DeserializeOperationSpec_InvalidJson_ReturnsNone()
    {
        // Arrange
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "Op",
            "not-json",
            DateTimeOffset.UtcNow);

        // Act
        var result = edge.DeserializeOperationSpec<TestSpec>();

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region VerifyHash

    [Fact]
    public void VerifyHash_ValidEdge_ReturnsTrue()
    {
        // Arrange
        var edge = TransitionEdge.CreateSimple(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Op",
            new { });

        // Act
        var result = edge.VerifyHash();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_HashChangesWithConfidence()
    {
        // Arrange
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var spec = new { };

        // Act
        var edge1 = TransitionEdge.CreateSimple(inputId, outputId, "Op", spec, 0.5, null);
        var edge2 = TransitionEdge.CreateSimple(inputId, outputId, "Op", spec, 0.9, null);

        // Assert
        edge1.Hash.Should().NotBe(edge2.Hash);
    }

    [Fact]
    public void VerifyHash_HashChangesWithDuration()
    {
        // Arrange
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var spec = new { };

        // Act
        var edge1 = TransitionEdge.CreateSimple(inputId, outputId, "Op", spec, null, 100L);
        var edge2 = TransitionEdge.CreateSimple(inputId, outputId, "Op", spec, null, 200L);

        // Assert
        edge1.Hash.Should().NotBe(edge2.Hash);
    }

    #endregion

    private sealed record TestSpec(string Name, int Value);
}
