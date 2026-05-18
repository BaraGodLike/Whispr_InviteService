using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;
using Application.Exceptions;
using Application.Models;
using Application.Options;

namespace Infrastructure.Security;

public sealed class HmacPowService : IPowService
{
    private readonly PowOptions _options;
    private readonly byte[] _signingKey;

    public HmacPowService(PowOptions options)
    {
        _options = options;
        _signingKey = DecodeKey(options.SigningKeyBase64);
    }

    public PowChallenge CreateChallenge(Guid inviteId, int attempts, DateTime nowUtc)
    {
        EnsureUtc(nowUtc);

        var bucket = GetBucket(nowUtc);
        var difficulty = GetDifficulty(attempts);
        var nonce = Convert.ToBase64String(CreateNonce(inviteId, attempts, bucket));
        var expiresAtUtc = GetBucketStart(bucket).Add(_options.ChallengeTtl);

        return new PowChallenge(nonce, difficulty, expiresAtUtc);
    }

    public void ValidateChallenge(
        Guid inviteId,
        int attempts,
        string normalizedPin,
        string nonce,
        ulong solution,
        DateTime nowUtc)
    {
        EnsureUtc(nowUtc);

        if (!TryDecodeNonce(nonce, out var nonceBytes))
        {
            throw new InvalidPowChallengeException("The supplied challenge nonce is malformed.");
        }

        if (!MatchesKnownNonce(inviteId, attempts, nonceBytes, nowUtc))
        {
            throw new InvalidPowChallengeException("The supplied challenge is no longer valid.");
        }

        var difficulty = GetDifficulty(attempts);
        if (!HasLeadingZeroBits(inviteId, nonceBytes, normalizedPin, solution, difficulty))
        {
            throw new InsufficientProofOfWorkException("The supplied proof of work is insufficient.");
        }
    }

    private int GetDifficulty(int attempts) =>
        Math.Min(_options.MaxDifficulty, _options.BaseDifficulty + attempts);

    private bool MatchesKnownNonce(Guid inviteId, int attempts, byte[] nonceBytes, DateTime nowUtc)
    {
        var currentBucket = GetBucket(nowUtc);

        for (var bucket = currentBucket; ; bucket--)
        {
            var bucketStart = GetBucketStart(bucket);
            var expiresAtUtc = bucketStart.Add(_options.ChallengeTtl);
            if (expiresAtUtc < nowUtc)
            {
                break;
            }

            var expectedNonce = CreateNonce(inviteId, attempts, bucket);
            if (CryptographicOperations.FixedTimeEquals(expectedNonce, nonceBytes))
            {
                return true;
            }
        }

        return false;
    }

    private byte[] CreateNonce(Guid inviteId, int attempts, long bucket)
    {
        Span<byte> payload = stackalloc byte[16 + 4 + 8];
        inviteId.TryWriteBytes(payload[..16]);
        BinaryPrimitives.WriteInt32BigEndian(payload[16..20], attempts);
        BinaryPrimitives.WriteInt64BigEndian(payload[20..], bucket);

        using var hmac = new HMACSHA256(_signingKey);
        var fullHash = hmac.ComputeHash(payload.ToArray());
        return fullHash[..16];
    }

    private bool HasLeadingZeroBits(
        Guid inviteId,
        byte[] nonce,
        string normalizedPin,
        ulong solution,
        int difficulty)
    {
        var inviteBytes = inviteId.ToByteArray();
        var pinBytes = Encoding.UTF8.GetBytes(normalizedPin ?? string.Empty);
        Span<byte> solutionBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(solutionBytes, solution);

        var buffer = new byte[inviteBytes.Length + nonce.Length + pinBytes.Length + solutionBytes.Length];
        var offset = 0;

        Buffer.BlockCopy(inviteBytes, 0, buffer, offset, inviteBytes.Length);
        offset += inviteBytes.Length;
        Buffer.BlockCopy(nonce, 0, buffer, offset, nonce.Length);
        offset += nonce.Length;
        Buffer.BlockCopy(pinBytes, 0, buffer, offset, pinBytes.Length);
        offset += pinBytes.Length;
        solutionBytes.CopyTo(buffer.AsSpan(offset, solutionBytes.Length));

        var hash = SHA256.HashData(buffer);
        return CountLeadingZeroBits(hash) >= difficulty;
    }

    private static int CountLeadingZeroBits(byte[] hash)
    {
        var count = 0;

        foreach (var value in hash)
        {
            if (value == 0)
            {
                count += 8;
                continue;
            }

            for (var bit = 7; bit >= 0; bit--)
            {
                if ((value & (1 << bit)) == 0)
                {
                    count++;
                    continue;
                }

                return count;
            }
        }

        return count;
    }

    private long GetBucket(DateTime nowUtc) =>
        nowUtc.Ticks / _options.BucketDuration.Ticks;

    private DateTime GetBucketStart(long bucket) =>
        new(bucket * _options.BucketDuration.Ticks, DateTimeKind.Utc);

    private static bool TryDecodeNonce(string nonce, out byte[] nonceBytes)
    {
        try
        {
            nonceBytes = Convert.FromBase64String(nonce);
            return nonceBytes.Length == 16;
        }
        catch (FormatException)
        {
            nonceBytes = [];
            return false;
        }
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("UTC timestamps are required.", nameof(value));
        }
    }

    private static byte[] DecodeKey(string keyBase64)
    {
        try
        {
            var key = Convert.FromBase64String(keyBase64);
            if (key.Length == 0)
            {
                throw new ArgumentException("Expected a non-empty Base64 key.", nameof(keyBase64));
            }

            return key;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Expected a valid Base64 value.", nameof(keyBase64), ex);
        }
    }
}
