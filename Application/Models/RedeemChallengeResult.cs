using Domain;

namespace Application.Models;

public sealed record RedeemChallengeResult(
    RedeemChallengeStatus Status,
    string? Nonce,
    int? Difficulty,
    int Attempts,
    DateTime? ExpiresAtUtc,
    DateTime? LockedUntilUtc);
