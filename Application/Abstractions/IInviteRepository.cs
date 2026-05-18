using Domain;

namespace Application.Abstractions;

public interface IInviteRepository
{
    Task CreateAsync(Invite invite, CancellationToken cancellationToken);
    Task<Invite?> GetByInviteIdAsync(Guid inviteId, CancellationToken cancellationToken);
    Task<byte[]?> TryRedeemAsync(
        Guid inviteId,
        byte[] pinHash,
        int expectedAttempts,
        DateTime utcNow,
        CancellationToken cancellationToken);
    Task<bool> TryRecordFailedRedeemAsync(
        Guid inviteId,
        int expectedAttempts,
        int nextAttempts,
        DateTime? lockedUntilUtc,
        CancellationToken cancellationToken);
    Task<bool> TryMarkRevokedAsync(Guid inviteId, CancellationToken cancellationToken);
    Task DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken);
}
