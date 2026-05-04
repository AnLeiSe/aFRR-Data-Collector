using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AfrrCollector;

public sealed class NucsAfrrService
{
    private const string BaseUrl = "https://www.nucs.net/balancing/acceptedBalancingCapacityBids/show";
    private const string DataTableUrl = "https://www.nucs.net/balancing/acceptedBalancingCapacityBids/getDataTableData/";
    private const int DataTablePageSize = 500;

    private static readonly string[] ContractTypes =
    {
        "A01", "A02", "A03", "A04", "A06", "A07", "A13", "A14", "A15", "A16"
    };

    private static readonly string[] ProductTypes = { "A01", "A02", "UNDEFINED" };
    private static readonly string[] ReserveSources = { "A03", "A04", "A05", "NIL" };

    private readonly HttpClient _httpClient;

    public NucsAfrrService(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            return;
        }

        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        _httpClient = new HttpClient(handler);
    }

    public async Task<IReadOnlyList<ScrapedDataPoint>> FetchRawPointsAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<RegionOption> regions,
        RegulationDirection direction,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            throw new ArgumentException("From date must be on or before To date.");
        }

        if (regions.Count == 0)
        {
            throw new ArgumentException("Choose at least one region.");
        }

        var points = new List<ScrapedDataPoint>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            foreach (var region in regions)
            {
                var singleRegion = new[] { region };
                var parsedRows = await FetchDataTableRowsAsync(day, singleRegion, direction, cancellationToken);
                points.AddRange(parsedRows.Select(r =>
                {
                    r.Day = day;
                    r.RegionCode = region.Code;
                    return r;
                }));
            }
        }

        return points;
    }



    public static IReadOnlyList<AfrrHourSummary> BuildHourlySummariesFromRaw(IEnumerable<ScrapedDataPoint> rawPoints)
    {
        return rawPoints
            .SelectMany(x => x.HourlyValues().Select(h => new { x.BiddingZone, x.Day, h.Time, h.Mw, x.PriceOfferedEuroPerMw }))
            .GroupBy(x => new { x.BiddingZone, x.Day, x.Time })
            .Select(g => new AfrrHourSummary
            {
                Zone = g.Key.BiddingZone,
                Day = g.Key.Day,
                Time = g.Key.Time,
                TotalMw = g.Sum(x => x.Mw),
                PriceAvg = g.Average(x => x.PriceOfferedEuroPerMw),
                PriceMin = g.Min(x => x.PriceOfferedEuroPerMw),
                PriceMax = g.Max(x => x.PriceOfferedEuroPerMw)
            })
            .OrderBy(x => x.Zone)
            .ThenBy(x => x.Day)
            .ThenBy(x => x.Time)
            .ToList();
    }

    public static IReadOnlyList<DailyVolumePoint> BuildDailyVolumeSeries(IEnumerable<AfrrHourSummary> hourly)
    {
        return hourly
            .GroupBy(x => new { x.Zone, x.Day })
            .Select(g => new DailyVolumePoint
            {
                Zone = g.Key.Zone,
                Day = g.Key.Day,
                Volume = g.Sum(x => x.Volume)
            })
            .OrderBy(x => x.Zone)
            .ThenBy(x => x.Day)
            .ToList();
    }

    private async Task<IReadOnlyList<ScrapedDataPoint>> FetchDataTableRowsAsync(
        DateOnly day,
        IReadOnlyCollection<RegionOption> regions,
        RegulationDirection direction,
        CancellationToken cancellationToken)
    {
        // Prime session/cookies like browser flow.
        var showUrl = BuildUrl(day, regions, direction, includeAtch: false);
        using (var showRequest = new HttpRequestMessage(HttpMethod.Get, showUrl))
        {
            showRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            using var showResponse = await _httpClient.SendAsync(showRequest, cancellationToken);
            showResponse.EnsureSuccessStatusCode();
        }

        var parsedRows = new List<ScrapedDataPoint>();
        var displayStart = 0;

        while (true)
        {
            var postUrl = BuildDataTableUrl(day, regions, direction, DataTablePageSize, displayStart);
            using var postRequest = new HttpRequestMessage(HttpMethod.Post, postUrl);
            postRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            postRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
            postRequest.Headers.Referrer = showUrl;
            postRequest.Content = new StringContent(BuildDataTableRequestBody(DataTablePageSize, displayStart), Encoding.UTF8, "application/json");

            using var postResponse = await _httpClient.SendAsync(postRequest, cancellationToken);
            if (postResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("NUCS denied access (HTTP 403) while requesting DataTable JSON.");
            }

            postResponse.EnsureSuccessStatusCode();
            var json = await postResponse.Content.ReadAsStringAsync(cancellationToken);
            var page = ParseDataTableJsonPage(json);
            parsedRows.AddRange(page.Rows);

            if (page.Rows.Count == 0 || parsedRows.Count >= page.TotalDisplayRecords)
            {
                break;
            }

            displayStart += DataTablePageSize;
        }

        if (parsedRows.Count == 0)
        {
            throw new InvalidOperationException(
                $"NUCS DataTable returned no parsable rows for {day:yyyy-MM-dd}. URL: {showUrl}");
        }

        return parsedRows;
    }

    private static string BuildDataTableRequestBody(int displayLength, int displayStart)
    {
        var payload = new
        {
            sEcho = 1,
            iColumns = 28,
            sColumns = "bidding_zone,direction,price_offered,first-hour,second-hour,third-hour,fourth-hour,fifth-hour,sixth-hour,seventh-hour,eight-hour,ninth-hour,tenth-hour,eleventh-hour,twelfth-hour,thirteenth-hour,fourteenth-hour,fifteenth-hour,sixteenth-hour,seventeenth-hour,eighteenth-hour,nineteenth-hour,twentieth-hour,twenty-first-hour,twenty-second-hour,twenty-third-hour,twenty-fourth-hour,reference_id",
            iDisplayStart = displayStart,
            iDisplayLength = displayLength,
            amDataProp = Enumerable.Range(0, 28).ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }

    private static Uri BuildDataTableUrl(DateOnly day, IReadOnlyCollection<RegionOption> regions, RegulationDirection direction, int pageLength, int pageStart)
        => BuildUrlCore(day, regions, direction, includeAtch: false, DataTableUrl, pageLength, pageStart);
    private static Uri BuildUrl(DateOnly day, IReadOnlyCollection<RegionOption> regions, RegulationDirection direction, bool includeAtch)
        => BuildUrlCore(day, regions, direction, includeAtch, BaseUrl, -1, 0);

    private static Uri BuildUrlCore(DateOnly day, IReadOnlyCollection<RegionOption> regions, RegulationDirection direction, bool includeAtch, string baseUrl, int pageLength, int pageStart)
    {
        var sb = new StringBuilder(baseUrl);
        sb.Append("?");

        void Add(string key, string value)
        {
            if (sb[sb.Length - 1] != '?')
            {
                sb.Append('&');
            }

            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
        }

        Add("name", string.Empty);
        Add("defaultValue", "false");
        Add("viewType", "TABLE");
        Add("areaType", "MBA");
        Add("atch", includeAtch ? "true" : "false");
        Add("dateTime.dateTime", $"{day:dd.MM.yyyy} 00:00|CET|DAY");
        Add("areaSelectType", "USER_SELECTED");

        foreach (var region in regions)
        {
            Add("schedulingArea.values", region.SchedulingValue);
        }

        foreach (var contract in ContractTypes)
        {
            Add("contractTypes.values", contract);
        }

        foreach (var product in ProductTypes)
        {
            Add("typesOfProduct.values", product);
        }

        foreach (var source in ReserveSources)
        {
            Add("reserveSources.values", source);
        }

        Add("reserveType.values", "A51");
        Add("balancingTypes", "SECONDARY");
        Add("energyDirection.values", direction == RegulationDirection.Up ? "UP" : "DOWN");
        Add("dv-datatable_length", pageLength.ToString(CultureInfo.InvariantCulture));
        Add("dv-datatable_start", pageStart.ToString(CultureInfo.InvariantCulture));

        return new Uri(sb.ToString());
    }

    private static (IReadOnlyList<ScrapedDataPoint> Rows, int TotalDisplayRecords) ParseDataTableJsonPage(string json)
    {
        var parsed = new List<ScrapedDataPoint>();
        using var doc = JsonDocument.Parse(json);
        var totalDisplay = doc.RootElement.TryGetProperty("iTotalDisplayRecords", out var totalElement)
            && totalElement.TryGetInt32(out var totalValue)
            ? totalValue
            : int.MaxValue;

        if (!doc.RootElement.TryGetProperty("aaData", out var dataRows) || dataRows.ValueKind != JsonValueKind.Array)
        {
            return (parsed, totalDisplay);
        }

        foreach (var row in dataRows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 28)
            {
                continue;
            }

            var point = new ScrapedDataPoint
            {
                BiddingZone = Clean(row[0].ToString()),
                Direction = Clean(row[1].ToString()),
                PriceOfferedEuroPerMw = TryParseNumber(Clean(row[2].ToString())) ?? 0,
                Hour01 = TryParseNumber(Clean(row[3].ToString())) ?? 0,
                Hour02 = TryParseNumber(Clean(row[4].ToString())) ?? 0,
                Hour03 = TryParseNumber(Clean(row[5].ToString())) ?? 0,
                Hour04 = TryParseNumber(Clean(row[6].ToString())) ?? 0,
                Hour05 = TryParseNumber(Clean(row[7].ToString())) ?? 0,
                Hour06 = TryParseNumber(Clean(row[8].ToString())) ?? 0,
                Hour07 = TryParseNumber(Clean(row[9].ToString())) ?? 0,
                Hour08 = TryParseNumber(Clean(row[10].ToString())) ?? 0,
                Hour09 = TryParseNumber(Clean(row[11].ToString())) ?? 0,
                Hour10 = TryParseNumber(Clean(row[12].ToString())) ?? 0,
                Hour11 = TryParseNumber(Clean(row[13].ToString())) ?? 0,
                Hour12 = TryParseNumber(Clean(row[14].ToString())) ?? 0,
                Hour13 = TryParseNumber(Clean(row[15].ToString())) ?? 0,
                Hour14 = TryParseNumber(Clean(row[16].ToString())) ?? 0,
                Hour15 = TryParseNumber(Clean(row[17].ToString())) ?? 0,
                Hour16 = TryParseNumber(Clean(row[18].ToString())) ?? 0,
                Hour17 = TryParseNumber(Clean(row[19].ToString())) ?? 0,
                Hour18 = TryParseNumber(Clean(row[20].ToString())) ?? 0,
                Hour19 = TryParseNumber(Clean(row[21].ToString())) ?? 0,
                Hour20 = TryParseNumber(Clean(row[22].ToString())) ?? 0,
                Hour21 = TryParseNumber(Clean(row[23].ToString())) ?? 0,
                Hour22 = TryParseNumber(Clean(row[24].ToString())) ?? 0,
                Hour23 = TryParseNumber(Clean(row[25].ToString())) ?? 0,
                Hour24 = TryParseNumber(Clean(row[26].ToString())) ?? 0,
                ReferenceId = Clean(row[27].ToString())
            };

            parsed.Add(point);
        }

        return (parsed, totalDisplay);
    }

    private static string Clean(string input)
    {
        var normalized = input.Replace("\\/", "/");
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = Regex.Replace(normalized, "<[^>]+>", string.Empty);
        normalized = normalized.Replace("\u00A0", " ");
        return normalized.Trim();
    }

    private static double? TryParseNumber(string text)
    {
        var cleaned = text.Replace(" ", string.Empty).Replace("€", string.Empty);

        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.GetCultureInfo("da-DK"), out var nordic))
        {
            return nordic;
        }

        return null;
    }
}
