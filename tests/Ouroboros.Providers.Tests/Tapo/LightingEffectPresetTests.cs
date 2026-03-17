using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class LightingEffectPresetTests
{
    [Fact]
    public void LightingEffectPreset_ShouldHave_SixteenValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<LightingEffectPreset>();

        // Assert
        values.Should().HaveCount(16);
    }

    [Theory]
    [InlineData(LightingEffectPreset.Aurora, 0)]
    [InlineData(LightingEffectPreset.BubblingCauldron, 1)]
    [InlineData(LightingEffectPreset.CandyCane, 2)]
    [InlineData(LightingEffectPreset.Christmas, 3)]
    [InlineData(LightingEffectPreset.Flicker, 4)]
    [InlineData(LightingEffectPreset.GrandmasChristmasLights, 5)]
    [InlineData(LightingEffectPreset.Hanukkah, 6)]
    [InlineData(LightingEffectPreset.HauntedMansion, 7)]
    [InlineData(LightingEffectPreset.Icicle, 8)]
    [InlineData(LightingEffectPreset.Lightning, 9)]
    [InlineData(LightingEffectPreset.Ocean, 10)]
    [InlineData(LightingEffectPreset.Rainbow, 11)]
    [InlineData(LightingEffectPreset.Raindrop, 12)]
    [InlineData(LightingEffectPreset.Spring, 13)]
    [InlineData(LightingEffectPreset.Sunrise, 14)]
    [InlineData(LightingEffectPreset.Sunset, 15)]
    public void LightingEffectPreset_Value_ShouldHaveExpectedIntValue(LightingEffectPreset preset, int expected)
    {
        // Act & Assert
        ((int)preset).Should().Be(expected);
    }

    [Fact]
    public void LightingEffectPreset_AllValues_ShouldBeDefined()
    {
        // Act & Assert
        foreach (var value in Enum.GetValues<LightingEffectPreset>())
        {
            Enum.IsDefined(value).Should().BeTrue();
        }
    }

    [Fact]
    public void LightingEffectPreset_ShouldSerializeAsString()
    {
        // Arrange
        var preset = LightingEffectPreset.Rainbow;

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(preset);

        // Assert
        json.Should().Be("\"Rainbow\"");
    }

    [Fact]
    public void LightingEffectPreset_ShouldDeserializeFromString()
    {
        // Arrange
        var json = "\"Aurora\"";

        // Act
        var result = System.Text.Json.JsonSerializer.Deserialize<LightingEffectPreset>(json);

        // Assert
        result.Should().Be(LightingEffectPreset.Aurora);
    }

    [Fact]
    public void LightingEffectPreset_Valentines_ShouldBeLastValue()
    {
        // Act
        var maxValue = Enum.GetValues<LightingEffectPreset>().Max();

        // Assert
        maxValue.Should().Be(LightingEffectPreset.Valentines);
    }
}
