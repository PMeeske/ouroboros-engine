// Copyright (c) Ouroboros. All rights reserved.

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// KLAP V2 protocol primitives — credential hashing, key derivation, and authenticated
/// AES-128-CBC encryption used by current-firmware Tapo devices on TCP 443.
/// </summary>
/// <remarks>
/// <para>
/// Protocol summary (compatible with the python-kasa <c>KlapTransportV2</c> implementation):
/// </para>
/// <list type="number">
///   <item><description>auth_hash = SHA256(SHA1(username) || SHA1(password))</description></item>
///   <item><description>handshake1: client → 16 random bytes (local_seed); device → 16 bytes remote_seed || 32 bytes SHA256(local_seed || auth_hash)</description></item>
///   <item><description>handshake2: client → SHA256(remote_seed || auth_hash); device → 200 OK</description></item>
///   <item><description>session keys: AES key/IV/sig derived from SHA256(salt || local_seed || remote_seed || auth_hash) with salts "lsk", "iv", "ldk"</description></item>
///   <item><description>each request: IV = base_iv (12) || seq (4 BE); body = SHA256(sig || seq_be || ciphertext)[..32] || ciphertext; URL = /app/request?seq={n}</description></item>
/// </list>
/// </remarks>
internal static class TapoKlapCipher
{
    private static readonly byte[] LocalKeySalt = Encoding.ASCII.GetBytes("lsk");
    private static readonly byte[] LocalIvSalt = Encoding.ASCII.GetBytes("iv");
    private static readonly byte[] LocalSigSalt = Encoding.ASCII.GetBytes("ldk");

    /// <summary>
    /// Computes the V2 auth_hash: SHA256(SHA1(username) || SHA1(password)).
    /// </summary>
    public static byte[] AuthHash(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        Span<byte> userSha = stackalloc byte[20];
        Span<byte> passSha = stackalloc byte[20];
        SHA1.HashData(Encoding.UTF8.GetBytes(username), userSha);
        SHA1.HashData(Encoding.UTF8.GetBytes(password), passSha);

        Span<byte> combined = stackalloc byte[40];
        userSha.CopyTo(combined);
        passSha.CopyTo(combined[20..]);
        return SHA256.HashData(combined);
    }

    /// <summary>
    /// Verifies the device's handshake1 response by recomputing the expected hash.
    /// </summary>
    public static bool VerifyHandshake1(
        ReadOnlySpan<byte> localSeed,
        ReadOnlySpan<byte> serverHash,
        ReadOnlySpan<byte> authHash)
    {
        Span<byte> material = stackalloc byte[localSeed.Length + authHash.Length];
        localSeed.CopyTo(material);
        authHash.CopyTo(material[localSeed.Length..]);
        Span<byte> expected = stackalloc byte[32];
        SHA256.HashData(material, expected);
        return CryptographicOperations.FixedTimeEquals(serverHash, expected);
    }

    /// <summary>
    /// Builds the handshake2 client hash = SHA256(remote_seed || auth_hash).
    /// </summary>
    public static byte[] BuildHandshake2(ReadOnlySpan<byte> remoteSeed, ReadOnlySpan<byte> authHash)
    {
        Span<byte> material = stackalloc byte[remoteSeed.Length + authHash.Length];
        remoteSeed.CopyTo(material);
        authHash.CopyTo(material[remoteSeed.Length..]);
        return SHA256.HashData(material.ToArray());
    }

    /// <summary>
    /// Derives the symmetric session key, the base IV (12 bytes — last 4 are filled per-request),
    /// and the HMAC signing key from the negotiated seeds and the auth hash.
    /// </summary>
    public static (byte[] Key, byte[] BaseIv, byte[] Sig) DeriveSession(
        ReadOnlySpan<byte> localSeed,
        ReadOnlySpan<byte> remoteSeed,
        ReadOnlySpan<byte> authHash)
    {
        return (
            Derive(LocalKeySalt, localSeed, remoteSeed, authHash, 16),
            Derive(LocalIvSalt, localSeed, remoteSeed, authHash, 12),
            Derive(LocalSigSalt, localSeed, remoteSeed, authHash, 28));
    }

    private static byte[] Derive(
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> localSeed,
        ReadOnlySpan<byte> remoteSeed,
        ReadOnlySpan<byte> authHash,
        int truncateTo)
    {
        int total = salt.Length + localSeed.Length + remoteSeed.Length + authHash.Length;
        byte[] buf = new byte[total];
        int offset = 0;
        salt.CopyTo(buf.AsSpan(offset));
        offset += salt.Length;
        localSeed.CopyTo(buf.AsSpan(offset));
        offset += localSeed.Length;
        remoteSeed.CopyTo(buf.AsSpan(offset));
        offset += remoteSeed.Length;
        authHash.CopyTo(buf.AsSpan(offset));

        byte[] hash = SHA256.HashData(buf);
        if (hash.Length == truncateTo)
        {
            return hash;
        }

        byte[] result = new byte[truncateTo];
        Buffer.BlockCopy(hash, 0, result, 0, truncateTo);
        return result;
    }

    /// <summary>
    /// Encrypts a UTF-8 JSON payload and produces the wire body (signature || ciphertext).
    /// Caller supplies the per-request sequence number.
    /// </summary>
    public static byte[] EncryptRequest(
        ReadOnlySpan<byte> plaintext,
        int seq,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> baseIv,
        ReadOnlySpan<byte> sigKey)
    {
        byte[] iv = BuildIv(baseIv, seq);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.KeySize = 128;
        aes.Key = key.ToArray();
        aes.IV = iv;

        byte[] ciphertext = aes.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);

        Span<byte> seqBe = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(seqBe, seq);

        // signature = SHA256(sig_key || seq_be || ciphertext) — full 32 bytes, prepended.
        byte[] sigInput = new byte[sigKey.Length + 4 + ciphertext.Length];
        sigKey.CopyTo(sigInput);
        seqBe.CopyTo(sigInput.AsSpan(sigKey.Length));
        ciphertext.CopyTo(sigInput.AsSpan(sigKey.Length + 4));
        byte[] signature = SHA256.HashData(sigInput);

        byte[] body = new byte[signature.Length + ciphertext.Length];
        Buffer.BlockCopy(signature, 0, body, 0, signature.Length);
        Buffer.BlockCopy(ciphertext, 0, body, signature.Length, ciphertext.Length);
        return body;
    }

    /// <summary>
    /// Decrypts a wire response (signature || ciphertext). Verifies the signature before decrypting.
    /// </summary>
    public static byte[] DecryptResponse(
        ReadOnlySpan<byte> wire,
        int seq,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> baseIv,
        ReadOnlySpan<byte> sigKey)
    {
        if (wire.Length < 32 + 16)
        {
            throw new InvalidOperationException("KLAP response too short to contain signature + ciphertext");
        }

        ReadOnlySpan<byte> signature = wire[..32];
        ReadOnlySpan<byte> ciphertext = wire[32..];

        Span<byte> seqBe = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(seqBe, seq);

        byte[] sigInput = new byte[sigKey.Length + 4 + ciphertext.Length];
        sigKey.CopyTo(sigInput);
        seqBe.CopyTo(sigInput.AsSpan(sigKey.Length));
        ciphertext.CopyTo(sigInput.AsSpan(sigKey.Length + 4));
        Span<byte> expectedSig = stackalloc byte[32];
        SHA256.HashData(sigInput, expectedSig);

        if (!CryptographicOperations.FixedTimeEquals(signature, expectedSig))
        {
            throw new InvalidOperationException("KLAP response signature mismatch — possible tamper or session drift");
        }

        byte[] iv = BuildIv(baseIv, seq);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.KeySize = 128;
        aes.Key = key.ToArray();
        return aes.DecryptCbc(ciphertext, iv, PaddingMode.PKCS7);
    }

    private static byte[] BuildIv(ReadOnlySpan<byte> baseIv, int seq)
    {
        if (baseIv.Length != 12)
        {
            throw new ArgumentException("KLAP base IV must be 12 bytes", nameof(baseIv));
        }

        byte[] iv = new byte[16];
        baseIv.CopyTo(iv);
        BinaryPrimitives.WriteInt32BigEndian(iv.AsSpan(12), seq);
        return iv;
    }
}
