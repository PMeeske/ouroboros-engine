// <copyright file="DockerMcpClientTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using FluentAssertions;
using Ouroboros.Providers.Docker;
using Xunit;

namespace Ouroboros.Tests.Tests.Docker;

/// <summary>
/// Tests for DockerMcpClient operations.
/// </summary>
public sealed class DockerMcpClientTests : IDisposable
{
    private readonly DockerMcpClientOptions _options;

    public DockerMcpClientTests()
    {
        _options = new DockerMcpClientOptions
        {
            BaseUrl = "http://localhost:2375",
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    [Fact]
    public void Constructor_ValidOptions_DoesNotThrow()
    {
        var client = new DockerMcpClient(_options, new HttpClient());
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_WithBaseUrl_IsValid()
    {
        _options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Options_Default_IsValidOnWindows()
    {
        var opts = new DockerMcpClientOptions();
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            opts.IsValid().Should().BeTrue();
    }

    [Fact]
    public async Task ListContainersAsync_ParsesContainers()
    {
        var json = """
        [{
            "Id": "abc123def456",
            "Names": ["/my-container"],
            "Image": "nginx:latest",
            "State": "running",
            "Status": "Up 3 hours",
            "Ports": [{"IP":"0.0.0.0","PrivatePort":80,"PublicPort":8080,"Type":"tcp"}],
            "Labels": {"env":"prod"},
            "Created": 1735689600
        }]
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.ListContainersAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Names.Should().Contain("my-container");
        result.Value[0].Image.Should().Be("nginx:latest");
        result.Value[0].State.Should().Be("running");
        result.Value[0].ShortId.Should().Be("abc123def456");
    }

    [Fact]
    public async Task InspectContainerAsync_ParsesDetails()
    {
        var json = """
        {
            "Id": "abc123def456789",
            "Name": "/web-server",
            "Config": { "Image": "nginx:latest" },
            "State": { "Status": "running" },
            "Created": "2026-01-01T00:00:00Z"
        }
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.InspectContainerAsync("abc123");

        result.IsSuccess.Should().BeTrue();
        result.Value.Names.Should().Contain("web-server");
    }

    [Fact]
    public async Task StartContainerAsync_AlreadyRunning_ReturnsSuccess()
    {
        var handler = new MockHttpHandler(HttpStatusCode.NotModified, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.StartContainerAsync("abc123");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("already running");
    }

    [Fact]
    public async Task StopContainerAsync_Success()
    {
        var handler = new MockHttpHandler(HttpStatusCode.NoContent, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.StopContainerAsync("abc123");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("stopped");
    }

    [Fact]
    public async Task RemoveContainerAsync_NotFound_ReturnsError()
    {
        var handler = new MockHttpHandler(HttpStatusCode.NotFound, """{"message":"no such container"}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.RemoveContainerAsync("nonexistent");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ListImagesAsync_ParsesImages()
    {
        var json = """
        [{
            "Id": "sha256:abc123",
            "RepoTags": ["nginx:latest", "nginx:1.25"],
            "Size": 188000000,
            "Created": 1735689600
        }]
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.ListImagesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].RepoTags.Should().Contain("nginx:latest");
    }

    [Fact]
    public async Task ListNetworksAsync_ParsesNetworks()
    {
        var json = """
        [{
            "Id": "net123",
            "Name": "bridge",
            "Driver": "bridge",
            "Scope": "local"
        }]
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.ListNetworksAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("bridge");
    }

    [Fact]
    public async Task ListVolumesAsync_ParsesVolumes()
    {
        var json = """
        {
            "Volumes": [{
                "Name": "data-vol",
                "Driver": "local",
                "Mountpoint": "/var/lib/docker/volumes/data-vol/_data",
                "Labels": {"purpose":"storage"}
            }]
        }
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.ListVolumesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("data-vol");
    }

    [Fact]
    public async Task RunContainerAsync_CreatesAndStarts()
    {
        var createJson = """{"Id":"new123container456"}""";
        var handler = new MockHttpHandler(HttpStatusCode.Created, createJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.RunContainerAsync("nginx:latest", "my-nginx", ["8080:80"]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("new123container456");
    }

    [Fact]
    public async Task GetContainerLogsAsync_ReturnsLogs()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, "log output here");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2375") };
        var client = new DockerMcpClient(_options, httpClient);

        var result = await client.GetContainerLogsAsync("abc123");

        result.IsSuccess.Should().BeTrue();
    }

    public void Dispose() { }

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
