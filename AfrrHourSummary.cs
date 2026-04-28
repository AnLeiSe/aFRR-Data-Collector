namespace AfrrCollector;

public sealed class AfrrHourSummary
{
    public required DateOnly Day { get; init; }
    public required string Time { get; init; }
    public required double TotalMw { get; init; }
    public required double PriceAvg { get; init; }
    public required double PriceMin { get; init; }
    public required double PriceMax { get; init; }

    public double Volume => TotalMw * PriceAvg;
}
