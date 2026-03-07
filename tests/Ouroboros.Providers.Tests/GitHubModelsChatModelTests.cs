namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class GitHubModelsChatModelTests
{
    [Fact]
    public void Ctor_NullToken_Throws()
    {
        FluentActions.Invoking(() => new GitHubModelsChatModel(null!, "gpt-4o"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullModel_Throws()
    {
        FluentActions.Invoking(() => new GitHubModelsChatModel("token", null!))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithValidArgs_DoesNotThrow()
    {
        var model = new GitHubModelsChatModel("my-token", "gpt-4o");

        model.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_WithCustomEndpoint_DoesNotThrow()
    {
        var model = new GitHubModelsChatModel("my-token", "gpt-4o", "https://custom.endpoint.com");

        model.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_WithSettings_DoesNotThrow()
    {
        var settings = new ChatRuntimeSettings();
        var model = new GitHubModelsChatModel("my-token", "gpt-4o", settings: settings);

        model.Should().NotBeNull();
    }
}
