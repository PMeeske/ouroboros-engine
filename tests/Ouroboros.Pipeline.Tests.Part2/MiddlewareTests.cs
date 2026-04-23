namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Middleware;

[Trait("Category", "Unit")]
public class PipelineContextTests
{
    #region Creation

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var context = new PipelineContext("input", new Dictionary<string, object>());

        // Assert
        context.Input.Should().Be("input");
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void FromInput_ShouldCreateWithEmptyMetadata()
    {
        // Act
        var context = PipelineContext.FromInput("test input");

        // Assert
        context.Input.Should().Be("test input");
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void FromInput_NullInput_ShouldAllowNull()
    {
        // Act
        var context = PipelineContext.FromInput(null!);

        // Assert
        context.Input.Should().BeNull();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class PipelineResultTests
{
    #region Factory Methods

    [Fact]
    public void Successful_ShouldCreateSuccessResult()
    {
        // Act
        var result = PipelineResult.Successful("output");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failed_ShouldCreateFailureResult()
    {
        // Arrange
        var exception = new InvalidOperationException("test error");

        // Act
        var result = PipelineResult.Failed(exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Output.Should().BeNull();
        result.Error.Should().Be(exception);
        result.Error!.Message.Should().Be("test error");
    }

    #endregion

    #region Properties

    [Fact]
    public void Properties_ShouldBeAccessible()
    {
        // Arrange
        var result = new PipelineResult(true, "output", null);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void FailedResult_WithNullError_ShouldStillBeFailure()
    {
        // Arrange & Act
        var result = new PipelineResult(false, null, null);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    #endregion
}
