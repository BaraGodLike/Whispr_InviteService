namespace Application.Options;

public sealed class InviteOptions
{
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(15);
    public int PinLength { get; set; } = 8;
    public string PinAlphabet { get; set; } = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public int ServerSecretSizeBytes { get; set; } = 32;
    public int RevokeTokenSizeBytes { get; set; } = 32;
}
