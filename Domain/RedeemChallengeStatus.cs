namespace Domain;

public enum RedeemChallengeStatus
{
    Available = 0,
    NotFound = 1,
    Expired = 2,
    Used = 3,
    Locked = 4,
    Revoked = 5
}
