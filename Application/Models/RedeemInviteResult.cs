using Domain;

namespace Application.Models;

public sealed record RedeemInviteResult(
    RedeemInviteStatus Status,
    byte[]? ServerSecret,
    DateTime? LockedUntilUtc);
