namespace Domain;

public readonly record struct FailedRedeemState(int Attempts, DateTime? LockedUntilUtc);
