namespace Ouroboros.Tests.Pipeline.Verification;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class NetworkActionTests
{
    [Fact]
    public void Constructor_SetsOperationAndEndpoint()
    {
        var action = new NetworkAction("get", "https://api.example.com");

        action.Operation.Should().Be("get");
        action.Endpoint.Should().Be("https://api.example.com");
    }

    [Fact]
    public void Endpoint_DefaultsToNull()
    {
        var action = new NetworkAction("post");
        action.Endpoint.Should().BeNull();
    }

    [Fact]
    public void ToMeTTaAtom_ContainsOperation()
    {
        var action = new NetworkAction("connect");
        action.ToMeTTaAtom().Should().Be("(NetworkAction \"connect\")");
    }
}
