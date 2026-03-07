namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class LightingEffectPresetTests
{
    [Fact]
    public void Enum_HasSeventeenMembers()
    {
        Enum.GetValues<LightingEffectPreset>().Should().HaveCount(17);
    }

    [Fact]
    public void SerializesToString()
    {
        var json = JsonSerializer.Serialize(LightingEffectPreset.Aurora);
        json.Should().Contain("Aurora");
    }

    [Fact]
    public void DeserializesFromString()
    {
        var result = JsonSerializer.Deserialize<LightingEffectPreset>("\"Rainbow\"");
        result.Should().Be(LightingEffectPreset.Rainbow);
    }
}
