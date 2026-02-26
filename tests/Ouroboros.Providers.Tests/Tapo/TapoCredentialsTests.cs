namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoCredentialsTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var creds = new TapoCredentials { Email = "test@example.com", Password = "secret" };

        creds.Email.Should().Be("test@example.com");
        creds.Password.Should().Be("secret");
    }

    [Fact]
    public void RoundTrips_ThroughJson()
    {
        var original = new TapoCredentials { Email = "a@b.com", Password = "p" };
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TapoCredentials>(json);

        deserialized.Should().Be(original);
    }
}
