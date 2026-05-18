using Application.Models;

namespace Application.Abstractions;

public interface IPowService
{
    PowChallenge CreateChallenge(Guid inviteId, int attempts, DateTime nowUtc);
    void ValidateChallenge(
        Guid inviteId,
        int attempts,
        string normalizedPin,
        string nonce,
        ulong solution,
        DateTime nowUtc);
}
