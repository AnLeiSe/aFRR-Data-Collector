namespace AfrrCollector;

public sealed class DailyVolumePoint
{
    public required string Zone { get; init; }
    public required DateOnly Day { get; init; }
    public required double Volume { get; init; }
}
