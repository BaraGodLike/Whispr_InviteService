using System.Security.Cryptography;
using Application.Abstractions;
using Application.Options;

namespace Infrastructure.Security;

public sealed class AesGcmSecretProtector(EncryptionOptions options) : ISecretProtector
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key = DecodeKey(options.KeyBase64, 32, nameof(options.KeyBase64));

    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        var protectedSecret = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, protectedSecret, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, protectedSecret, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, protectedSecret, NonceSizeBytes + TagSizeBytes, ciphertext.Length);

        return protectedSecret;
    }

    public byte[] Unprotect(byte[] protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);

        if (protectedSecret.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Protected secret is malformed.");
        }

        var nonce = protectedSecret[..NonceSizeBytes];
        var tag = protectedSecret[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        var ciphertext = protectedSecret[(NonceSizeBytes + TagSizeBytes)..];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private static byte[] DecodeKey(string keyBase64, int expectedLength, string paramName)
    {
        try
        {
            var key = Convert.FromBase64String(keyBase64);
            if (key.Length != expectedLength)
            {
                throw new ArgumentException(
                    $"Expected a {expectedLength}-byte key encoded as Base64.",
                    paramName);
            }

            return key;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Expected a valid Base64 value.", paramName, ex);
        }
    }
}
