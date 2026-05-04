namespace AfrrCollector;

public sealed class ScrapedDataPoint
{
    public DateOnly Day { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public string BiddingZone { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public double PriceOfferedEuroPerMw { get; set; }
    public double Hour01 { get; set; }
    public double Hour02 { get; set; }
    public double Hour03 { get; set; }
    public double Hour04 { get; set; }
    public double Hour05 { get; set; }
    public double Hour06 { get; set; }
    public double Hour07 { get; set; }
    public double Hour08 { get; set; }
    public double Hour09 { get; set; }
    public double Hour10 { get; set; }
    public double Hour11 { get; set; }
    public double Hour12 { get; set; }
    public double Hour13 { get; set; }
    public double Hour14 { get; set; }
    public double Hour15 { get; set; }
    public double Hour16 { get; set; }
    public double Hour17 { get; set; }
    public double Hour18 { get; set; }
    public double Hour19 { get; set; }
    public double Hour20 { get; set; }
    public double Hour21 { get; set; }
    public double Hour22 { get; set; }
    public double Hour23 { get; set; }
    public double Hour24 { get; set; }
    public string ReferenceId { get; set; } = string.Empty;

    public IEnumerable<(string Time, string DataColumn, double Mw)> HourlyValues()
    {
        yield return ("00:00", "first-hour", Hour01);
        yield return ("01:00", "second-hour", Hour02);
        yield return ("02:00", "third-hour", Hour03);
        yield return ("03:00", "fourth-hour", Hour04);
        yield return ("04:00", "fifth-hour", Hour05);
        yield return ("05:00", "sixth-hour", Hour06);
        yield return ("06:00", "seventh-hour", Hour07);
        yield return ("07:00", "eight-hour", Hour08);
        yield return ("08:00", "ninth-hour", Hour09);
        yield return ("09:00", "tenth-hour", Hour10);
        yield return ("10:00", "eleventh-hour", Hour11);
        yield return ("11:00", "twelfth-hour", Hour12);
        yield return ("12:00", "thirteenth-hour", Hour13);
        yield return ("13:00", "fourteenth-hour", Hour14);
        yield return ("14:00", "fifteenth-hour", Hour15);
        yield return ("15:00", "sixteenth-hour", Hour16);
        yield return ("16:00", "seventeenth-hour", Hour17);
        yield return ("17:00", "eighteenth-hour", Hour18);
        yield return ("18:00", "nineteenth-hour", Hour19);
        yield return ("19:00", "twentieth-hour", Hour20);
        yield return ("20:00", "twenty-first-hour", Hour21);
        yield return ("21:00", "twenty-second-hour", Hour22);
        yield return ("22:00", "twenty-third-hour", Hour23);
        yield return ("23:00", "twenty-fourth-hour", Hour24);
    }
}
