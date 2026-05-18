namespace Application.Models;

public sealed record PowChallenge(string Nonce, int Difficulty, DateTime ExpiresAtUtc);
