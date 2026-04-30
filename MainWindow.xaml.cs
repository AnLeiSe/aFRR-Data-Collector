using System.Collections.ObjectModel;
using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace AfrrCollector;

public partial class MainWindow : Window
{
    private readonly NucsAfrrService _service = new();
    private readonly ObservableCollection<AfrrHourSummary> _rows = new();

    private static readonly IReadOnlyList<RegionOption> Regions =
    new List<RegionOption>
    {
        new() { Code = "DK1", SchedulingValue = "CTY|10Y1001A1001A65H!MBA|10YDK-1--------W" },
        new() { Code = "DK2", SchedulingValue = "CTY|10Y1001A1001A65H!MBA|10YDK-2--------M" },
        new() { Code = "FI", SchedulingValue = "CTY|10YFI-1--------U!MBA|10YFI-1--------U" },
        new() { Code = "NO1", SchedulingValue = "CTY|10YNO-0--------C!MBA|10YNO-1--------2" },
        new() { Code = "NO2", SchedulingValue = "CTY|10YNO-0--------C!MBA|10YNO-2--------T" },
        new() { Code = "NO3", SchedulingValue = "CTY|10YNO-0--------C!MBA|10YNO-3--------J" },
        new() { Code = "NO4", SchedulingValue = "CTY|10YNO-0--------C!MBA|10YNO-4--------9" },
        new() { Code = "NO5", SchedulingValue = "CTY|10YNO-0--------C!MBA|10Y1001A1001A48H" },
        new() { Code = "SE1", SchedulingValue = "CTY|10YSE-1--------K!MBA|10Y1001A1001A44P" },
        new() { Code = "SE2", SchedulingValue = "CTY|10YSE-1--------K!MBA|10Y1001A1001A45N" },
        new() { Code = "SE3", SchedulingValue = "CTY|10YSE-1--------K!MBA|10Y1001A1001A46L" },
        new() { Code = "SE4", SchedulingValue = "CTY|10YSE-1--------K!MBA|10Y1001A1001A47J" }
    };

    public MainWindow()
    {
        InitializeComponent();

        RegionsListBox.ItemsSource = Regions;
        DirectionComboBox.ItemsSource = Enum.GetValues<RegulationDirection>();
        DirectionComboBox.SelectedItem = RegulationDirection.Up;

        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-7);
        ToDatePicker.SelectedDate = DateTime.Today;

        ResultsDataGrid.ItemsSource = _rows;
        DailyVolumePlot.Model = CreateEmptyPlot();
    }

    private async void FetchButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var from = FromDatePicker.SelectedDate?.Date;
            var to = ToDatePicker.SelectedDate?.Date;
            var direction = DirectionComboBox.SelectedItem as RegulationDirection?;
            var selectedRegions = RegionsListBox.SelectedItems.Cast<RegionOption>().ToArray();

            if (from is null || to is null)
            {
                MessageBox.Show("Select valid From/To dates.", "Input error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (direction is null)
            {
                MessageBox.Show("Select UP or DOWN direction.", "Input error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedRegions.Length == 0)
            {
                MessageBox.Show("Select at least one region.", "Input error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FetchButton.IsEnabled = false;
            StatusText.Text = "Fetching data from NUCS...";
            ErrorMessageTextBox.Text = string.Empty;

            var hourly = await _service.FetchHourlySummariesAsync(
                DateOnly.FromDateTime(from.Value),
                DateOnly.FromDateTime(to.Value),
                selectedRegions,
                direction.Value);

            _rows.Clear();
            foreach (var row in hourly)
            {
                _rows.Add(row);
            }

            var daily = NucsAfrrService.BuildDailyVolumeSeries(hourly);
            DailyVolumePlot.Model = CreateDailyVolumePlot(daily);

            var dayCount = hourly.Select(x => x.Day).Distinct().Count();
            StatusText.Text = $"Done. {hourly.Count} hourly rows across {dayCount} day(s).";
            ErrorMessageTextBox.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Failed.";
            ErrorMessageTextBox.Text = ex.ToString();
            MessageBox.Show(ex.Message, "Fetch error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            FetchButton.IsEnabled = true;
        }
    }

    private static PlotModel CreateEmptyPlot()
    {
        var model = new PlotModel { Title = "Daily traded volume (Total MW * Price Avg)", IsLegendVisible = true };
        model.Legends.Add(new Legend { LegendTitle = "Zone colors", LegendPosition = LegendPosition.TopRight });
        model.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "yyyy-MM-dd",
            IntervalType = DateTimeIntervalType.Days,
            MinorIntervalType = DateTimeIntervalType.Days,
            MajorStep = 1,
            MinorStep = 1,
            Angle = 30
        });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Volume" });
        return model;
    }

    private static PlotModel CreateDailyVolumePlot(IEnumerable<DailyVolumePoint> points)
    {
        var model = CreateEmptyPlot();
        foreach (var zoneGroup in points.GroupBy(x => x.Zone).OrderBy(g => g.Key))
        {
            var line = new LineSeries { Title = zoneGroup.Key, StrokeThickness = 2 };

            foreach (var p in zoneGroup.OrderBy(x => x.Day))
            {
                var dateTime = p.Day.ToDateTime(TimeOnly.MinValue);
                line.Points.Add(new DataPoint(DateTimeAxis.ToDouble(dateTime), p.Volume));
            }

            model.Series.Add(line);
        }
        return model;
    }
}
