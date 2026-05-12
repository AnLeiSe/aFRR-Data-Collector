using Microsoft.Data.Sqlite;

namespace AfrrCollector;

public sealed class DatabaseService
{
    private readonly string _dbPath;
    public DatabaseService(string dbPath) => _dbPath = dbPath;
    private string ConnectionString => $"Data Source={_dbPath}";

    public void EnsureCreated()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        CreateTable(connection, "scraped_points");
        CreateTable(connection, "mfrr_scraped_points");
    }

    private static void CreateTable(SqliteConnection connection, string table)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = $@"CREATE TABLE IF NOT EXISTS {table} (
id INTEGER PRIMARY KEY AUTOINCREMENT,
day TEXT NOT NULL,
region_code TEXT NOT NULL,
bidding_zone TEXT NOT NULL,
direction TEXT NOT NULL,
price_offered REAL NOT NULL,
hour01 REAL NOT NULL,hour02 REAL NOT NULL,hour03 REAL NOT NULL,hour04 REAL NOT NULL,hour05 REAL NOT NULL,hour06 REAL NOT NULL,
hour07 REAL NOT NULL,hour08 REAL NOT NULL,hour09 REAL NOT NULL,hour10 REAL NOT NULL,hour11 REAL NOT NULL,hour12 REAL NOT NULL,
hour13 REAL NOT NULL,hour14 REAL NOT NULL,hour15 REAL NOT NULL,hour16 REAL NOT NULL,hour17 REAL NOT NULL,hour18 REAL NOT NULL,
hour19 REAL NOT NULL,hour20 REAL NOT NULL,hour21 REAL NOT NULL,hour22 REAL NOT NULL,hour23 REAL NOT NULL,hour24 REAL NOT NULL,
reference_id TEXT NOT NULL
);";
        cmd.ExecuteNonQuery();

        var idx = connection.CreateCommand();
        idx.CommandText = $"CREATE INDEX IF NOT EXISTS idx_{table}_lookup ON {table}(day, region_code, bidding_zone, direction);";
        idx.ExecuteNonQuery();
    }

    public void SaveScrapedPoints(IEnumerable<ScrapedDataPoint> points) => SavePoints(points, "scraped_points");
    public void SaveMfrrScrapedPoints(IEnumerable<ScrapedDataPoint> points) => SavePoints(points, "mfrr_scraped_points");

    private void SavePoints(IEnumerable<ScrapedDataPoint> points, string table)
    {
        EnsureCreated();
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var tx = c.BeginTransaction();
        foreach (var p in points)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = $@"INSERT INTO {table}(day,region_code,bidding_zone,direction,price_offered,hour01,hour02,hour03,hour04,hour05,hour06,hour07,hour08,hour09,hour10,hour11,hour12,hour13,hour14,hour15,hour16,hour17,hour18,hour19,hour20,hour21,hour22,hour23,hour24,reference_id)
VALUES($d,$r,$z,$dir,$p,$h1,$h2,$h3,$h4,$h5,$h6,$h7,$h8,$h9,$h10,$h11,$h12,$h13,$h14,$h15,$h16,$h17,$h18,$h19,$h20,$h21,$h22,$h23,$h24,$ref);";
            cmd.Parameters.AddWithValue("$d", p.Day.ToString("yyyy-MM-dd")); cmd.Parameters.AddWithValue("$r", p.RegionCode); cmd.Parameters.AddWithValue("$z", p.BiddingZone); cmd.Parameters.AddWithValue("$dir", p.Direction); cmd.Parameters.AddWithValue("$p", p.PriceOfferedEuroPerMw);
            cmd.Parameters.AddWithValue("$h1", p.Hour01); cmd.Parameters.AddWithValue("$h2", p.Hour02); cmd.Parameters.AddWithValue("$h3", p.Hour03); cmd.Parameters.AddWithValue("$h4", p.Hour04); cmd.Parameters.AddWithValue("$h5", p.Hour05); cmd.Parameters.AddWithValue("$h6", p.Hour06);
            cmd.Parameters.AddWithValue("$h7", p.Hour07); cmd.Parameters.AddWithValue("$h8", p.Hour08); cmd.Parameters.AddWithValue("$h9", p.Hour09); cmd.Parameters.AddWithValue("$h10", p.Hour10); cmd.Parameters.AddWithValue("$h11", p.Hour11); cmd.Parameters.AddWithValue("$h12", p.Hour12);
            cmd.Parameters.AddWithValue("$h13", p.Hour13); cmd.Parameters.AddWithValue("$h14", p.Hour14); cmd.Parameters.AddWithValue("$h15", p.Hour15); cmd.Parameters.AddWithValue("$h16", p.Hour16); cmd.Parameters.AddWithValue("$h17", p.Hour17); cmd.Parameters.AddWithValue("$h18", p.Hour18);
            cmd.Parameters.AddWithValue("$h19", p.Hour19); cmd.Parameters.AddWithValue("$h20", p.Hour20); cmd.Parameters.AddWithValue("$h21", p.Hour21); cmd.Parameters.AddWithValue("$h22", p.Hour22); cmd.Parameters.AddWithValue("$h23", p.Hour23); cmd.Parameters.AddWithValue("$h24", p.Hour24);
            cmd.Parameters.AddWithValue("$ref", p.ReferenceId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public IReadOnlySet<(DateOnly Day, string RegionCode, string Direction)> LoadExistingDayRegionDirection(DateOnly from, DateOnly to, IReadOnlyCollection<string> regionCodes, string direction)
        => LoadExistingDayRegionDirection(from, to, regionCodes, direction, "scraped_points");
    public IReadOnlySet<(DateOnly Day, string RegionCode, string Direction)> LoadExistingMfrrDayRegionDirection(DateOnly from, DateOnly to, IReadOnlyCollection<string> regionCodes, string direction)
        => LoadExistingDayRegionDirection(from, to, regionCodes, direction, "mfrr_scraped_points");

    private IReadOnlySet<(DateOnly Day, string RegionCode, string Direction)> LoadExistingDayRegionDirection(DateOnly from, DateOnly to, IReadOnlyCollection<string> regionCodes, string direction, string table)
    {
        var existing = new HashSet<(DateOnly, string, string)>();
        foreach (var p in LoadBySelection(from, to, regionCodes, direction, table))
            existing.Add((p.Day, p.RegionCode, p.Direction.ToUpperInvariant()));
        return existing;
    }

    public IReadOnlyList<ScrapedDataPoint> LoadBySelection(DateOnly from, DateOnly to, IReadOnlyCollection<string> regionCodes, string direction)
        => LoadBySelection(from, to, regionCodes, direction, "scraped_points");
    public IReadOnlyList<ScrapedDataPoint> LoadMfrrBySelection(DateOnly from, DateOnly to, IReadOnlyCollection<string> regionCodes, string direction)
        => LoadBySelection(from, to, regionCodes, direction, "mfrr_scraped_points");

    private IReadOnlyList<ScrapedDataPoint> LoadBySelection(DateOnly from, DateOnly to, IReadOnlyCollection<string> regionCodes, string direction, string table)
    {
        EnsureCreated();
        var list = new List<ScrapedDataPoint>();
        if (regionCodes.Count == 0) return list;
        using var c = new SqliteConnection(ConnectionString); c.Open();
        var cmd = c.CreateCommand();
        var placeholders = new List<string>();
        var i = 0;
        foreach (var code in regionCodes) { var parameter = $"$r{i++}"; placeholders.Add(parameter); cmd.Parameters.AddWithValue(parameter, code); }
        cmd.CommandText = $@"SELECT day,region_code,bidding_zone,direction,price_offered,hour01,hour02,hour03,hour04,hour05,hour06,hour07,hour08,hour09,hour10,hour11,hour12,hour13,hour14,hour15,hour16,hour17,hour18,hour19,hour20,hour21,hour22,hour23,hour24,reference_id
FROM {table}
WHERE day >= $from AND day <= $to AND UPPER(direction)=UPPER($direction)
AND (region_code IN ({string.Join(',', placeholders)}) OR bidding_zone IN ({string.Join(',', placeholders)}))
ORDER BY bidding_zone, day;";
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd")); cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd")); cmd.Parameters.AddWithValue("$direction", direction);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (!DateOnly.TryParse(r.GetString(0), out var day)) continue;
            list.Add(new ScrapedDataPoint { Day = day, RegionCode = r.GetString(1), BiddingZone = r.GetString(2), Direction = r.GetString(3), PriceOfferedEuroPerMw = r.GetDouble(4),
                Hour01=r.GetDouble(5),Hour02=r.GetDouble(6),Hour03=r.GetDouble(7),Hour04=r.GetDouble(8),Hour05=r.GetDouble(9),Hour06=r.GetDouble(10),Hour07=r.GetDouble(11),Hour08=r.GetDouble(12),Hour09=r.GetDouble(13),Hour10=r.GetDouble(14),Hour11=r.GetDouble(15),Hour12=r.GetDouble(16),Hour13=r.GetDouble(17),Hour14=r.GetDouble(18),Hour15=r.GetDouble(19),Hour16=r.GetDouble(20),Hour17=r.GetDouble(21),Hour18=r.GetDouble(22),Hour19=r.GetDouble(23),Hour20=r.GetDouble(24),Hour21=r.GetDouble(25),Hour22=r.GetDouble(26),Hour23=r.GetDouble(27),Hour24=r.GetDouble(28),ReferenceId=r.GetString(29)});
        }
        return list;
    }

    public IReadOnlyList<ScrapedDataPoint> LoadAll() => LoadBySelection(DateOnly.MinValue, DateOnly.MaxValue, new[] { "DK1", "DK2", "FI", "NO1", "NO2", "NO3", "NO4", "NO5", "SE1", "SE2", "SE3", "SE4" }, "UP");
}
