using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoCredentialsTests
{
    [Fact]
    public void TapoCredentials_Construction_ShouldSetProperties()
    {
        // Arrange & Act
        var credentials = new TapoCredentials
        {
            Email = "user@example.com",
            Password = "secret123"
        };

        // Assert
        credentials.Email.Should().Be("user@example.com");
        credentials.Password.Should().Be("secret123");
    }

    [Fact]
    public void TapoCredentials_Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new TapoCredentials { Email = "user@example.com", Password = "pass" };
        var b = new TapoCredentials { Email = "user@example.com", Password = "pass" };

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void TapoCredentials_Equality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var a = new TapoCredentials { Email = "user1@example.com", Password = "pass" };
        var b = new TapoCredentials { Email = "user2@example.com", Password = "pass" };

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void TapoCredentials_JsonSerialization_ShouldUseJsonPropertyNames()
    {
        // Arrange
        var credentials = new TapoCredentials
        {
            Email = "test@example.com",
            Password = "mypassword"
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(credentials);

        // Assert
        json.Should().Contain("\"email\":");
        json.Should().Contain("\"password\":");
    }

    [Fact]
    public void TapoCredentials_JsonDeserialization_ShouldRoundtrip()
    {
        // Arrange
        var original = new TapoCredentials
        {
            Email = "test@example.com",
            Password = "secure"
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TapoCredentials>(json);

        // Assert
        deserialized.Should().Be(original);
    }
}
