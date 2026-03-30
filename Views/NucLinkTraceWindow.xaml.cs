using IEC101MasterTester.Models;
using IEC101MasterTester.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace IEC101MasterTester.Views
{
    public partial class NucLinkTraceWindow : Window
    {
        private const int TimelineBucketCount = 60;
        private const int BucketSizeSeconds = 1;
        private readonly ObservableCollection<LineMonitorRow> _linkAViewRows = new ObservableCollection<LineMonitorRow>();
        private readonly ObservableCollection<LineMonitorRow> _linkBViewRows = new ObservableCollection<LineMonitorRow>();
        private readonly DispatcherTimer _refreshTimer;
        private bool _isPaused;
        private bool _autoScroll = true;
        private bool _isDraggingTimeline;
        private int _rowLimit = 50;
        private DateTime? _cursorTime;

        public NucLinkTraceWindow()
        {
            InitializeComponent();

            LinkAGrid.ItemsSource = _linkAViewRows;
            LinkBGrid.ItemsSource = _linkBViewRows;

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            Loaded += (s, e) =>
            {
                SyncRowLimit();
                RefreshViewport(true);
                _refreshTimer.Start();
            };

            Closed += (s, e) => _refreshTimer.Stop();
        }

        private MainViewModel ViewModel => DataContext as MainViewModel;

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (!_isPaused || _autoScroll)
            {
                RefreshViewport(false);
            }
        }

        private void RefreshViewport(bool force)
        {
            MainViewModel vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            List<LineMonitorRow> aRows = vm.NucTraceLinkA.ToList();
            List<LineMonitorRow> bRows = vm.NucTraceLinkB.ToList();

            DateTime latest = MaxTimestamp(aRows, bRows) ?? DateTime.Now;

            if (_autoScroll || !_cursorTime.HasValue || force)
            {
                _cursorTime = latest;
            }

            DateTime center = _cursorTime ?? latest;

            ReplaceRows(_linkAViewRows, TakeWindow(aRows, center, _rowLimit));
            ReplaceRows(_linkBViewRows, TakeWindow(bRows, center, _rowLimit));

            List<TimelineEvent> linkAEvents = BuildTimelineEvents(aRows);
            List<TimelineEvent> linkBEvents = BuildTimelineEvents(bRows);
            DrawTimeline(linkAEvents, linkBEvents, latest);
        }

        private static void ReplaceRows(ObservableCollection<LineMonitorRow> target, List<LineMonitorRow> rows)
        {
            target.Clear();
            foreach (LineMonitorRow row in rows)
            {
                target.Add(row);
            }
        }

        private static List<LineMonitorRow> TakeWindow(List<LineMonitorRow> rows, DateTime center, int rowLimit)
        {
            List<RowWithTime> parsed = rows
                .Select(r => new RowWithTime(r, ParseTime(r.Time)))
                .Where(r => r.Time.HasValue)
                .OrderByDescending(r => r.Time.Value)
                .ToList();

            if (parsed.Count == 0)
            {
                return rows.Take(rowLimit).ToList();
            }

            List<LineMonitorRow> window = parsed
                .Where(r => r.Time.Value >= center.AddSeconds(-20) && r.Time.Value <= center.AddSeconds(20))
                .Select(r => r.Row)
                .Take(rowLimit)
                .ToList();

            if (window.Count == 0)
            {
                window = parsed.Take(rowLimit).Select(r => r.Row).ToList();
            }

            return window;
        }

        private static List<TimelineEvent> BuildTimelineEvents(List<LineMonitorRow> rows)
        {
            return rows
                .Select(r => new TimelineEvent(ParseTime(r.Time), string.Equals(r.Direction, "TX", StringComparison.OrdinalIgnoreCase), IsFixedFrame(r)))
                .Where(r => r.Time.HasValue)
                .Select(r => r)
                .ToList();
        }

        private void DrawTimeline(List<TimelineEvent> aRows, List<TimelineEvent> bRows, DateTime latest)
        {
            double width = TimelineCanvas.ActualWidth;
            double height = TimelineCanvas.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            TimelineCanvas.Children.Clear();

            DateTime start = latest.AddSeconds(-(TimelineBucketCount * BucketSizeSeconds));
            DateTime end = latest;
            double laneTop = 16;
            double laneBottom = height - 10;
            double laneHeight = Math.Max(18, (laneBottom - laneTop) / 2.0);
            double aBaseY = laneTop + (laneHeight * 0.55);
            double bBaseY = laneTop + laneHeight + (laneHeight * 0.55);

            DrawLaneLabel("A", aBaseY - 9);
            DrawLaneLabel("B", bBaseY - 9);

            DrawBaseLine(aBaseY);
            DrawBaseLine(bBaseY);

            DrawTraceRows(aRows, start, end, width, aBaseY);
            DrawTraceRows(bRows, start, end, width, bBaseY);

            if (_cursorTime.HasValue)
            {
                double x = TimeToX(_cursorTime.Value, start, end, width);
                TimelineCanvas.Children.Add(new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 2,
                    Y2 = height - 2,
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 191, 0)),
                    StrokeThickness = 1.5
                });
            }
        }

        private void DrawLaneLabel(string text, double top)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                FontSize = 10
            };

            Canvas.SetLeft(label, 8);
            Canvas.SetTop(label, Math.Max(0, top));
            TimelineCanvas.Children.Add(label);
        }

        private void DrawBaseLine(double y)
        {
            TimelineCanvas.Children.Add(new Line
            {
                X1 = 56,
                X2 = Math.Max(56, TimelineCanvas.ActualWidth - 6),
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(90, 120, 140, 170)),
                StrokeThickness = 1
            });
        }

        private void DrawTraceRows(List<TimelineEvent> rows, DateTime start, DateTime end, double width, double baseY)
        {
            foreach (TimelineEvent row in rows)
            {
                if (!row.Time.HasValue || row.Time.Value < start || row.Time.Value > end)
                {
                    continue;
                }

                Brush stroke = row.IsTx
                    ? new SolidColorBrush(row.IsFixed ? Color.FromArgb(150, 56, 189, 248) : Color.FromArgb(235, 56, 189, 248))
                    : new SolidColorBrush(row.IsFixed ? Color.FromArgb(150, 34, 197, 94) : Color.FromArgb(235, 34, 197, 94));

                double x = TimeToX(row.Time.Value, start, end, width);
                double tipY = row.IsTx ? baseY - 11 : baseY + 11;

                TimelineCanvas.Children.Add(new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = baseY,
                    Y2 = tipY,
                    Stroke = stroke,
                    StrokeThickness = row.IsFixed ? 1.2 : 1.8,
                    SnapsToDevicePixels = true
                });
            }
        }

        private static bool IsFixedFrame(LineMonitorRow row)
        {
            return string.Equals(row.FrameType, "Fixed", StringComparison.OrdinalIgnoreCase)
                || (row.FrameType ?? string.Empty).IndexOf("Fixed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double TimeToX(DateTime time, DateTime start, DateTime end, double width)
        {
            double seconds = Math.Max(1, (end - start).TotalSeconds);
            double ratio = (time - start).TotalSeconds / seconds;
            ratio = Math.Max(0, Math.Min(1, ratio));
            return 56 + ((width - 64) * ratio);
        }

        private static DateTime? MaxTimestamp(List<LineMonitorRow> aRows, List<LineMonitorRow> bRows)
        {
            return aRows.Concat(bRows)
                .Select(r => ParseTime(r.Time))
                .Where(t => t.HasValue)
                .Select(t => t.Value)
                .OrderByDescending(t => t)
                .FirstOrDefault();
        }

        private static DateTime? ParseTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed;
            }

            return null;
        }

        private void SyncRowLimit()
        {
            ComboBoxItem selected = RowLimitComboBox.SelectedItem as ComboBoxItem;
            int parsed;
            if (selected != null && int.TryParse(selected.Content.ToString(), out parsed))
            {
                _rowLimit = parsed;
            }
        }

        private void PauseViewButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = PauseViewButton.IsChecked == true;
            if (_isPaused)
            {
                _autoScroll = false;
                AutoScrollButton.IsChecked = false;
            }
        }

        private void LiveButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = false;
            _autoScroll = true;
            PauseViewButton.IsChecked = false;
            AutoScrollButton.IsChecked = true;
            _cursorTime = null;
            RefreshViewport(true);
        }

        private void ClearViewButton_Click(object sender, RoutedEventArgs e)
        {
            _linkAViewRows.Clear();
            _linkBViewRows.Clear();
            TimelineCanvas.Children.Clear();
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = string.Format(CultureInfo.InvariantCulture, "NUC-LinkTrace-{0:yyyyMMdd-HHmmss}.csv", DateTime.Now)
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            using (StreamWriter writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Link,Time,Dir,Frame,Summary,Detail");
                WriteCsvRows(writer, "Link A", _linkAViewRows);
                WriteCsvRows(writer, "Link B", _linkBViewRows);
            }
        }

        private static void WriteCsvRows(StreamWriter writer, string link, IEnumerable<LineMonitorRow> rows)
        {
            foreach (LineMonitorRow row in rows)
            {
                writer.WriteLine(string.Join(",",
                    Csv(link),
                    Csv(row.Time),
                    Csv(row.Direction),
                    Csv(row.FrameType),
                    Csv(row.Summary),
                    Csv(row.Detail)));
            }
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string escaped = value.Replace("\"", "\"\"");
            return escaped.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + escaped + "\"" : escaped;
        }

        private void AutoScrollButton_Click(object sender, RoutedEventArgs e)
        {
            _autoScroll = AutoScrollButton.IsChecked == true;
            if (_autoScroll)
            {
                _isPaused = false;
                PauseViewButton.IsChecked = false;
                _cursorTime = null;
                RefreshViewport(true);
            }
        }

        private void RowLimitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SyncRowLimit();
            RefreshViewport(true);
        }

        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingTimeline = true;
            TimelineCanvas.CaptureMouse();
            ScrubToPoint(e.GetPosition(TimelineCanvas).X);
        }

        private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingTimeline = false;
            TimelineCanvas.ReleaseMouseCapture();
        }

        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingTimeline && e.LeftButton == MouseButtonState.Pressed)
            {
                ScrubToPoint(e.GetPosition(TimelineCanvas).X);
            }
        }

        private void TimelineCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDraggingTimeline && e.LeftButton != MouseButtonState.Pressed)
            {
                _isDraggingTimeline = false;
                TimelineCanvas.ReleaseMouseCapture();
            }
        }

        private void ScrubToPoint(double x)
        {
            MainViewModel vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            DateTime latest = MaxTimestamp(vm.NucTraceLinkA.ToList(), vm.NucTraceLinkB.ToList()) ?? DateTime.Now;
            DateTime start = latest.AddSeconds(-(TimelineBucketCount * BucketSizeSeconds));
            double width = Math.Max(1, TimelineCanvas.ActualWidth - 64);
            double ratio = Math.Max(0, Math.Min(1, (x - 56) / width));
            _cursorTime = start.AddSeconds((TimelineBucketCount * BucketSizeSeconds) * ratio);
            _autoScroll = false;
            _isPaused = true;
            PauseViewButton.IsChecked = true;
            AutoScrollButton.IsChecked = false;
            RefreshViewport(true);
        }

        private void NucLinkTraceWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshViewport(true);
        }

        private sealed class RowWithTime
        {
            public RowWithTime(LineMonitorRow row, DateTime? time)
            {
                Row = row;
                Time = time;
            }

            public LineMonitorRow Row { get; }
            public DateTime? Time { get; }
        }

        private sealed class TimelineEvent
        {
            public TimelineEvent(DateTime? time, bool isTx, bool isFixed)
            {
                Time = time;
                IsTx = isTx;
                IsFixed = isFixed;
            }

            public DateTime? Time { get; }
            public bool IsTx { get; }
            public bool IsFixed { get; }
        }
    }
}
