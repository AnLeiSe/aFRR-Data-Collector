using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace AfrrCollector;

public sealed class NucsAfrrService
{
    private const string BaseUrl = "https://www.nucs.net/balancing/acceptedBalancingCapacityBids/show";

    private static readonly string[] ContractTypes =
    {
        "A01", "A02", "A03", "A04", "A06", "A07", "A13", "A14", "A15", "A16"
    };

    private static readonly string[] ProductTypes = { "A01", "A02", "UNDEFINED" };
    private static readonly string[] ReserveSources = { "A03", "A04", "A05", "NIL" };

    private readonly HttpClient _httpClient;

    public NucsAfrrService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
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
            var fetchResult = await FetchPageHtmlAsync(day, regions, direction, includeAtch: false, cancellationToken);
            var parsedRows = ParseRows(fetchResult.Html);
            if (parsedRows.Count == 0)
            {
                var atchFetchResult = await FetchPageHtmlAsync(day, regions, direction, includeAtch: true, cancellationToken);
                parsedRows = ParseRows(atchFetchResult.Html);

                if (parsedRows.Count == 0)
                {
                    throw BuildNoRowsException(day, fetchResult.Url, fetchResult.Html, atchFetchResult.Url, atchFetchResult.Html);
                }
            }

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

    private async Task<(Uri Url, string Html)> FetchPageHtmlAsync(
        DateOnly day,
        IReadOnlyCollection<RegionOption> regions,
        RegulationDirection direction,
        bool includeAtch,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(day, regions, direction, includeAtch);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("NUCS denied access (HTTP 403). Try again later.");
        }

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return (url, html);
    }


    private static InvalidOperationException BuildNoRowsException(DateOnly day, Uri requestUrl, string html, Uri secondRequestUrl, string secondHtml)
    {
        var tableRowCount = Regex.Matches(html, "<tr", RegexOptions.IgnoreCase).Count;
        var htmlLength = html.Length;
        var preview = htmlLength > 220 ? html[..220] + "..." : html;
        var secondTableRowCount = Regex.Matches(secondHtml, "<tr", RegexOptions.IgnoreCase).Count;
        var secondHtmlLength = secondHtml.Length;
        var secondPreview = secondHtmlLength > 220 ? secondHtml[..220] + "..." : secondHtml;
        var message =
            $"NUCS returned no parsable bid rows for {day:yyyy-MM-dd}. " +
            $"Attempt 1 URL (atch=false): {requestUrl}. Returned HTML length: {htmlLength}, '<tr' count: {tableRowCount}. Response preview: {preview} " +
            $"Attempt 2 URL (atch=true): {secondRequestUrl}. Returned HTML length: {secondHtmlLength}, '<tr' count: {secondTableRowCount}. Response preview: {secondPreview}";

        return new InvalidOperationException(message);
    }
    private static Uri BuildUrl(DateOnly day, IReadOnlyCollection<RegionOption> regions, RegulationDirection direction, bool includeAtch)
    {
        var sb = new StringBuilder(BaseUrl);
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
        Add("dv-datatable_length", "-1");
        Add("dv-datatable_start", "0");

        return new Uri(sb.ToString());
    }

    private static IReadOnlyList<(string Time, double Mw, double Price)> ParseRows(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//table[@id='dv-datatable']//tbody/tr")
            ?? new HtmlNodeCollection(doc.DocumentNode);
        var parsed = new List<(string Time, double Mw, double Price)>();

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./td");
            if (cells is null || cells.Count < 28)
            {
                continue;
            }

            var priceText = Clean(cells[2].InnerText);
            var price = TryParseNumber(priceText);
            if (!price.HasValue)
            {
                continue;
            }

            for (var hour = 1; hour <= 24; hour++)
            {
                var mwText = Clean(cells[2 + hour].InnerText);
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

    private static string Clean(string input) => WebUtility.HtmlDecode(input).Replace("\u00A0", " ").Trim();

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
