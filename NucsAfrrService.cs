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

    public async Task<IReadOnlyList<AfrrHourSummary>> FetchHourlySummariesAsync(
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

        var aggregated = new Dictionary<(DateOnly Day, string Time), List<(double Mw, double Price)>>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var parsedRows = await FetchDataTableRowsAsync(day, regions, direction, cancellationToken);

            foreach (var row in parsedRows)
            {
                var key = (day, row.Time);
                if (!aggregated.TryGetValue(key, out var rows))
                {
                    rows = new List<(double Mw, double Price)>();
                    aggregated[key] = rows;
                }

                rows.Add((row.Mw, row.Price));
            }
        }

        return aggregated
            .Select(kvp =>
            {
                var mwSum = kvp.Value.Sum(x => x.Mw);
                var prices = kvp.Value.Select(x => x.Price).ToArray();

                return new AfrrHourSummary
                {
                    Day = kvp.Key.Day,
                    Time = kvp.Key.Time,
                    TotalMw = mwSum,
                    PriceAvg = prices.Length == 0 ? 0 : prices.Average(),
                    PriceMin = prices.Length == 0 ? 0 : prices.Min(),
                    PriceMax = prices.Length == 0 ? 0 : prices.Max()
                };
            })
            .OrderBy(x => x.Day)
            .ThenBy(x => x.Time)
            .ToList();
    }

    public static IReadOnlyList<DailyVolumePoint> BuildDailyVolumeSeries(IEnumerable<AfrrHourSummary> hourly)
    {
        return hourly
            .GroupBy(x => x.Day)
            .Select(g => new DailyVolumePoint
            {
                Day = g.Key,
                Volume = g.Sum(x => x.Volume)
            })
            .OrderBy(x => x.Day)
            .ToList();
    }

    private async Task<IReadOnlyList<(string Time, double Mw, double Price)>> FetchDataTableRowsAsync(
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

        var postUrl = BuildDataTableUrl(day, regions, direction);
        using var postRequest = new HttpRequestMessage(HttpMethod.Post, postUrl);
        postRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        postRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
        postRequest.Headers.Referrer = showUrl;
        postRequest.Content = new StringContent(BuildDataTableRequestBody(), Encoding.UTF8, "application/json");

        using var postResponse = await _httpClient.SendAsync(postRequest, cancellationToken);
        if (postResponse.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("NUCS denied access (HTTP 403) while requesting DataTable JSON.");
        }

        postResponse.EnsureSuccessStatusCode();
        var json = await postResponse.Content.ReadAsStringAsync(cancellationToken);
        var parsedRows = ParseDataTableJson(json);

        if (parsedRows.Count == 0)
        {
            var preview = json.Length > 350 ? json[..350] + "..." : json;
            throw new InvalidOperationException(
                $"NUCS DataTable returned no parsable rows for {day:yyyy-MM-dd}. URL: {postUrl}. JSON length: {json.Length}. Preview: {preview}");
        }

        return parsedRows;
    }

    private static string BuildDataTableRequestBody()
        => """
           {"sEcho":1,"iColumns":28,"sColumns":"bidding_zone,direction,price_offered,first-hour,second-hour,third-hour,fourth-hour,fifth-hour,sixth-hour,seventh-hour,eight-hour,ninth-hour,tenth-hour,eleventh-hour,twelfth-hour,thirteenth-hour,fourteenth-hour,fifteenth-hour,sixteenth-hour,seventeenth-hour,eighteenth-hour,nineteenth-hour,twentieth-hour,twenty-first-hour,twenty-second-hour,twenty-third-hour,twenty-fourth-hour,reference_id","iDisplayStart":0,"iDisplayLength":1000,"amDataProp":[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27]}
           """;

    private static Uri BuildDataTableUrl(DateOnly day, IReadOnlyCollection<RegionOption> regions, RegulationDirection direction)
        => BuildUrlCore(day, regions, direction, includeAtch: false, DataTableUrl, 1000);
    private static Uri BuildUrl(DateOnly day, IReadOnlyCollection<RegionOption> regions, RegulationDirection direction, bool includeAtch)
        => BuildUrlCore(day, regions, direction, includeAtch, BaseUrl, -1);

    private static Uri BuildUrlCore(DateOnly day, IReadOnlyCollection<RegionOption> regions, RegulationDirection direction, bool includeAtch, string baseUrl, int pageLength)
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
        Add("dv-datatable_start", "0");

        return new Uri(sb.ToString());
    }

    private static IReadOnlyList<(string Time, double Mw, double Price)> ParseDataTableJson(string json)
    {
        var parsed = new List<(string Time, double Mw, double Price)>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("aaData", out var dataRows) || dataRows.ValueKind != JsonValueKind.Array)
        {
            return parsed;
        }

        foreach (var row in dataRows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 28)
            {
                continue;
            }

            var priceText = Clean(row[2].ToString());
            var price = TryParseNumber(priceText);
            if (!price.HasValue)
            {
                continue;
            }

            for (var hour = 1; hour <= 24; hour++)
            {
                var mwText = Clean(row[2 + hour].ToString());
                var mw = TryParseNumber(mwText) ?? 0;
                if (mw <= 0)
                {
                    continue;
                }

                var time = $"{hour - 1:00}:00";
                parsed.Add((time, mw, price.Value));
            }
        }

        return parsed;
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
