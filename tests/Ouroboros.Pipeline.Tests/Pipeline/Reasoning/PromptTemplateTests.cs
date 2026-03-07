namespace Ouroboros.Tests.Pipeline.Reasoning;

using Ouroboros.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public class PromptTemplateTests
{
    [Fact]
    public void Format_ReplacesVariables()
    {
        var template = new PromptTemplate("Hello {name}, you are {role}.");
        var vars = new Dictionary<string, string>
        {
            { "name", "John" },
            { "role", "admin" },
        };

        var result = template.Format(vars);

        result.Should().Be("Hello John, you are admin.");
    }

    [Fact]
    public void Format_ThrowsOnNullVars()
    {
        var template = new PromptTemplate("test");
        var act = () => template.Format(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SafeFormat_ReturnsSuccess_WhenAllVarsProvided()
    {
        var template = new PromptTemplate("Hello {name}.");
        var vars = new Dictionary<string, string> { { "name", "World" } };

        var result = template.SafeFormat(vars);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello World.");
    }

    [Fact]
    public void SafeFormat_ReturnsFailure_WhenVarsMissing()
    {
        var template = new PromptTemplate("Hello {name}, {greeting}.");
        var vars = new Dictionary<string, string> { { "name", "World" } };

        var result = template.SafeFormat(vars);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void SafeFormat_ReturnsFailure_WhenVarsNull()
    {
        var template = new PromptTemplate("test");
        var result = template.SafeFormat(null!);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void RequiredVariables_ExtractsPlaceholders()
    {
        var template = new PromptTemplate("{a} and {b} and {a}");

        template.RequiredVariables.Should().Contain("a");
        template.RequiredVariables.Should().Contain("b");
        template.RequiredVariables.Should().HaveCount(2);
    }

    [Fact]
    public void ToString_ReturnsRawTemplate()
    {
        var template = new PromptTemplate("raw {template}");
        template.ToString().Should().Be("raw {template}");
    }

    [Fact]
    public void ImplicitConversion_FromString()
    {
        PromptTemplate template = "Hello {world}";
        template.ToString().Should().Be("Hello {world}");
    }

    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        var act = () => new PromptTemplate(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
