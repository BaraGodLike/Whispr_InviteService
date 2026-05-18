namespace Application.Abstractions;

public interface IInviteHasher
{
    byte[] HashPin(string normalizedPin);
    byte[] HashToken(byte[] token);
    bool AreEqual(byte[] left, byte[] right);
}
