namespace Domain;

public enum RedeemInviteStatus
{
    Succeeded = 0,
    NotFound = 1,
    Expired = 2,
    Used = 3,
    InvalidPin = 4,
    Locked = 5,
    Revoked = 6
}
