namespace Application.Options;

public sealed class PowOptions
{
    public int BaseDifficulty { get; set; } = 18;
    public int MaxDifficulty { get; set; } = 26;
    public TimeSpan ChallengeTtl { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan BucketDuration { get; set; } = TimeSpan.FromMinutes(1);
    public string SigningKeyBase64 { get; set; } = string.Empty;
}
