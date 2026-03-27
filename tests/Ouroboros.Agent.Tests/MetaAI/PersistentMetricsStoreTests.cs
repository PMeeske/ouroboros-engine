using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class PersistentMetricsStoreTests : IDisposable
{
    private readonly string _testDir;

    public PersistentMetricsStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ouroboros_metrics_test_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private PersistentMetricsStore CreateSut(bool autoSave = false)
    {
        return new PersistentMetricsStore(new PersistentMetricsConfig(
            StoragePath: _testDir,
            AutoSave: autoSave,
            AutoSaveInterval: TimeSpan.FromHours(1), // Prevent timer firing
            MaxMetricsAge: 0));
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_CreatesDirectory()
    {
        using var sut = CreateSut();
        Directory.Exists(_testDir).Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullConfig_UsesDefaults()
    {
        // This will create a "metrics" directory in the current working directory.
        // Ensure we clean it up before and after the test to avoid polluting the workspace.
        var metricsDir = Path.Combine(Directory.GetCurrentDirectory(), "metrics");

        if (Directory.Exists(metricsDir))
        {
            Directory.Delete(metricsDir, recursive: true);
        }

        try
        {
            using var store = new PersistentMetricsStore(null);
            // If the constructor throws, the test will fail naturally.
        }
        finally
        {
            if (Directory.Exists(metricsDir))
            {
                Directory.Delete(metricsDir, recursive: true);
            }
        }
    }

    // === StoreMetricsAsync Tests ===

    [Fact]
    public async Task StoreMetricsAsync_NullMetrics_ThrowsArgumentNullException()
    {
        using var sut = CreateSut();
        var act = () => sut.StoreMetricsAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StoreMetricsAsync_ValidMetrics_StoresSuccessfully()
    {
        using var sut = CreateSut();
        var metrics = CreateMetrics("test-resource");

        await sut.StoreMetricsAsync(metrics);

        var retrieved = await sut.GetMetricsAsync("test-resource");
        retrieved.Should().NotBeNull();
        retrieved!.ResourceName.Should().Be("test-resource");
    }

    [Fact]
    public async Task StoreMetricsAsync_NoAutoSave_SavesImmediately()
    {
        using var sut = CreateSut(autoSave: false);
        var metrics = CreateMetrics("test-resource");

        await sut.StoreMetricsAsync(metrics);

        // File should exist because autoSave=false means immediate save
        var files = Directory.GetFiles(_testDir, "*.json");
        files.Should().NotBeEmpty();
    }

    // === GetMetricsAsync Tests ===

    [Fact]
    public async Task GetMetricsAsync_NonExistentResource_ReturnsNull()
    {
        using var sut = CreateSut();

        var result = await sut.GetMetricsAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMetricsAsync_ExistingResource_ReturnsMetrics()
    {
        using var sut = CreateSut();
        await sut.StoreMetricsAsync(CreateMetrics("existing"));

        var result = await sut.GetMetricsAsync("existing");

        result.Should().NotBeNull();
        result!.ResourceName.Should().Be("existing");
    }

    // === GetAllMetricsAsync Tests ===

    [Fact]
    public async Task GetAllMetricsAsync_Empty_ReturnsEmptyDictionary()
    {
        using var sut = CreateSut();

        var result = await sut.GetAllMetricsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllMetricsAsync_WithMetrics_ReturnsAll()
    {
        using var sut = CreateSut();
        await sut.StoreMetricsAsync(CreateMetrics("resource-1"));
        await sut.StoreMetricsAsync(CreateMetrics("resource-2"));

        var result = await sut.GetAllMetricsAsync();

        result.Should().HaveCount(2);
        result.Should().ContainKey("resource-1");
        result.Should().ContainKey("resource-2");
    }

    // === RemoveMetricsAsync Tests ===

    [Fact]
    public async Task RemoveMetricsAsync_ExistingResource_ReturnsTrue()
    {
        using var sut = CreateSut();
        await sut.StoreMetricsAsync(CreateMetrics("to-remove"));

        var result = await sut.RemoveMetricsAsync("to-remove");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveMetricsAsync_NonExistentResource_ReturnsFalse()
    {
        using var sut = CreateSut();

        var result = await sut.RemoveMetricsAsync("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMetricsAsync_RemovedResource_CannotBeRetrieved()
    {
        using var sut = CreateSut();
        await sut.StoreMetricsAsync(CreateMetrics("to-remove"));
        await sut.RemoveMetricsAsync("to-remove");

        var result = await sut.GetMetricsAsync("to-remove");

        result.Should().BeNull();
    }

    // === ClearAsync Tests ===

    [Fact]
    public async Task ClearAsync_WithMetrics_RemovesAll()
    {
        using var sut = CreateSut();
        await sut.StoreMetricsAsync(CreateMetrics("r1"));
        await sut.StoreMetricsAsync(CreateMetrics("r2"));

        await sut.ClearAsync();

        var all = await sut.GetAllMetricsAsync();
        all.Should().BeEmpty();
    }

    // === GetStatisticsAsync Tests ===

    [Fact]
    public async Task GetStatisticsAsync_Empty_ReturnsZeros()
    {
        using var sut = CreateSut();

        var stats = await sut.GetStatisticsAsync();

        stats.TotalResources.Should().Be(0);
        stats.TotalExecutions.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithMetrics_ReturnsAccurateStats()
    {
        using var sut = CreateSut();
        await sut.StoreMetricsAsync(new PerformanceMetrics("r1", 10, 100, 0.9, DateTime.UtcNow, new Dictionary<string, double>()));
        await sut.StoreMetricsAsync(new PerformanceMetrics("r2", 5, 200, 0.8, DateTime.UtcNow, new Dictionary<string, double>()));

        var stats = await sut.GetStatisticsAsync();

        stats.TotalResources.Should().Be(2);
        stats.TotalExecutions.Should().Be(15);
        stats.OverallSuccessRate.Should().BeApproximately(0.85, 0.01);
    }

    // === SaveMetricsAsync / LoadMetricsAsync Tests ===

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        var metrics = new PerformanceMetrics("rt-test", 5, 150.0, 0.85, DateTime.UtcNow, new Dictionary<string, double>());

        using (var sut = CreateSut())
        {
            await sut.StoreMetricsAsync(metrics);
            await sut.SaveMetricsAsync();
        }

        // Create new store reading from same directory
        using var sut2 = CreateSut();
        var loaded = await sut2.GetMetricsAsync("rt-test");

        loaded.Should().NotBeNull();
        loaded!.ResourceName.Should().Be("rt-test");
        loaded.ExecutionCount.Should().Be(5);
    }

    // === Dispose Tests ===

    [Fact]
    public async Task Dispose_WithDirtyData_SavesOnDispose()
    {
        var sut = CreateSut(autoSave: true);
        await sut.StoreMetricsAsync(CreateMetrics("dirty-data"));

        sut.Dispose();

        // Verify file was written
        var files = Directory.GetFiles(_testDir, "*.json");
        files.Should().NotBeEmpty();
    }

    [Fact]
    public void Dispose_MultipleDispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Disposed_StoreMetrics_ThrowsObjectDisposedException()
    {
        var sut = CreateSut();
        sut.Dispose();

        var act = () => sut.StoreMetricsAsync(CreateMetrics("test"));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // === Helper Methods ===

    private static PerformanceMetrics CreateMetrics(string name)
    {
        return new PerformanceMetrics(name, 1, 100.0, 1.0, DateTime.UtcNow, new Dictionary<string, double>());
    }
}
