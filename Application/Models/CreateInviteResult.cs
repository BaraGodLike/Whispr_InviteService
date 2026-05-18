namespace Application.Models;

public sealed record CreateInviteResult(
    Guid InviteId,
    byte[] ServerSecret,
    string Pin,
    byte[] RevokeToken,
    DateTime ExpiresAtUtc);
