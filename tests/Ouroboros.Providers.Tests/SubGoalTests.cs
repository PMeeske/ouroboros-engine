namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class SubGoalTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var deps = new List<string> { "goal_1" };
        var goal = new SubGoal("goal_2", "Implement feature", SubGoalComplexity.Moderate,
            SubGoalType.Coding, deps, PathwayTier.Specialized);

        goal.Id.Should().Be("goal_2");
        goal.Description.Should().Be("Implement feature");
        goal.Complexity.Should().Be(SubGoalComplexity.Moderate);
        goal.Type.Should().Be(SubGoalType.Coding);
        goal.Dependencies.Should().ContainSingle().Which.Should().Be("goal_1");
        goal.PreferredTier.Should().Be(PathwayTier.Specialized);
    }

    [Fact]
    public void FromDescription_ShortText_InfersSimple()
    {
        var goal = SubGoal.FromDescription("find X");

        goal.Complexity.Should().Be(SubGoalComplexity.Simple);
    }

    [Fact]
    public void FromDescription_CodingKeyword_InfersCodingType()
    {
        var goal = SubGoal.FromDescription("implement a function to parse JSON data and handle errors properly");

        goal.Type.Should().Be(SubGoalType.Coding);
    }

    [Fact]
    public void FromDescription_MathKeyword_InfersMathType()
    {
        var goal = SubGoal.FromDescription("calculate the area under the curve using integral formula");

        goal.Type.Should().Be(SubGoalType.Math);
    }

    [Fact]
    public void FromDescription_CreativeKeyword_InfersCreativeType()
    {
        var goal = SubGoal.FromDescription("write a compelling story about a journey through time and space");

        goal.Type.Should().Be(SubGoalType.Creative);
    }

    [Fact]
    public void FromDescription_ReasoningKeyword_InfersReasoningType()
    {
        var goal = SubGoal.FromDescription("analyze the market trends and compare them with historical data over many years");

        goal.Type.Should().Be(SubGoalType.Reasoning);
    }

    [Fact]
    public void FromDescription_TransformKeyword_InfersTransformType()
    {
        var goal = SubGoal.FromDescription("convert the CSV file to JSON format for further processing in the pipeline");

        goal.Type.Should().Be(SubGoalType.Transform);
    }

    [Fact]
    public void FromDescription_RetrievalKeyword_InfersRetrievalType()
    {
        var goal = SubGoal.FromDescription("find information about the latest release of the software product updates");

        goal.Type.Should().Be(SubGoalType.Retrieval);
    }

    [Fact]
    public void FromDescription_NoSpecificKeyword_DefaultsToReasoning()
    {
        var goal = SubGoal.FromDescription("handle this interesting task about organizing data for better downstream usage");

        goal.Type.Should().Be(SubGoalType.Reasoning);
    }

    [Fact]
    public void FromDescription_SetsIdWithIndex()
    {
        var goal = SubGoal.FromDescription("test", 3);

        goal.Id.Should().Be("goal_4");
    }

    [Fact]
    public void FromDescription_EmptyDependencies()
    {
        var goal = SubGoal.FromDescription("simple task");

        goal.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void FromDescription_LongText_InfersHigherComplexity()
    {
        var longText = new string('a', 250) + " then also compute " + new string('b', 100);
        var goal = SubGoal.FromDescription(longText);

        goal.Complexity.Should().BeOneOf(SubGoalComplexity.Complex, SubGoalComplexity.Expert);
    }

    [Fact]
    public void FromDescription_SimpleCoding_InfersLocalTier()
    {
        var goal = SubGoal.FromDescription("code X");

        goal.Complexity.Should().Be(SubGoalComplexity.Simple);
        goal.PreferredTier.Should().Be(PathwayTier.Local);
    }

    [Fact]
    public void FromDescription_ComplexCoding_InfersSpecializedTier()
    {
        var goal = SubGoal.FromDescription(
            "implement a complex function that handles edge cases and refactor the existing codebase for better performance and maintainability across multiple modules");

        goal.PreferredTier.Should().Be(PathwayTier.Specialized);
    }

    [Fact]
    public void FromDescription_ComplexCreative_InfersCloudPremiumTier()
    {
        var goal = SubGoal.FromDescription(
            "write a detailed creative story exploring themes of consciousness and identity in a futuristic world where artificial intelligence has become sentient");

        goal.PreferredTier.Should().Be(PathwayTier.CloudPremium);
    }
}
