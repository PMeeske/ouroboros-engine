namespace Ouroboros.Network.Tests;

[Trait("Category", "Unit")]
public sealed class EnumsTests
{
    #region WalEntryType

    [Fact]
    public void WalEntryType_AddNode_HasExpectedValue()
    {
        // Assert
        WalEntryType.AddNode.Should().Be(WalEntryType.AddNode);
        ((int)WalEntryType.AddNode).Should().Be(0);
    }

    [Fact]
    public void WalEntryType_AddEdge_HasExpectedValue()
    {
        // Assert
        WalEntryType.AddEdge.Should().Be(WalEntryType.AddEdge);
        ((int)WalEntryType.AddEdge).Should().Be(1);
    }

    [Fact]
    public void WalEntryType_AllValues_AreDefined()
    {
        // Act
        var values = Enum.GetValues(typeof(WalEntryType)).Cast<WalEntryType>().ToList();

        // Assert
        values.Should().Contain(WalEntryType.AddNode);
        values.Should().Contain(WalEntryType.AddEdge);
        values.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("AddNode", WalEntryType.AddNode)]
    [InlineData("AddEdge", WalEntryType.AddEdge)]
    public void WalEntryType_CanParseFromString(string name, WalEntryType expected)
    {
        // Act
        var parsed = Enum.Parse<WalEntryType>(name);

        // Assert
        parsed.Should().Be(expected);
    }

    #endregion
}

[Trait("Category", "Unit")]
public sealed class BranchReifiedEventArgsTests
{
    #region Construction

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var branchName = "TestBranch";
        var nodesCreated = 5;

        // Act
        var args = new BranchReifiedEventArgs(branchName, nodesCreated);

        // Assert
        args.BranchName.Should().Be(branchName);
        args.NodesCreated.Should().Be(nodesCreated);
        args.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithZeroNodes_SetsProperties()
    {
        // Act
        var args = new BranchReifiedEventArgs("Empty", 0);

        // Assert
        args.NodesCreated.Should().Be(0);
        args.BranchName.Should().Be("Empty");
    }

    [Fact]
    public void Constructor_EmptyBranchName_SetsProperties()
    {
        // Act
        var args = new BranchReifiedEventArgs("", 1);

        // Assert
        args.BranchName.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NullBranchName_SetsNull()
    {
        // Act
        var args = new BranchReifiedEventArgs(null!, 1);

        // Assert
        args.BranchName.Should().BeNull();
    }

    [Fact]
    public void Constructor_NegativeNodes_SetsProperty()
    {
        // Act
        var args = new BranchReifiedEventArgs("Test", -1);

        // Assert
        args.NodesCreated.Should().Be(-1);
    }

    #endregion

    #region Inheritance

    [Fact]
    public void InheritsFrom_EventArgs()
    {
        // Arrange
        var args = new BranchReifiedEventArgs("Test", 1);

        // Assert
        args.Should().BeAssignableTo<EventArgs>();
    }

    #endregion
}
