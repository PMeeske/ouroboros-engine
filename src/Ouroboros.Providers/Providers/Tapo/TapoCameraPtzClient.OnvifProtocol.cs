// <copyright file="TapoCameraPtzClient.OnvifProtocol.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// ONVIF protocol internals: SOAP builders, WS-Security, and command transport.
/// </summary>
public sealed partial class TapoCameraPtzClient
{
    private async Task<Result<PtzMoveResult>> ContinuousMoveAsync(
        float panSpeed, float tiltSpeed, float zoomSpeed,
        int durationMs, CancellationToken ct)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        // Clamp speeds to valid range
        panSpeed = Math.Clamp(panSpeed, -MaxSpeed, MaxSpeed);
        tiltSpeed = Math.Clamp(tiltSpeed, -MaxSpeed, MaxSpeed);
        zoomSpeed = Math.Clamp(zoomSpeed, -MaxSpeed, MaxSpeed);

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var soapBody = BuildOnvifContinuousMove(
                _profileToken ?? "profile_1", panSpeed, tiltSpeed, zoomSpeed);
            var startResult = await SendOnvifCommandAsync(soapBody, ct);

            if (startResult.IsFailure)
            {
                return Result<PtzMoveResult>.Failure($"Move failed: {startResult.Error}");
            }

            // Wait for the specified duration
            await Task.Delay(durationMs, ct);

            // Stop the movement
            await StopAsync(ct);

            sw.Stop();

            var direction = (panSpeed, tiltSpeed) switch
            {
                ( < 0, 0) => "pan_left",
                ( > 0, 0) => "pan_right",
                (0, > 0) => "tilt_up",
                (0, < 0) => "tilt_down",
                ( < 0, > 0) => "pan_left+tilt_up",
                ( > 0, > 0) => "pan_right+tilt_up",
                ( < 0, < 0) => "pan_left+tilt_down",
                ( > 0, < 0) => "pan_right+tilt_down",
                _ => "stop"
            };

            _logger?.LogDebug("PTZ move {Direction} at speed ({Pan},{Tilt}) for {Duration}ms on {CameraIp}",
                direction, panSpeed, tiltSpeed, durationMs, _cameraIp);

            return Result<PtzMoveResult>.Success(
                new PtzMoveResult(true, direction, sw.Elapsed,
                    $"Moved {direction} for {durationMs}ms"));
        }
        catch (OperationCanceledException)
        {
            await StopAsync(CancellationToken.None);
            return Result<PtzMoveResult>.Failure("Movement cancelled");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "PTZ move failed on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Move failed: {ex.Message}");
        }
    }

    private async Task<Result<string>> SendOnvifCommandAsync(string soapEnvelope, CancellationToken ct)
    {
        try
        {
            // Inject WS-Security header into SOAP envelope
            var authenticatedEnvelope = InjectWsseHeader(soapEnvelope);
            var content = new StringContent(authenticatedEnvelope, Encoding.UTF8, "application/soap+xml");
            content.Headers.Add("SOAPAction", "\"\"");

            var request = new HttpRequestMessage(HttpMethod.Post, _onvifUrl)
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return Result<string>.Success(body);
            }

            // If ONVIF port 2020 fails, try port 80
            if (response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return await TryAlternateOnvifPortAsync(soapEnvelope, ct);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            return Result<string>.Failure(
                $"ONVIF returned {response.StatusCode}: {errorBody}");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            // Port 2020 not available, try port 80
            return await TryAlternateOnvifPortAsync(soapEnvelope, ct);
        }
        catch (TaskCanceledException)
        {
            return Result<string>.Failure("ONVIF request timed out");
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Failure($"ONVIF error: {ex.Message}");
        }
    }

    private async Task<Result<string>> TryAlternateOnvifPortAsync(
        string soapEnvelope, CancellationToken ct)
    {
        try
        {
            // Tapo cameras often expose ONVIF on port 80 or 8080
            var alternateUrls = new[]
            {
                $"http://{_cameraIp}:80/onvif/ptz_service",
                $"http://{_cameraIp}:8080/onvif/ptz_service",
                $"http://{_cameraIp}:80/onvif/device_service",
            };

            foreach (var url in alternateUrls)
            {
                try
                {
                    var content = new StringContent(soapEnvelope, Encoding.UTF8, "application/soap+xml");
                    var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                    var response = await _httpClient.SendAsync(request, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync(ct);
                        _logger?.LogDebug("ONVIF succeeded on alternate URL: {Url}", url);
                        return Result<string>.Success(body);
                    }
                }
                catch
                {
                    // Try next URL
                }
            }

            return Result<string>.Failure("ONVIF not available on any port");
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Failure($"Alternate port probe failed: {ex.Message}");
        }
    }

    private async Task<Result<string>> GetOnvifProfileTokenAsync(CancellationToken ct)
    {
        var getProfilesSoap = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
                <s:Header>{BuildWsseHeader()}</s:Header>
                <s:Body>
                    <trt:GetProfiles/>
                </s:Body>
            </s:Envelope>
            """;

        var mediaUrl = $"http://{_cameraIp}:2020/onvif/media_service";
        try
        {
            var content = new StringContent(getProfilesSoap, Encoding.UTF8, "application/soap+xml");
            var request = new HttpRequestMessage(HttpMethod.Post, mediaUrl) { Content = content };
            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                // Parse profile token from SOAP response
                var tokenStart = body.IndexOf("token=\"", StringComparison.Ordinal);
                if (tokenStart >= 0)
                {
                    tokenStart += 7;
                    var tokenEnd = body.IndexOf('"', tokenStart);
                    if (tokenEnd > tokenStart)
                    {
                        return Result<string>.Success(body[tokenStart..tokenEnd]);
                    }
                }
                return Result<string>.Success("profile_1");
            }

            return Result<string>.Failure($"GetProfiles failed: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Failure($"GetProfiles failed: {ex.Message}");
        }
    }

    private static string BuildOnvifContinuousMove(
        string profileToken, float panSpeed, float tiltSpeed, float zoomSpeed)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl"
                        xmlns:tt="http://www.onvif.org/ver10/schema">
                <s:Header/>
                <s:Body>
                    <tptz:ContinuousMove>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                        <tptz:Velocity>
                            <tt:PanTilt x="{panSpeed:F2}" y="{tiltSpeed:F2}"/>
                            <tt:Zoom x="{zoomSpeed:F2}"/>
                        </tptz:Velocity>
                    </tptz:ContinuousMove>
                </s:Body>
            </s:Envelope>
            """;
    }

    private static string BuildOnvifStop(string profileToken)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl">
                <s:Header/>
                <s:Body>
                    <tptz:Stop>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                        <tptz:PanTilt>true</tptz:PanTilt>
                        <tptz:Zoom>true</tptz:Zoom>
                    </tptz:Stop>
                </s:Body>
            </s:Envelope>
            """;
    }

    private static string BuildOnvifGotoHomePosition(string profileToken)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl">
                <s:Header/>
                <s:Body>
                    <tptz:GotoHomePosition>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                    </tptz:GotoHomePosition>
                </s:Body>
            </s:Envelope>
            """;
    }

    private static string BuildOnvifSetPreset(string profileToken, string presetName)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl">
                <s:Header/>
                <s:Body>
                    <tptz:SetPreset>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                        <tptz:PresetName>{presetName}</tptz:PresetName>
                    </tptz:SetPreset>
                </s:Body>
            </s:Envelope>
            """;
    }

    private static string BuildOnvifGotoPreset(string profileToken, string presetToken)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl"
                        xmlns:tt="http://www.onvif.org/ver10/schema">
                <s:Header/>
                <s:Body>
                    <tptz:GotoPreset>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                        <tptz:PresetToken>{presetToken}</tptz:PresetToken>
                        <tptz:Speed>
                            <tt:PanTilt x="0.5" y="0.5"/>
                            <tt:Zoom x="0.5"/>
                        </tptz:Speed>
                    </tptz:GotoPreset>
                </s:Body>
            </s:Envelope>
            """;
    }

    private string BuildWsseHeader()
    {
        // WS-Security UsernameToken with PasswordDigest per ONVIF spec:
        // PasswordDigest = Base64(SHA1(nonce_raw + created_utf8 + password_utf8))
        var nonceBytes = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(nonceBytes);
        }

        var created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var createdBytes = Encoding.UTF8.GetBytes(created);
        var passwordBytes = Encoding.UTF8.GetBytes(_password);

        // Digest = Base64(SHA1(nonce + created + password))
        var digestInput = new byte[nonceBytes.Length + createdBytes.Length + passwordBytes.Length];
        Buffer.BlockCopy(nonceBytes, 0, digestInput, 0, nonceBytes.Length);
        Buffer.BlockCopy(createdBytes, 0, digestInput, nonceBytes.Length, createdBytes.Length);
        Buffer.BlockCopy(passwordBytes, 0, digestInput, nonceBytes.Length + createdBytes.Length, passwordBytes.Length);

        var digestHash = SHA1.HashData(digestInput);
        var passwordDigest = Convert.ToBase64String(digestHash);
        var nonceBase64 = Convert.ToBase64String(nonceBytes);

        return $"""
            <Security xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"
                      s:mustUnderstand="true">
                <UsernameToken>
                    <Username>{_username}</Username>
                    <Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest">{passwordDigest}</Password>
                    <Nonce EncodingType="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary">{nonceBase64}</Nonce>
                    <Created xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">{created}</Created>
                </UsernameToken>
            </Security>
            """;
    }

    /// <summary>
    /// Replaces the empty <c>&lt;s:Header/&gt;</c> placeholder in a SOAP envelope
    /// with a freshly computed WS-Security UsernameToken header.
    /// </summary>
    private string InjectWsseHeader(string soapEnvelope)
    {
        var wsseHeader = BuildWsseHeader();
        // Replace empty header placeholder with auth header
        return soapEnvelope.Replace("<s:Header/>", $"<s:Header>{wsseHeader}</s:Header>");
    }
}
