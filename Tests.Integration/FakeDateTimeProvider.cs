using Application.Abstractions;

namespace Tests.Integration;

internal sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; }
}
