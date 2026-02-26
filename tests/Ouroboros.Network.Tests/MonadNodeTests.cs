using System.Collections.Immutable;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class MonadNodeTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var parentIds = ImmutableArray.Create(Guid.NewGuid());

        var node = new MonadNode(id, "Draft", "{\"text\":\"hello\"}", now, parentIds);

        node.Id.Should().Be(id);
        node.TypeName.Should().Be("Draft");
        node.PayloadJson.Should().Contain("hello");
        node.CreatedAt.Should().Be(now);
        node.ParentIds.Should().HaveCount(1);
        node.Hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Ctor_NullTypeName_Throws()
    {
        FluentActions.Invoking(() => new MonadNode(
                Guid.NewGuid(), null!, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullPayloadJson_Throws()
    {
        FluentActions.Invoking(() => new MonadNode(
                Guid.NewGuid(), "Test", null!, DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void VerifyHash_ReturnsTrueForValidNode()
    {
        var node = new MonadNode(
            Guid.NewGuid(), "Test", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        node.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        var id = Guid.NewGuid();
        var time = DateTimeOffset.UtcNow;

        var a = new MonadNode(id, "T", "{}", time, ImmutableArray<Guid>.Empty);
        var b = new MonadNode(id, "T", "{}", time, ImmutableArray<Guid>.Empty);

        a.Hash.Should().Be(b.Hash);
    }

    [Fact]
    public void FromPayload_CreatesValidNode()
    {
        var node = MonadNode.FromPayload("TestPayload", new { Value = 42 });

        node.TypeName.Should().Be("TestPayload");
        node.PayloadJson.Should().Contain("42");
        node.ParentIds.Should().BeEmpty();
        node.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void FromPayload_WithParentIds_SetsParents()
    {
        var parentId = Guid.NewGuid();
        var node = MonadNode.FromPayload("T", "data", ImmutableArray.Create(parentId));

        node.ParentIds.Should().ContainSingle().Which.Should().Be(parentId);
    }

    [Fact]
    public void DeserializePayload_ValidJson_ReturnsSome()
    {
        var node = MonadNode.FromPayload("Test", new { Name = "test" });
        var result = node.DeserializePayload<Dictionary<string, object>>();

        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void DeserializePayload_InvalidType_ReturnsNone()
    {
        var node = new MonadNode(
            Guid.NewGuid(), "T", "not-valid-json", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        var result = node.DeserializePayload<int>();
        result.HasValue.Should().BeFalse();
    }
}
