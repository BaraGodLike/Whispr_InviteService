namespace Application.Exceptions;

public sealed class InsufficientProofOfWorkException(string message) : Exception(message);
