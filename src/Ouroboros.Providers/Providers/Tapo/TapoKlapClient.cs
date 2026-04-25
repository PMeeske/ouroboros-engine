// Copyright (c) Ouroboros. All rights reserved.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Native C# client for current-firmware Tapo devices using the KLAP V2 protocol over HTTPS.
/// Replaces the Python <c>tapo</c> library + <c>tapo_gateway.py</c> sidecar that
/// only spoke legacy plain-HTTP and fails against post-2023 firmware which RSTs port 80.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle: construct → <see cref="LoginAsync"/> → call methods → dispose.
/// The handshake establishes a session cookie and derives AES-128-CBC keys; subsequent calls
/// to <see cref="SendAsync"/> use those keys until the device times them out (~24 min idle).
/// On a 401/403 the caller should construct a new client and re-login.
/// </para>
/// <para>
/// Certificate validation: Tapo devices present self-signed certs. The handler bundled with
/// this client trusts any cert from the configured host. Authentication and confidentiality
/// come from the KLAP layer, not TLS — TLS here is just a transport that the device firmware
/// happens to require.
/// </para>
/// </remarks>
public sealed class TapoKlapClient : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    private readonly Uri _baseUri;
    private readonly byte[] _authHash;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly ILogger _logger;
    private readonly string _terminalUuid;
    private readonly JsonSerializerOptions _jsonOptions;

    private byte[]? _key;
    private byte[]? _baseIv;
    private byte[]? _sigKey;
    private int _seq;
    private string? _sessionCookie;

    /// <summary>The base URI (e.g. <c>https://192.168.1.42</c>) — useful for diagnostics.</summary>
    public Uri BaseUri => _baseUri;

    /// <summary>True once <see cref="LoginAsync"/> has completed successfully.</summary>
    public bool IsAuthenticated => _key is not null && _baseIv is not null && _sigKey is not null;

    /// <summary>
    /// Creates a client targeting a specific Tapo device. Caller may inject an <see cref="HttpClient"/>
    /// (e.g. one with custom DNS, proxy, or shared connection pool); when null, an internal client
    /// is created with cert validation disabled — appropriate for LAN devices with self-signed certs.
    /// </summary>
    /// <param name="host">Device hostname or IPv4 address.</param>
    /// <param name="username">Tapo cloud account username (typically email).</param>
    /// <param name="password">Tapo cloud account password.</param>
    /// <param name="port">HTTPS port (default 443).</param>
    /// <param name="httpClient">Optional HttpClient. When null, the client owns its handler.</param>
    /// <param name="logger">Optional logger.</param>
    public TapoKlapClient(
        string host,
        string username,
        string password,
        int port = 443,
        HttpClient? httpClient = null,
        ILogger<TapoKlapClient>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        _baseUri = new UriBuilder("https", host, port).Uri;
        _authHash = TapoKlapCipher.AuthHash(username, password);
        _logger = logger ?? NullLogger<TapoKlapClient>.Instance;
        _terminalUuid = Guid.NewGuid().ToString();
        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        if (httpClient is null)
        {
            HttpClientHandler? handler = null;
            try
            {
                handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                    UseCookies = false,
                };
                _http = new HttpClient(handler, disposeHandler: true) { Timeout = DefaultTimeout };
                handler = null; // ownership transferred to HttpClient
                _ownsHttp = true;
            }
            finally
            {
                handler?.Dispose();
            }
        }
        else
        {
            _http = httpClient;
            _ownsHttp = false;
        }
    }

    /// <summary>
    /// Performs the two-step KLAP handshake, derives session keys, and stores the session cookie.
    /// Idempotent — calling twice resets the session.
    /// </summary>
    public async Task LoginAsync(CancellationToken ct = default)
    {
        // handshake1: client posts 16-byte local_seed; device returns 48 bytes (16 remote_seed || 32 server_hash) + Set-Cookie
        byte[] localSeed = RandomNumberGenerator.GetBytes(16);

        using var hs1 = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "/app/handshake1"))
        {
            Content = new ByteArrayContent(localSeed),
        };
        hs1.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using HttpResponseMessage resp1 = await _http.SendAsync(hs1, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        if (resp1.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new TapoKlapAuthException("handshake1 returned 403 — credentials rejected by device");
        }
        resp1.EnsureSuccessStatusCode();

        byte[] hs1Body = await resp1.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (hs1Body.Length != 48)
        {
            throw new TapoKlapAuthException($"handshake1 returned {hs1Body.Length} bytes; expected 48 (16 remote_seed || 32 server_hash)");
        }

        // Slice into byte[] copies — these survive across awaits below; ReadOnlySpan would not.
        byte[] remoteSeed = hs1Body.AsSpan(0, 16).ToArray();
        byte[] serverHash = hs1Body.AsSpan(16, 32).ToArray();

        if (!TapoKlapCipher.VerifyHandshake1(localSeed, serverHash, _authHash))
        {
            throw new TapoKlapAuthException("handshake1 server_hash mismatch — username/password incorrect for this device");
        }

        _sessionCookie = ExtractSessionCookie(resp1);

        // handshake2: client posts SHA256(remote_seed || auth_hash); device returns 200 OK
        byte[] hs2Body = TapoKlapCipher.BuildHandshake2(remoteSeed, _authHash);

        using var hs2 = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "/app/handshake2"))
        {
            Content = new ByteArrayContent(hs2Body),
        };
        hs2.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        AddCookie(hs2);

        using HttpResponseMessage resp2 = await _http.SendAsync(hs2, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        resp2.EnsureSuccessStatusCode();

        // Derive the session.
        (byte[] key, byte[] iv, byte[] sig) = TapoKlapCipher.DeriveSession(localSeed, remoteSeed, _authHash);
        _key = key;
        _baseIv = iv;
        _sigKey = sig;
        _seq = 1;

        _logger.LogDebug("[TapoKlap] Authenticated to {Host}", _baseUri.Host);
    }

    /// <summary>Convenience for the common <c>get_device_info</c> call.</summary>
    public async Task<TapoDeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default)
    {
        TapoDeviceInfo? info = await SendAsync<TapoDeviceInfo>("get_device_info", null, ct).ConfigureAwait(false);
        return info ?? throw new InvalidOperationException("Device returned no device_info result");
    }

    /// <summary>Convenience: turn a plug or bulb on or off.</summary>
    public Task SetDeviceOnAsync(bool on, CancellationToken ct = default)
        => SendAsync<JsonElement>("set_device_info", new { device_on = on }, ct);

    /// <summary>
    /// Sends an arbitrary KLAP method call. Encrypts the request, signs it, and decodes the
    /// <c>{"error_code":N,"result":...}</c> envelope. Throws <see cref="TapoKlapException"/> on non-zero error_code.
    /// </summary>
    public async Task<T?> SendAsync<T>(string method, object? @params, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        var envelope = new TapoKlapRequest
        {
            Method = method,
            Params = @params,
            TerminalUuid = _terminalUuid,
        };
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(envelope, _jsonOptions);

        int seq = Interlocked.Increment(ref _seq);
        byte[] body = TapoKlapCipher.EncryptRequest(plaintext, seq, _key!, _baseIv!, _sigKey!);

        var url = new Uri(_baseUri, $"/app/request?seq={seq}");
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new ByteArrayContent(body),
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        AddCookie(req);

        using HttpResponseMessage resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
        {
            // Session expired or device kicked us; caller should re-login.
            _key = _baseIv = _sigKey = null;
            throw new TapoKlapAuthException($"Session rejected ({(int)resp.StatusCode}) — re-login required");
        }
        resp.EnsureSuccessStatusCode();

        byte[] wire = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        byte[] decrypted = TapoKlapCipher.DecryptResponse(wire, seq, _key!, _baseIv!, _sigKey!);

        TapoKlapEnvelope<T>? parsed = JsonSerializer.Deserialize<TapoKlapEnvelope<T>>(decrypted, _jsonOptions);
        if (parsed is null)
        {
            throw new TapoKlapException("KLAP response decoded but JSON envelope was null");
        }
        if (parsed.ErrorCode != 0)
        {
            throw new TapoKlapException($"Device returned error_code={parsed.ErrorCode} for {method} ({parsed.Message ?? "no message"})");
        }
        return parsed.Result;
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("Call LoginAsync before issuing requests");
        }
    }

    private void AddCookie(HttpRequestMessage req)
    {
        if (!string.IsNullOrEmpty(_sessionCookie))
        {
            req.Headers.TryAddWithoutValidation("Cookie", _sessionCookie);
        }
    }

    private static string? ExtractSessionCookie(HttpResponseMessage resp)
    {
        if (!resp.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
        {
            return null;
        }

        foreach (string raw in values)
        {
            // We forward only the name=value pair; attributes (Path, TIMEOUT, etc.) are device-side.
            int semi = raw.IndexOf(';');
            string pair = semi >= 0 ? raw[..semi] : raw;
            if (pair.StartsWith("TP_SESSIONID=", StringComparison.OrdinalIgnoreCase))
            {
                return pair;
            }
        }
        return null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
        if (_key is not null) CryptographicOperations.ZeroMemory(_key);
        if (_baseIv is not null) CryptographicOperations.ZeroMemory(_baseIv);
        if (_sigKey is not null) CryptographicOperations.ZeroMemory(_sigKey);
        CryptographicOperations.ZeroMemory(_authHash);
    }
}

/// <summary>Base exception for KLAP transport errors.</summary>
public class TapoKlapException : Exception
{
    public TapoKlapException(string message) : base(message) { }
    public TapoKlapException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Authentication-specific failure (bad creds, session expired, device rejected).</summary>
public sealed class TapoKlapAuthException : TapoKlapException
{
    public TapoKlapAuthException(string message) : base(message) { }
    public TapoKlapAuthException(string message, Exception inner) : base(message, inner) { }
}
