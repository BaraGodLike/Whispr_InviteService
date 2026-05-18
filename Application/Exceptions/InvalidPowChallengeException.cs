namespace Application.Exceptions;

public sealed class InvalidPowChallengeException(string message) : Exception(message);
