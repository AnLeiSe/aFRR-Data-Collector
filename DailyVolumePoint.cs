namespace AfrrCollector;

public sealed class DailyVolumePoint
{
    public required DateOnly Day { get; init; }
    public required double Volume { get; init; }
}
