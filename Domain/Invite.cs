namespace Domain;

public sealed class Invite
{
    public long Id { get; init; }
    public Guid InviteId { get; init; }
    public byte[] ProtectedServerSecret { get; init; } = [];
    public byte[] PinHash { get; init; } = [];
    public byte[] RevokeTokenHash { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public bool IsUsed { get; init; }
    public int Attempts { get; init; }
    public DateTime? LockedUntilUtc { get; init; }
    public bool IsRevoked { get; init; }

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsLocked(DateTime nowUtc) =>
        LockedUntilUtc.HasValue && LockedUntilUtc.Value > nowUtc;

    public int GetCurrentDifficulty(int baseDifficulty, int maxDifficulty) =>
        Math.Min(maxDifficulty, baseDifficulty + Attempts);

    public FailedRedeemState GetFailedRedeemState(
        DateTime nowUtc,
        int firstLockThreshold,
        TimeSpan firstLockDuration,
        int secondLockThreshold,
        TimeSpan secondLockDuration)
    {
        var nextAttempts = Attempts + 1;
        var lockedUntilUtc = nextAttempts switch
        {
            _ when nextAttempts >= secondLockThreshold => nowUtc.Add(secondLockDuration),
            _ when nextAttempts >= firstLockThreshold => nowUtc.Add(firstLockDuration),
            _ => (DateTime?)null
        };

        return new FailedRedeemState(nextAttempts, lockedUntilUtc);
    }
}
