using FluentAssertions;
using Ouroboros.Pipeline.Grammar;
using Xunit;

namespace Ouroboros.Tests.Pipeline.Grammar;

[Trait("Category", "Unit")]
public class GrammarCompilationExceptionTests
{
    [Fact]
    public void Constructor_WithDiagnostics_SetsProperties()
    {
        var diagnostics = new List<string> { "error CS0001", "error CS0002" };

        var ex = new GrammarCompilationException("Compilation failed", CompilationStage.RoslynCompilation, diagnostics);

        ex.Message.Should().Be("Compilation failed");
        ex.Stage.Should().Be(CompilationStage.RoslynCompilation);
        ex.Diagnostics.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_WithNullDiagnostics_DefaultsToEmpty()
    {
        var ex = new GrammarCompilationException("Failed", CompilationStage.AntlrCodeGeneration);

        ex.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("Inner error");

        var ex = new GrammarCompilationException("Outer error", CompilationStage.AssemblyLoading, inner);

        ex.InnerException.Should().Be(inner);
        ex.Stage.Should().Be(CompilationStage.AssemblyLoading);
        ex.Diagnostics.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class CompilationStageTests
{
    [Theory]
    [InlineData(CompilationStage.AntlrCodeGeneration)]
    [InlineData(CompilationStage.RoslynCompilation)]
    [InlineData(CompilationStage.AssemblyLoading)]
    [InlineData(CompilationStage.ParserInstantiation)]
    public void AllValues_AreDefined(CompilationStage value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void CompilationStage_HasFourValues()
    {
        Enum.GetValues<CompilationStage>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class GrammarIssueTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var issue = new GrammarIssue(GrammarIssueSeverity.Error, "expr", "Left recursion detected", GrammarIssueKind.LeftRecursion);

        issue.Severity.Should().Be(GrammarIssueSeverity.Error);
        issue.RuleName.Should().Be("expr");
        issue.Description.Should().Be("Left recursion detected");
        issue.Kind.Should().Be(GrammarIssueKind.LeftRecursion);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new GrammarIssue(GrammarIssueSeverity.Warning, "r", "d", GrammarIssueKind.Ambiguity);
        var b = new GrammarIssue(GrammarIssueSeverity.Warning, "r", "d", GrammarIssueKind.Ambiguity);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class GrammarIssueSeverityTests
{
    [Theory]
    [InlineData(GrammarIssueSeverity.Warning)]
    [InlineData(GrammarIssueSeverity.Error)]
    public void AllValues_AreDefined(GrammarIssueSeverity value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void GrammarIssueSeverity_HasTwoValues()
    {
        Enum.GetValues<GrammarIssueSeverity>().Should().HaveCount(2);
    }
}

[Trait("Category", "Unit")]
public class GrammarIssueKindTests
{
    [Theory]
    [InlineData(GrammarIssueKind.Unspecified)]
    [InlineData(GrammarIssueKind.LeftRecursion)]
    [InlineData(GrammarIssueKind.UnreachableRule)]
    [InlineData(GrammarIssueKind.FirstSetConflict)]
    [InlineData(GrammarIssueKind.MissingRule)]
    [InlineData(GrammarIssueKind.SyntaxError)]
    [InlineData(GrammarIssueKind.Ambiguity)]
    public void AllValues_AreDefined(GrammarIssueKind value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void GrammarIssueKind_HasSevenValues()
    {
        Enum.GetValues<GrammarIssueKind>().Should().HaveCount(7);
    }
}

[Trait("Category", "Unit")]
public class GrammarValidationResultTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Warning, "rule1", "desc", GrammarIssueKind.Ambiguity)
        };

        var result = new GrammarValidationResult(true, issues);

        result.IsValid.Should().BeTrue();
        result.Issues.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WithNoIssues_IsValid()
    {
        var result = new GrammarValidationResult(true, Array.Empty<GrammarIssue>());

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class GrammarCorrectionResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var corrections = new List<string> { "Removed left recursion" };
        var remaining = new List<GrammarIssue>();

        var result = new GrammarCorrectionResult(true, "grammar Foo;", corrections, remaining);

        result.Success.Should().BeTrue();
        result.CorrectedGrammarG4.Should().Be("grammar Foo;");
        result.CorrectionsApplied.Should().HaveCount(1);
        result.RemainingIssues.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class GrammarRequestTests
{
    [Fact]
    public void Constructor_WithDefaults_SetsProperties()
    {
        var request = new GrammarRequest("Parse JSON");

        request.Description.Should().Be("Parse JSON");
        request.SampleInput.Should().BeNull();
        request.MaxAttempts.Should().Be(5);
    }

    [Fact]
    public void Constructor_WithAllParams_SetsProperties()
    {
        var request = new GrammarRequest("Parse CSV", "a,b,c", 10);

        request.Description.Should().Be("Parse CSV");
        request.SampleInput.Should().Be("a,b,c");
        request.MaxAttempts.Should().Be(10);
    }
}
