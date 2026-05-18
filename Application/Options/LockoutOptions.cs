namespace Application.Options;

public sealed class LockoutOptions
{
    public int FirstThreshold { get; set; } = 3;
    public TimeSpan FirstDuration { get; set; } = TimeSpan.FromMinutes(2);
    public int SecondThreshold { get; set; } = 5;
    public TimeSpan SecondDuration { get; set; } = TimeSpan.FromMinutes(5);
}
