namespace Ouroboros.Tests.Pipeline.Reasoning;

using Ouroboros.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public class OperatingCostAuditPromptsTests
{
    [Fact]
    public void SystemPrompt_IsNotNull()
    {
        OperatingCostAuditPrompts.SystemPrompt.Should().NotBeNull();
    }

    [Fact]
    public void AnalyzeMainStatement_ContainsRequiredVariables()
    {
        var template = OperatingCostAuditPrompts.AnalyzeMainStatement;
        template.RequiredVariables.Should().Contain("main_statement");
    }

    [Fact]
    public void CompareWithHoaStatement_ContainsRequiredVariables()
    {
        var template = OperatingCostAuditPrompts.CompareWithHoaStatement;
        template.RequiredVariables.Should().Contain("main_statement");
        template.RequiredVariables.Should().Contain("hoa_statement");
    }

    [Fact]
    public void CheckAllocationRules_ContainsRequiredVariables()
    {
        var template = OperatingCostAuditPrompts.CheckAllocationRules;
        template.RequiredVariables.Should().Contain("rental_agreement_rules");
    }

    [Fact]
    public void GenerateAuditReport_ContainsRequiredVariables()
    {
        var template = OperatingCostAuditPrompts.GenerateAuditReport;
        template.RequiredVariables.Should().Contain("analysis_results");
    }

    [Fact]
    public void ExtractCostCategories_ContainsRequiredVariables()
    {
        var template = OperatingCostAuditPrompts.ExtractCostCategories;
        template.RequiredVariables.Should().Contain("main_statement");
    }
}
