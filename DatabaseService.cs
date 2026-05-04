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
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS scraped_points (
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
    }

    public void SaveScrapedPoints(IEnumerable<ScrapedDataPoint> points)
    {
        EnsureCreated();
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var tx = c.BeginTransaction();
        foreach (var p in points)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = @"INSERT INTO scraped_points(day,region_code,bidding_zone,direction,price_offered,hour01,hour02,hour03,hour04,hour05,hour06,hour07,hour08,hour09,hour10,hour11,hour12,hour13,hour14,hour15,hour16,hour17,hour18,hour19,hour20,hour21,hour22,hour23,hour24,reference_id)
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

    public IReadOnlyList<ScrapedDataPoint> LoadAll()
    {
        EnsureCreated();
        var list = new List<ScrapedDataPoint>();
        using var c = new SqliteConnection(ConnectionString); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT day,region_code,bidding_zone,direction,price_offered,hour01,hour02,hour03,hour04,hour05,hour06,hour07,hour08,hour09,hour10,hour11,hour12,hour13,hour14,hour15,hour16,hour17,hour18,hour19,hour20,hour21,hour22,hour23,hour24,reference_id FROM scraped_points ORDER BY bidding_zone, day;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ScrapedDataPoint { Day = DateOnly.Parse(r.GetString(0)), RegionCode = r.GetString(1), BiddingZone = r.GetString(2), Direction = r.GetString(3), PriceOfferedEuroPerMw = r.GetDouble(4), Hour01 = r.GetDouble(5), Hour02 = r.GetDouble(6), Hour03 = r.GetDouble(7), Hour04 = r.GetDouble(8), Hour05 = r.GetDouble(9), Hour06 = r.GetDouble(10), Hour07 = r.GetDouble(11), Hour08 = r.GetDouble(12), Hour09 = r.GetDouble(13), Hour10 = r.GetDouble(14), Hour11 = r.GetDouble(15), Hour12 = r.GetDouble(16), Hour13 = r.GetDouble(17), Hour14 = r.GetDouble(18), Hour15 = r.GetDouble(19), Hour16 = r.GetDouble(20), Hour17 = r.GetDouble(21), Hour18 = r.GetDouble(22), Hour19 = r.GetDouble(23), Hour20 = r.GetDouble(24), Hour21 = r.GetDouble(25), Hour22 = r.GetDouble(26), Hour23 = r.GetDouble(27), Hour24 = r.GetDouble(28), ReferenceId = r.GetString(29) });
        }
        return list;
    }
}
