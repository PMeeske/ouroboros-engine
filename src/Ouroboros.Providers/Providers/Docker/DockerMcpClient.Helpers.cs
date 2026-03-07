// <copyright file="DockerMcpClient.Helpers.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Ouroboros.Providers.Json;

namespace Ouroboros.Providers.Docker;

public sealed partial class DockerMcpClient
{
    private static HttpClient CreateHttpClient(DockerMcpClientOptions options)
    {
        HttpMessageHandler handler;

        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            handler = new HttpClientHandler();
        }
        else if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, ct) =>
                {
                    var pipeName = options.PipePath.Replace(@"//./pipe/", "");
                    var pipe = new System.IO.Pipes.NamedPipeClientStream(
                        ".", pipeName, System.IO.Pipes.PipeDirection.InOut,
                        System.IO.Pipes.PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(ct);
                    return pipe;
                }
            };
        }
        else
        {
            handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, ct) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(options.SocketPath), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };
        }

        var client = new HttpClient(handler) { Timeout = options.Timeout };

        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            client.BaseAddress = new Uri(options.BaseUrl);
        }
        else
        {
            client.BaseAddress = new Uri("http://localhost");
        }

        return client;
    }

    private static DockerContainerInfo ParseContainerSummary(JsonElement el)
    {
        var names = new List<string>();
        if (el.TryGetProperty("Names", out var ns) && ns.ValueKind == JsonValueKind.Array)
            foreach (var n in ns.EnumerateArray())
                names.Add(n.GetString()!.TrimStart('/'));

        var ports = new List<DockerPortMapping>();
        if (el.TryGetProperty("Ports", out var ps) && ps.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in ps.EnumerateArray())
            {
                ports.Add(new DockerPortMapping
                {
                    HostIp = p.TryGetProperty("IP", out var ip) ? ip.GetString() : null,
                    HostPort = p.TryGetProperty("PublicPort", out var hp) ? hp.GetInt32() : null,
                    ContainerPort = p.TryGetProperty("PrivatePort", out var cp) ? cp.GetInt32() : 0,
                    Protocol = p.TryGetProperty("Type", out var tp) ? tp.GetString()! : "tcp"
                });
            }
        }

        var labels = new Dictionary<string, string>();
        if (el.TryGetProperty("Labels", out var lbl) && lbl.ValueKind == JsonValueKind.Object)
            foreach (var kv in lbl.EnumerateObject())
                labels[kv.Name] = kv.Value.GetString()!;

        return new DockerContainerInfo
        {
            Id = el.GetProperty("Id").GetString()!,
            Names = names,
            Image = el.TryGetProperty("Image", out var img) ? img.GetString()! : "unknown",
            State = el.TryGetProperty("State", out var st) ? st.GetString()! : "unknown",
            Status = el.TryGetProperty("Status", out var sts) ? sts.GetString() : null,
            Ports = ports,
            Labels = labels,
            CreatedAt = el.TryGetProperty("Created", out var cr)
                ? DateTimeOffset.FromUnixTimeSeconds(cr.GetInt64()) : null
        };
    }

    private static DockerContainerInfo ParseContainerInspect(JsonElement el)
    {
        var name = el.TryGetProperty("Name", out var n) ? n.GetString()!.TrimStart('/') : "unknown";
        var state = el.TryGetProperty("State", out var st) && st.TryGetProperty("Status", out var sts)
            ? sts.GetString()! : "unknown";

        return new DockerContainerInfo
        {
            Id = el.GetProperty("Id").GetString()!,
            Names = [name],
            Image = el.TryGetProperty("Config", out var cfg) && cfg.TryGetProperty("Image", out var img)
                ? img.GetString()! : "unknown",
            State = state,
            Status = state,
            CreatedAt = el.TryGetProperty("Created", out var cr)
                ? DateTimeOffset.Parse(cr.GetString()!) : null
        };
    }

    /// <summary>
    /// Docker log streams include 8-byte headers per frame. Strip them for plain text output.
    /// </summary>
    private static string StripDockerStreamHeaders(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < 8)
            return raw;

        var sb = new StringBuilder();
        var bytes = Encoding.UTF8.GetBytes(raw);
        var i = 0;
        while (i + 8 <= bytes.Length)
        {
            var frameSize = (bytes[i + 4] << 24) | (bytes[i + 5] << 16) | (bytes[i + 6] << 8) | bytes[i + 7];
            i += 8;
            if (i + frameSize <= bytes.Length)
            {
                sb.Append(Encoding.UTF8.GetString(bytes, i, frameSize));
                i += frameSize;
            }
            else
            {
                return raw;
            }
        }

        return sb.Length > 0 ? sb.ToString() : raw;
    }
}
