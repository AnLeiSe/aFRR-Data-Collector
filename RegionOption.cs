namespace AfrrCollector;

public sealed class RegionOption
{
    public required string Code { get; init; }
    public required string SchedulingValue { get; init; }

    public override string ToString() => Code;
}
