using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;
using Application.Options;

namespace Infrastructure.Security;

public sealed class HmacInviteHasher(HashingOptions options) : IInviteHasher
{
    private readonly byte[] _key = DecodeKey(options.KeyBase64, nameof(options.KeyBase64));

    public byte[] HashPin(string normalizedPin)
    {
        var payload = Encoding.UTF8.GetBytes(normalizedPin ?? string.Empty);
        return ComputeHash(payload);
    }

    public byte[] HashToken(byte[] token) => ComputeHash(token);

    public bool AreEqual(byte[] left, byte[] right) =>
        CryptographicOperations.FixedTimeEquals(left, right);

    private byte[] ComputeHash(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);

        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(value);
    }

    private static byte[] DecodeKey(string keyBase64, string paramName)
    {
        try
        {
            var key = Convert.FromBase64String(keyBase64);
            if (key.Length == 0)
            {
                throw new ArgumentException("Expected a non-empty Base64 key.", paramName);
            }

            return key;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Expected a valid Base64 value.", paramName, ex);
        }
    }
}
