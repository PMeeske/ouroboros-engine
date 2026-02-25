// <copyright file="KubernetesMcpClientTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.Kubernetes;

namespace Ouroboros.Tests.Tests.Kubernetes;

/// <summary>
/// Tests for KubernetesMcpClient operations.
/// </summary>
public sealed class KubernetesMcpClientTests : IDisposable
{
    private readonly KubernetesMcpClientOptions _options;

    public KubernetesMcpClientTests()
    {
        _options = new KubernetesMcpClientOptions
        {
            BaseUrl = "https://localhost:6443",
            Token = "test-token",
            SkipTlsVerify = true,
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    [Fact]
    public void Constructor_ValidOptions_DoesNotThrow()
    {
        var client = new KubernetesMcpClient(_options, new HttpClient());
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_InvalidOptions_ThrowsArgumentException()
    {
        var invalid = new KubernetesMcpClientOptions
        {
            BaseUrl = null,
            Token = null,
            KubeConfigPath = null
        };

        // Only invalid if no in-cluster, no kubeconfig, no explicit config
        // On CI this might pass if ~/.kube/config exists, so we just test non-null
        var act = () => new KubernetesMcpClient(invalid, new HttpClient());

        // This may or may not throw depending on whether default kubeconfig exists
        // So we test the options validation directly
        if (!invalid.IsValid())
        {
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void Options_WithBaseUrlAndToken_IsValid()
    {
        _options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Options_Empty_ValidatesBasedOnEnvironment()
    {
        var opts = new KubernetesMcpClientOptions();
        // Validity depends on whether kubeconfig or in-cluster env exists
        opts.IsValid().Should().Be(
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"))
            || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config"))
        );
    }

    [Fact]
    public async Task ListPodsAsync_WithMockResponse_ParsesPods()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, CreatePodListJson());
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:6443") };
        var client = new KubernetesMcpClient(_options, httpClient);

        var result = await client.ListPodsAsync("default");

        result.IsSuccess.Should().BeTrue();
        var pods = result.Value;
        pods.Should().HaveCount(1);
        pods[0].Name.Should().Be("test-pod");
        pods[0].Namespace.Should().Be("default");
        pods[0].Phase.Should().Be("Running");
        pods[0].Containers.Should().Contain("main");
    }

    [Fact]
    public async Task GetPodAsync_NotFound_ReturnsError()
    {
        var handler = new MockHttpHandler(HttpStatusCode.NotFound, "{}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:6443") };
        var client = new KubernetesMcpClient(_options, httpClient);

        var result = await client.GetPodAsync("nonexistent");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetPodLogsAsync_ReturnsLogText()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, "line1\nline2\nline3");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:6443") };
        var client = new KubernetesMcpClient(_options, httpClient);

        var result = await client.GetPodLogsAsync("test-pod");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("line1");
    }

    [Fact]
    public async Task ListDeploymentsAsync_ParsesDeployments()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, CreateDeploymentListJson());
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:6443") };
        var client = new KubernetesMcpClient(_options, httpClient);

        var result = await client.ListDeploymentsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("test-deploy");
        result.Value[0].Replicas.Should().Be(3);
    }

    [Fact]
    public async Task ListServicesAsync_ParsesServices()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, CreateServiceListJson());
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:6443") };
        var client = new KubernetesMcpClient(_options, httpClient);

        var result = await client.ListServicesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("test-svc");
        result.Value[0].Type.Should().Be("ClusterIP");
    }

    [Fact]
    public async Task ListNamespacesAsync_ReturnsNames()
    {
        var json = """{"items":[{"metadata":{"name":"default"}},{"metadata":{"name":"kube-system"}}]}""";
        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:6443") };
        var client = new KubernetesMcpClient(_options, httpClient);

        var result = await client.ListNamespacesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("default");
        result.Value.Should().Contain("kube-system");
    }

    [Fact]
    public async Task ScaleDeploymentAsync_ServerError_ReturnsError()
    {
        var handler = new MockHttpHandler(HttpStatusCode.InternalServerError, "{}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:6443") };
        var client = new KubernetesMcpClient(_options, httpClient);

        var result = await client.ScaleDeploymentAsync("test-deploy", 5);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteResourceAsync_Success()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, """{"status":"Success"}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:6443") };
        var client = new KubernetesMcpClient(_options, httpClient);

        var result = await client.DeleteResourceAsync("pod", "old-pod");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("deleted");
    }

    public void Dispose() { }

    // ── JSON Builders ───────────────────────────────────────────────────

    private static string CreatePodListJson() => """
    {
        "items": [{
            "metadata": { "name": "test-pod", "namespace": "default", "labels": {"app":"web"}, "creationTimestamp": "2026-01-01T00:00:00Z" },
            "spec": { "containers": [{"name":"main"}], "nodeName": "node-1" },
            "status": { "phase": "Running", "podIP": "10.0.0.1", "containerStatuses": [{"restartCount": 2}] }
        }]
    }
    """;

    private static string CreateDeploymentListJson() => """
    {
        "items": [{
            "metadata": { "name": "test-deploy", "namespace": "default", "labels": {"app":"web"}, "creationTimestamp": "2026-01-01T00:00:00Z" },
            "spec": { "replicas": 3 },
            "status": { "readyReplicas": 3, "availableReplicas": 3 }
        }]
    }
    """;

    private static string CreateServiceListJson() => """
    {
        "items": [{
            "metadata": { "name": "test-svc", "namespace": "default" },
            "spec": { "type": "ClusterIP", "clusterIP": "10.96.0.1", "ports": [{"name":"http","port":80,"targetPort":8080,"protocol":"TCP"}], "selector": {"app":"web"} }
        }]
    }
    """;

    /// <summary>Simple mock HTTP handler for testing.</summary>
    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public MockHttpHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
