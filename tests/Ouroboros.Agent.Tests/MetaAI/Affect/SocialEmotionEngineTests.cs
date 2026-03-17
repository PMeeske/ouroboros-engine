using FluentAssertions;
using Ouroboros.Agent.MetaAI.Affect;
using Xunit;

namespace Ouroboros.Tests.MetaAI.Affect;

[Trait("Category", "Unit")]
public class SocialEmotionEngineTests
{
    private readonly SocialEmotionEngine _sut;

    public SocialEmotionEngineTests()
    {
        _sut = new SocialEmotionEngine();
    }

    #region EvaluateSocialSituationAsync Tests

    [Fact]
    public async Task EvaluateSocialSituationAsync_NullSituation_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EvaluateSocialSituationAsync(null!, "some state");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateSocialSituationAsync_NullOtherAgentState_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EvaluateSocialSituationAsync("situation", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateSocialSituationAsync_CancelledToken_ReturnsFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.EvaluateSocialSituationAsync("situation", "state", cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("I hurt someone accidentally", "upset", SocialEmotionType.Guilt)]
    [InlineData("The agent caused harm to the system", "distressed", SocialEmotionType.Guilt)]
    [InlineData("I failed the test publicly", "embarrassed", SocialEmotionType.Shame)]
    [InlineData("The agent was exposed for errors", "humiliated", SocialEmotionType.Shame)]
    [InlineData("I achieved the goal successfully", "proud", SocialEmotionType.Pride)]
    [InlineData("Great accomplishment in the project", "happy", SocialEmotionType.Pride)]
    [InlineData("Someone helped me solve the problem", "grateful", SocialEmotionType.Gratitude)]
    [InlineData("Thank you for the gift", "happy", SocialEmotionType.Gratitude)]
    [InlineData("The other agent is very sad", "depressed", SocialEmotionType.Empathy)]
    [InlineData("They are in pain and suffering", "distressed", SocialEmotionType.Empathy)]
    [InlineData("That's unfair and I'm jealous", "envious", SocialEmotionType.Jealousy)]
    public async Task EvaluateSocialSituationAsync_ClassifiesEmotionCorrectly(
        string situation, string otherState, SocialEmotionType expected)
    {
        // Act
        var result = await _sut.EvaluateSocialSituationAsync(situation, otherState);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(expected);
    }

    [Fact]
    public async Task EvaluateSocialSituationAsync_DefaultClassification_ReturnsCompassion()
    {
        // Arrange — no trigger keywords
        var situation = "I observed something happening";
        var otherState = "neutral";

        // Act
        var result = await _sut.EvaluateSocialSituationAsync(situation, otherState);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(SocialEmotionType.Compassion);
    }

    [Fact]
    public async Task EvaluateSocialSituationAsync_IntensityClampedBetweenZeroAndOne()
    {
        // Act
        var result = await _sut.EvaluateSocialSituationAsync(
            "A terribly devastating situation that extremely affects everyone",
            "deeply affected");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Intensity.Should().BeGreaterThanOrEqualTo(0.0);
        result.Value.Intensity.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task EvaluateSocialSituationAsync_StrongWordsBoostIntensity()
    {
        // Arrange
        var mildSituation = "Something happened";
        var strongSituation = "Something extremely terrible and devastating happened";

        // Act
        var mildResult = await _sut.EvaluateSocialSituationAsync(mildSituation, "neutral");
        var strongResult = await _sut.EvaluateSocialSituationAsync(strongSituation, "neutral");

        // Assert
        strongResult.Value.Intensity.Should().BeGreaterThan(mildResult.Value.Intensity);
    }

    [Fact]
    public async Task EvaluateSocialSituationAsync_AddsEmotionToActiveList()
    {
        // Arrange
        _sut.GetActiveEmotions().Should().BeEmpty();

        // Act
        await _sut.EvaluateSocialSituationAsync("I hurt them", "upset");

        // Assert
        _sut.GetActiveEmotions().Should().HaveCount(1);
    }

    [Fact]
    public async Task EvaluateSocialSituationAsync_SetsTimestampAndId()
    {
        // Act
        var result = await _sut.EvaluateSocialSituationAsync("I helped them", "grateful");

        // Assert
        result.Value.Id.Should().NotBeNullOrEmpty();
        result.Value.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Value.Trigger.Should().Be("I helped them");
        result.Value.Context.Should().Be("grateful");
    }

    #endregion

    #region RegulateEmotionAsync Tests

    [Fact]
    public async Task RegulateEmotionAsync_NullEmotion_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.RegulateEmotionAsync(null!, RegulationStrategy.Reappraisal);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RegulateEmotionAsync_CancelledToken_ReturnsFailure()
    {
        // Arrange
        var emotion = new SocialEmotion("e1", SocialEmotionType.Guilt, 0.8, "trigger", "ctx", DateTime.UtcNow);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.RegulateEmotionAsync(emotion, RegulationStrategy.Reappraisal, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RegulateEmotionAsync_Reappraisal_ReducesGuiltIntensity()
    {
        // Arrange
        var emotion = new SocialEmotion("e1", SocialEmotionType.Guilt, 0.9, "harm", "ctx", DateTime.UtcNow);

        // Act
        var result = await _sut.RegulateEmotionAsync(emotion, RegulationStrategy.Reappraisal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RegulatedIntensity.Should().BeLessThan(result.Value.OriginalIntensity);
        result.Value.OriginalEmotion.Should().Be(SocialEmotionType.Guilt);
        result.Value.UsedStrategy.Should().Be(RegulationStrategy.Reappraisal);
    }

    [Fact]
    public async Task RegulateEmotionAsync_HighEffectiveness_ReducesBelowHalf()
    {
        // Arrange — Reappraisal + Guilt has 0.8 effectiveness
        var emotion = new SocialEmotion("e1", SocialEmotionType.Guilt, 0.6, "harm", "ctx", DateTime.UtcNow);

        // Act
        var result = await _sut.RegulateEmotionAsync(emotion, RegulationStrategy.Reappraisal);

        // Assert
        // 0.6 * (1 - 0.8) = 0.12, which is < 0.5
        result.Value.RegulatedIntensity.Should().BeLessThan(0.5);
        result.Value.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RegulateEmotionAsync_LowEffectiveness_MayNotSucceed()
    {
        // Arrange — Suppression + Guilt has 0.3 effectiveness
        var emotion = new SocialEmotion("e1", SocialEmotionType.Guilt, 0.9, "harm", "ctx", DateTime.UtcNow);

        // Act
        var result = await _sut.RegulateEmotionAsync(emotion, RegulationStrategy.Suppression);

        // Assert
        // 0.9 * (1 - 0.3) = 0.63, which is > 0.5
        result.Value.RegulatedIntensity.Should().BeGreaterThan(0.5);
        result.Value.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RegulateEmotionAsync_UnknownStrategyEmotionPair_UsesDefaultEffectiveness()
    {
        // Arrange — Pride + Suppression not in matrix, defaults to 0.25
        var emotion = new SocialEmotion("e1", SocialEmotionType.Pride, 0.8, "achievement", "ctx", DateTime.UtcNow);

        // Act
        var result = await _sut.RegulateEmotionAsync(emotion, RegulationStrategy.Suppression);

        // Assert
        // 0.8 * (1 - 0.25) = 0.6
        result.Value.RegulatedIntensity.Should().BeApproximately(0.6, 0.01);
    }

    [Fact]
    public async Task RegulateEmotionAsync_UpdatesActiveEmotionIntensity()
    {
        // Arrange — first add the emotion via EvaluateSocialSituation
        var evalResult = await _sut.EvaluateSocialSituationAsync("I hurt someone", "upset");
        var emotion = evalResult.Value;

        // Act
        await _sut.RegulateEmotionAsync(emotion, RegulationStrategy.Reappraisal);

        // Assert
        var activeEmotions = _sut.GetActiveEmotions();
        activeEmotions.Should().ContainSingle();
        activeEmotions[0].Intensity.Should().BeLessThan(emotion.Intensity);
    }

    #endregion

    #region GenerateEmpathyAsync Tests

    [Fact]
    public async Task GenerateEmpathyAsync_NullTargetAgentId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.GenerateEmpathyAsync(null!, "sad", "ctx");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateEmpathyAsync_NullOtherAgentEmotion_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.GenerateEmpathyAsync("agent-1", null!, "ctx");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateEmpathyAsync_CancelledToken_ReturnsFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.GenerateEmpathyAsync("agent-1", "sad", "ctx", cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEmpathyAsync_ValidInput_ReturnsEmpathyResponse()
    {
        // Act
        var result = await _sut.GenerateEmpathyAsync("agent-42", "deeply sad and depressed", "interaction context");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TargetAgent.Should().Be("agent-42");
        result.Value.PerceivedEmotion.Should().Be("deeply sad and depressed");
        result.Value.ResonanceStrength.Should().BeGreaterThan(0.0);
        result.Value.SuggestedResponse.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateEmpathyAsync_EmptyEmotion_LowResonance()
    {
        // Act
        var result = await _sut.GenerateEmpathyAsync("agent-1", " ", "ctx");

        // Assert
        result.Value.ResonanceStrength.Should().Be(0.2);
    }

    [Fact]
    public async Task GenerateEmpathyAsync_HighResonance_GeneratesAppropriatePhrase()
    {
        // Arrange — long emotion string to produce high resonance
        var longEmotion = new string('x', 200);

        // Act
        var result = await _sut.GenerateEmpathyAsync("agent-1", longEmotion, "ctx");

        // Assert
        result.Value.ResonanceStrength.Should().BeGreaterThan(0.7);
        result.Value.SuggestedResponse.Should().Contain("I can feel");
    }

    [Fact]
    public async Task GenerateEmpathyAsync_MediumResonance_GeneratesMediumPhrase()
    {
        // Arrange — medium length emotion for resonance between 0.4 and 0.7
        var mediumEmotion = new string('x', 50);

        // Act
        var result = await _sut.GenerateEmpathyAsync("agent-1", mediumEmotion, "ctx");

        // Assert
        result.Value.ResonanceStrength.Should().BeGreaterThanOrEqualTo(0.4);
        result.Value.ResonanceStrength.Should().BeLessThanOrEqualTo(0.9);
    }

    #endregion

    #region GetActiveEmotions Tests

    [Fact]
    public void GetActiveEmotions_InitiallyEmpty()
    {
        // Act
        var emotions = _sut.GetActiveEmotions();

        // Assert
        emotions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveEmotions_ReturnsDefensiveCopy()
    {
        // Arrange
        await _sut.EvaluateSocialSituationAsync("hurt", "upset");

        // Act
        var emotions1 = _sut.GetActiveEmotions();
        var emotions2 = _sut.GetActiveEmotions();

        // Assert
        emotions1.Should().NotBeSameAs(emotions2);
    }

    #endregion

    #region RecordEmotionOutcome and GetAppropriatenessRate Tests

    [Fact]
    public void RecordEmotionOutcome_NullEmotionId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.RecordEmotionOutcome(null!, true);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetAppropriatenessRate_NoOutcomes_ReturnsOne()
    {
        // Act
        var rate = _sut.GetAppropriatenessRate();

        // Assert
        rate.Should().Be(1.0);
    }

    [Fact]
    public void GetAppropriatenessRate_AllAppropriate_ReturnsOne()
    {
        // Arrange
        _sut.RecordEmotionOutcome("e1", wasAppropriate: true);
        _sut.RecordEmotionOutcome("e2", wasAppropriate: true);
        _sut.RecordEmotionOutcome("e3", wasAppropriate: true);

        // Act
        var rate = _sut.GetAppropriatenessRate();

        // Assert
        rate.Should().Be(1.0);
    }

    [Fact]
    public void GetAppropriatenessRate_MixedOutcomes_ReturnsCorrectFraction()
    {
        // Arrange
        _sut.RecordEmotionOutcome("e1", wasAppropriate: true);
        _sut.RecordEmotionOutcome("e2", wasAppropriate: false);
        _sut.RecordEmotionOutcome("e3", wasAppropriate: true);
        _sut.RecordEmotionOutcome("e4", wasAppropriate: false);

        // Act
        var rate = _sut.GetAppropriatenessRate();

        // Assert
        rate.Should().Be(0.5);
    }

    [Fact]
    public void GetAppropriatenessRate_AllInappropriate_ReturnsZero()
    {
        // Arrange
        _sut.RecordEmotionOutcome("e1", wasAppropriate: false);
        _sut.RecordEmotionOutcome("e2", wasAppropriate: false);

        // Act
        var rate = _sut.GetAppropriatenessRate();

        // Assert
        rate.Should().Be(0.0);
    }

    #endregion

    #region Record Type Tests

    [Fact]
    public void SocialEmotion_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var emotion = new SocialEmotion("id-1", SocialEmotionType.Pride, 0.75, "achievement", "context", DateTime.UtcNow);

        // Assert
        emotion.Id.Should().Be("id-1");
        emotion.Type.Should().Be(SocialEmotionType.Pride);
        emotion.Intensity.Should().Be(0.75);
        emotion.Trigger.Should().Be("achievement");
        emotion.Context.Should().Be("context");
    }

    [Fact]
    public void EmotionRegulationResult_PropertiesSetCorrectly()
    {
        // Act
        var result = new EmotionRegulationResult(
            SocialEmotionType.Guilt, 0.8, 0.3, RegulationStrategy.Reappraisal, true);

        // Assert
        result.OriginalEmotion.Should().Be(SocialEmotionType.Guilt);
        result.OriginalIntensity.Should().Be(0.8);
        result.RegulatedIntensity.Should().Be(0.3);
        result.UsedStrategy.Should().Be(RegulationStrategy.Reappraisal);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void EmpathyResponse_PropertiesSetCorrectly()
    {
        // Act
        var response = new EmpathyResponse("agent-1", "sad", 0.6, "I understand");

        // Assert
        response.TargetAgent.Should().Be("agent-1");
        response.PerceivedEmotion.Should().Be("sad");
        response.ResonanceStrength.Should().Be(0.6);
        response.SuggestedResponse.Should().Be("I understand");
    }

    #endregion

    #region Enum Tests

    [Theory]
    [InlineData(SocialEmotionType.Guilt)]
    [InlineData(SocialEmotionType.Shame)]
    [InlineData(SocialEmotionType.Pride)]
    [InlineData(SocialEmotionType.Gratitude)]
    [InlineData(SocialEmotionType.Empathy)]
    [InlineData(SocialEmotionType.Jealousy)]
    [InlineData(SocialEmotionType.Compassion)]
    public void SocialEmotionType_AllValuesAreDefined(SocialEmotionType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Theory]
    [InlineData(RegulationStrategy.Reappraisal)]
    [InlineData(RegulationStrategy.Suppression)]
    [InlineData(RegulationStrategy.Distraction)]
    [InlineData(RegulationStrategy.SituationModification)]
    [InlineData(RegulationStrategy.AttentionDeployment)]
    public void RegulationStrategy_AllValuesAreDefined(RegulationStrategy strategy)
    {
        Enum.IsDefined(strategy).Should().BeTrue();
    }

    #endregion
}
