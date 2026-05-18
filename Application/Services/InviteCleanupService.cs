using Application.Abstractions;

namespace Application.Services;

public sealed class InviteCleanupService(
    IInviteRepository inviteRepository,
    IDateTimeProvider dateTimeProvider)
{
    public Task DeleteExpiredInvitesAsync(CancellationToken cancellationToken) =>
        inviteRepository.DeleteExpiredAsync(dateTimeProvider.UtcNow, cancellationToken);
}
