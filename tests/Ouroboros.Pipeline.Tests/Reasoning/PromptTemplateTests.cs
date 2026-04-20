using Ouroboros.Pipeline.Reasoning;

namespace Ouroboros.Tests.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public sealed class PromptTemplateTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidTemplate_CreatesInstance()
    {
        // Arrange & Act
        var template = new PromptTemplate("Hello {name}");

        // Assert
        template.ToString().Should().Be("Hello {name}");
    }

    [Fact]
    public void Constructor_WithNullTemplate_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new PromptTemplate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyTemplate_CreatesInstance()
    {
        // Arrange & Act
        var template = new PromptTemplate(string.Empty);

        // Assert
        template.ToString().Should().BeEmpty();
    }

    #endregion

    #region Format Tests

    [Fact]
    public void Format_WithMatchingVariables_ReplacesPlaceholders()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}, you are {age} years old.");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["age"] = "30"
        };

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("Hello Alice, you are 30 years old.");
    }

    [Fact]
    public void Format_WithNullDictionary_ThrowsArgumentNullException()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}");

        // Act
        Action act = () => template.Format(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Format_WithExtraVariables_IgnoresExtras()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Bob",
            ["extra"] = "ignored"
        };

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("Hello Bob");
    }

    [Fact]
    public void Format_WithMissingVariables_LeavesPlaceholdersUnchanged()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}, welcome to {place}");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Charlie"
        };

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("Hello Charlie, welcome to {place}");
    }

    [Fact]
    public void Format_WithEmptyDictionary_ReturnsTemplateUnchanged()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}");
        var vars = new Dictionary<string, string>();

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("Hello {name}");
    }

    [Fact]
    public void Format_WithNullValue_ReplacesWithEmptyString()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}");
        var vars = new Dictionary<string, string>
        {
            ["name"] = null!
        };

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("Hello ");
    }

    [Fact]
    public void Format_WithNoPlaceholders_ReturnsTemplateUnchanged()
    {
        // Arrange
        var template = new PromptTemplate("Hello World");
        var vars = new Dictionary<string, string> { ["name"] = "Bob" };

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public void Format_WithMultipleOccurrences_ReplacesAll()
    {
        // Arrange
        var template = new PromptTemplate("{x} + {x} = 2*{x}");
        var vars = new Dictionary<string, string> { ["x"] = "5" };

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("5 + 5 = 2*5");
    }

    #endregion

    #region SafeFormat Tests

    [Fact]
    public void SafeFormat_WithAllVariablesProvided_ReturnsSuccess()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}, welcome to {place}");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["place"] = "Wonderland"
        };

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello Alice, welcome to Wonderland");
    }

    [Fact]
    public void SafeFormat_WithMissingVariables_ReturnsFailure()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}, you are {age} from {city}");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice"
        };

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Missing required variables");
        result.Error.Should().Contain("age");
        result.Error.Should().Contain("city");
    }

    [Fact]
    public void SafeFormat_WithNullDictionary_ReturnsFailure()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}");

        // Act
        var result = template.SafeFormat(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public void SafeFormat_WithNoPlaceholders_ReturnsSuccess()
    {
        // Arrange
        var template = new PromptTemplate("Hello World");
        var vars = new Dictionary<string, string>();

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello World");
    }

    [Fact]
    public void SafeFormat_WithExtraVariables_ReturnsSuccess()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["extra"] = "value"
        };

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello Alice");
    }

    #endregion

    #region RequiredVariables Tests

    [Fact]
    public void RequiredVariables_WithPlaceholders_ReturnsPlaceholderNames()
    {
        // Arrange
        var template = new PromptTemplate("{greeting} {name}, welcome to {place}");

        // Act
        var variables = template.RequiredVariables;

        // Assert
        variables.Should().HaveCount(3);
        variables.Should().Contain("greeting");
        variables.Should().Contain("name");
        variables.Should().Contain("place");
    }

    [Fact]
    public void RequiredVariables_WithNoPlaceholders_ReturnsEmptyList()
    {
        // Arrange
        var template = new PromptTemplate("No placeholders here");

        // Act
        var variables = template.RequiredVariables;

        // Assert
        variables.Should().BeEmpty();
    }

    [Fact]
    public void RequiredVariables_WithDuplicatePlaceholders_ReturnsDistinct()
    {
        // Arrange
        var template = new PromptTemplate("{x} + {x} = 2*{x}");

        // Act
        var variables = template.RequiredVariables;

        // Assert
        variables.Should().HaveCount(1);
        variables.Should().Contain("x");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsRawTemplate()
    {
        // Arrange
        var raw = "Hello {name}, this is a template";
        var template = new PromptTemplate(raw);

        // Act
        string result = template.ToString();

        // Assert
        result.Should().Be(raw);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_FromString_CreatesPromptTemplate()
    {
        // Arrange & Act
        PromptTemplate template = "Hello {name}";

        // Assert
        template.ToString().Should().Be("Hello {name}");
    }

    [Fact]
    public void ImplicitConversion_CanBeUsedInFormat()
    {
        // Arrange
        PromptTemplate template = "Hello {name}";
        var vars = new Dictionary<string, string> { ["name"] = "World" };

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("Hello World");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Format_WithNestedBraces_HandlesGracefully()
    {
        // Arrange - single-level braces only
        var template = new PromptTemplate("{outer}");
        var vars = new Dictionary<string, string> { ["outer"] = "value" };

        // Act
        string result = template.Format(vars);

        // Assert
        result.Should().Be("value");
    }

    [Fact]
    public void Format_WithEmptyPlaceholderName_IgnoresEmptyPlaceholder()
    {
        // Arrange - empty braces {} should be ignored by ExtractPlaceholders
        var template = new PromptTemplate("Hello {} World");
        var vars = new Dictionary<string, string>();

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        // Empty placeholder names are filtered by IsNullOrWhiteSpace check
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}
