using IEC101MasterTester.Controls;
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
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace IEC101MasterTester.Views
{
    public partial class NucLinkTraceWindow : Window
    {
        private const int TimelineBucketCount = 300;
        private const int MaxSamples = 300;
        private const double BucketSizeSeconds = 0.2;
        private const double TimelineLeftMargin = 72;
        private const double TimelineRightMargin = 14;
        private readonly ObservableCollection<LineMonitorRow> _linkAViewRows = new ObservableCollection<LineMonitorRow>();
        private readonly ObservableCollection<LineMonitorRow> _linkBViewRows = new ObservableCollection<LineMonitorRow>();
        private readonly DispatcherTimer _refreshTimer;
        private readonly List<float> _laneABuffer = new List<float>();
        private readonly List<float> _laneBBuffer = new List<float>();
        private bool _isPaused;
        private bool _followRight = true;
        private bool _suppressGridSelectionSync;
        private int _rowLimit = 50;
        private float _laneAPrev = 0.08f;
        private float _laneBPrev = 0.08f;
        private DateTime? _lastSampleTime;
        private DateTime? _selectedTime;
        private DateTime? _windowStartTime;
        private DateTime? _windowEndTime;
        private bool _isInspectFrozen;
        private DateTime? _inspectWindowStartTime;
        private DateTime? _inspectWindowEndTime;
        private DateTime? _inspectBucketStartTime;
        private DateTime? _selectedAnchorTime;

        public NucLinkTraceWindow()
        {
            InitializeComponent();

            LinkAGrid.ItemsSource = _linkAViewRows;
            LinkBGrid.ItemsSource = _linkBViewRows;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _refreshTimer.Tick += RefreshTimer_Tick;

            Loaded += (s, e) =>
            {
                TimelineTape.SelectedTimeChanged += TimelineTape_SelectedTimeChanged;
                SyncRowLimit();
                SampleTape();
                RefreshViewport(true);
                _refreshTimer.Start();
            };

            Closed += (s, e) =>
            {
                TimelineTape.SelectedTimeChanged -= TimelineTape_SelectedTimeChanged;
                _refreshTimer.Stop();
            };
        }

        private MainViewModel ViewModel
        {
            get { return DataContext as MainViewModel; }
        }

        private double ViewportDurationSeconds
        {
            get { return TimelineBucketCount * BucketSizeSeconds; }
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (!_isPaused || _followRight)
            {
                SampleTape();
                RefreshViewport(false);
            }
        }

        private void SampleTape()
        {
            MainViewModel vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            List<RowWithTime> parsedA = ParseRows(vm.NucTraceLinkA);
            List<RowWithTime> parsedB = ParseRows(vm.NucTraceLinkB);
            DateTime sampleEnd = MaxTimestamp(parsedA, parsedB) ?? DateTime.Now;
            DateTime sampleStart = _lastSampleTime ?? sampleEnd.AddSeconds(-BucketSizeSeconds);
            if (sampleEnd <= sampleStart)
            {
                return;
            }

            bool isLinkAActive = (vm.RedundancyActiveLinkText ?? string.Empty).IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isLinkBActive = (vm.RedundancyActiveLinkText ?? string.Empty).IndexOf("Backup", StringComparison.OrdinalIgnoreCase) >= 0;

            AppendSample(_laneABuffer, ref _laneAPrev, ComputeLaneIntensity(GetSampleEvents(parsedA, sampleStart, sampleEnd), !isLinkAActive));
            AppendSample(_laneBBuffer, ref _laneBPrev, ComputeLaneIntensity(GetSampleEvents(parsedB, sampleStart, sampleEnd), !isLinkBActive));
            _lastSampleTime = sampleEnd;
        }

        private void RefreshViewport(bool forceLiveSelection)
        {
            MainViewModel vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            List<RowWithTime> parsedA = ParseRows(vm.NucTraceLinkA);
            List<RowWithTime> parsedB = ParseRows(vm.NucTraceLinkB);

            DateTime latest = MaxTimestamp(parsedA, parsedB) ?? DateTime.Now;
            if (latest < DateTime.MinValue.AddSeconds(ViewportDurationSeconds))
            {
                latest = DateTime.MinValue.AddSeconds(ViewportDurationSeconds);
            }

            if (latest > DateTime.MaxValue)
            {
                latest = DateTime.Now;
            }

            DateTime windowEnd = _isInspectFrozen && _inspectWindowEndTime.HasValue
                ? _inspectWindowEndTime.Value
                : latest;
            DateTime windowStart = _isInspectFrozen && _inspectWindowStartTime.HasValue
                ? _inspectWindowStartTime.Value
                : latest.AddSeconds(-ViewportDurationSeconds);
            _windowStartTime = windowStart;
            _windowEndTime = windowEnd;

            if (_followRight || !_selectedTime.HasValue || forceLiveSelection)
            {
                _selectedTime = windowEnd;
                _selectedAnchorTime = null;
            }
            else
            {
                _selectedTime = ClampSelectedTime(_selectedTime.Value, windowStart, windowEnd);
            }

            if (_isInspectFrozen && _selectedTime.HasValue)
            {
                DateTime anchorTime = _selectedAnchorTime ?? _selectedTime.Value;
                DateTime bucketStart = _inspectBucketStartTime ?? GetBucketStart(windowStart, anchorTime);
                ReplaceRows(_linkAViewRows, TakeInspectRows(parsedA, bucketStart, anchorTime, _rowLimit));
                ReplaceRows(_linkBViewRows, TakeInspectRows(parsedB, bucketStart, anchorTime, _rowLimit));
            }
            else
            {
                ReplaceRows(_linkAViewRows, TakeWindow(parsedA, windowStart, windowEnd, _rowLimit));
                ReplaceRows(_linkBViewRows, TakeWindow(parsedB, windowStart, windowEnd, _rowLimit));
            }

            TimelineTape.WindowStart = windowStart;
            TimelineTape.WindowEnd = windowEnd;
            TimelineTape.SelectedTime = _selectedTime;
            TimelineTape.IsLaneAActive = IsLinkAActive(vm);
            TimelineTape.IsLaneBActive = IsLinkBActive(vm);
            TimelineTape.SetBuffers(_laneABuffer, _laneBBuffer);

            DrawRuler(windowStart, windowEnd);
            NavigatorStartLabel.Text = windowStart.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            NavigatorEndLabel.Text = windowEnd.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            UpdateReadout();
            SyncGridSelections();
        }

        private static List<RowWithTime> ParseRows(IEnumerable<LineMonitorRow> rows)
        {
            return rows
                .Select(r => new RowWithTime(r, ParseTime(r.Time)))
                .Where(r => r.Time.HasValue)
                .OrderBy(r => r.Time.Value)
                .ToList();
        }

        private static List<LineMonitorRow> GetSampleEvents(IEnumerable<RowWithTime> rows, DateTime start, DateTime end)
        {
            return rows
                .Where(r => r.Time.Value >= start && r.Time.Value < end)
                .Select(r => r.Row)
                .ToList();
        }

        private void AppendSample(List<float> buffer, ref float prev, float raw)
        {
            float basePulse = 0.08f;
            float value = Math.Max(raw, basePulse);
            float smooth = (prev * 0.3f) + (value * 0.7f);
            prev = smooth;

            buffer.Add(smooth);
            if (buffer.Count > MaxSamples)
            {
                buffer.RemoveAt(0);
            }
        }

        private static float ComputeLaneIntensity(IList<LineMonitorRow> events, bool isStandby)
        {
            if (events == null || events.Count == 0)
            {
                return 0;
            }

            int count = 0;
            bool isGi = false;
            bool isLinkCheck = true;

            foreach (LineMonitorRow ev in events)
            {
                string summary = ev.Summary ?? string.Empty;
                string detail = ev.Detail ?? string.Empty;

                if (summary.IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0
                    || detail.IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isGi = true;
                }

                bool fixedFrame = string.Equals(ev.FrameType, "Fixed", StringComparison.OrdinalIgnoreCase)
                    || ((ev.FrameType ?? string.Empty).IndexOf("Fixed", StringComparison.OrdinalIgnoreCase) >= 0);
                bool looksLikeLinkCheck = fixedFrame
                    && (detail.IndexOf("Length=6", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("Length-6", StringComparison.OrdinalIgnoreCase) >= 0
                        || summary.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!looksLikeLinkCheck)
                {
                    isLinkCheck = false;
                }

                if (!IsLowPriorityTraffic(ev))
                {
                    count++;
                }
            }

            if (isGi || count > 20)
            {
                return isStandby ? 0.92f : 1.0f;
            }

            if (isLinkCheck)
            {
                return 0.12f + ((DateTime.UtcNow.Millisecond % 400 < 200) ? 0.05f : 0f);
            }

            float raw = (float)(1.0 - Math.Exp(-count * 0.5));
            raw *= isStandby ? 0.4f : 1.2f;
            return Math.Max(0, Math.Min(1.0f, raw));
        }

        private static bool IsLowPriorityTraffic(LineMonitorRow row)
        {
            string summary = row.Summary ?? string.Empty;
            string detail = row.Detail ?? string.Empty;
            return summary.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("link-layer test function", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ReplaceRows(ObservableCollection<LineMonitorRow> target, IEnumerable<LineMonitorRow> rows)
        {
            target.Clear();
            foreach (LineMonitorRow row in rows)
            {
                target.Add(row);
            }
        }

        private static IEnumerable<LineMonitorRow> TakeWindow(IEnumerable<RowWithTime> rows, DateTime start, DateTime end, int rowLimit)
        {
            return rows
                .Where(r => r.Time.Value >= start && r.Time.Value <= end)
                .OrderByDescending(r => r.Time.Value)
                .Take(rowLimit)
                .OrderBy(r => r.Time.Value)
                .Select(r => r.Row)
                .ToList();
        }

        private static IEnumerable<LineMonitorRow> TakeInspectRows(IEnumerable<RowWithTime> rows, DateTime bucketStart, DateTime anchorTime, int rowLimit)
        {
            DateTime bucketEnd = bucketStart.AddSeconds(BucketSizeSeconds);

            List<RowWithTime> bucketRows = rows
                .Where(r => r.Time.Value >= bucketStart && r.Time.Value < bucketEnd)
                .OrderBy(r => r.Time.Value)
                .ToList();

            if (bucketRows.Count == 0)
            {
                DateTime expandedStart = bucketStart.AddSeconds(-BucketSizeSeconds);
                DateTime expandedEnd = bucketEnd.AddSeconds(BucketSizeSeconds);
                bucketRows = rows
                    .Where(r => r.Time.Value >= expandedStart && r.Time.Value < expandedEnd)
                    .OrderBy(r => Math.Abs((r.Time.Value - anchorTime).TotalMilliseconds))
                    .Take(rowLimit)
                    .OrderBy(r => r.Time.Value)
                    .ToList();
            }

            return bucketRows
                .Take(rowLimit)
                .Select(r => r.Row)
                .ToList();
        }

        private void DrawRuler(DateTime start, DateTime end)
        {
            double width = TimelineRulerCanvas.ActualWidth;
            double height = TimelineRulerCanvas.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            TimelineRulerCanvas.Children.Clear();

            TimelineRulerCanvas.Children.Add(new Line
            {
                X1 = TimelineLeftMargin,
                X2 = Math.Max(TimelineLeftMargin, width - TimelineRightMargin),
                Y1 = height - 4,
                Y2 = height - 4,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 76, 98, 122)),
                StrokeThickness = 1
            });

            int tickStep = (int)(5 / BucketSizeSeconds);
            for (int i = 0; i <= TimelineBucketCount; i += tickStep)
            {
                DateTime tickTime = start.AddSeconds(i * BucketSizeSeconds);
                double x = TimeToX(tickTime, start, end, width);
                bool major = ((i / tickStep) % 2) == 0;

                TimelineRulerCanvas.Children.Add(new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = major ? 2 : 8,
                    Y2 = height - 4,
                    Stroke = new SolidColorBrush(Color.FromArgb(major ? (byte)150 : (byte)90, 77, 98, 122)),
                    StrokeThickness = 1
                });

                TextBlock label = new TextBlock
                {
                    Text = tickTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    Foreground = (Brush)FindResource("MutedTextBrush"),
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas")
                };
                Canvas.SetLeft(label, Math.Max(TimelineLeftMargin, x - 28));
                Canvas.SetTop(label, 0);
                TimelineRulerCanvas.Children.Add(label);
            }
        }

        private static double TimeToX(DateTime time, DateTime start, DateTime end, double width)
        {
            double seconds = Math.Max(1, (end - start).TotalSeconds);
            double ratio = (time - start).TotalSeconds / seconds;
            ratio = Math.Max(0, Math.Min(1, ratio));
            return TimelineLeftMargin + (Math.Max(1, width - TimelineLeftMargin - TimelineRightMargin) * ratio);
        }

        private static DateTime? MaxTimestamp(List<RowWithTime> aRows, List<RowWithTime> bRows)
        {
            List<DateTime> values = aRows
                .Concat(bRows)
                .Where(t => t.Time.HasValue)
                .Select(t => t.Time.Value)
                .ToList();

            if (values.Count == 0)
            {
                return null;
            }

            return values.Max();
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
                SetFollowRight(false);
            }
        }

        private void LiveButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = false;
            PauseViewButton.IsChecked = false;
            ClearInspectState();
            SetFollowRight(true);
            SampleTape();
            RefreshViewport(true);
        }

        private void ClearViewButton_Click(object sender, RoutedEventArgs e)
        {
            _linkAViewRows.Clear();
            _linkBViewRows.Clear();
            _laneABuffer.Clear();
            _laneBBuffer.Clear();
            _laneAPrev = 0.08f;
            _laneBPrev = 0.08f;
            _lastSampleTime = null;
            _selectedTime = null;
            _windowStartTime = null;
            _windowEndTime = null;
            ClearInspectState();
            TimelineRulerCanvas.Children.Clear();
            TimelineTape.SetBuffers(null, null);
            TimelineTape.SelectedTime = null;
            NavigatorStartLabel.Text = "--:--:--.---";
            NavigatorEndLabel.Text = "--:--:--.---";
            SelectedTimeTextBlock.Text = "--:--:--.---";
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
                writer.WriteLine(string.Join(",", Csv(link), Csv(row.Time), Csv(row.Direction), Csv(row.FrameType), Csv(row.Summary), Csv(row.Detail)));
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
            SetFollowRight(AutoScrollButton.IsChecked == true);
            if (_followRight)
            {
                _isPaused = false;
                PauseViewButton.IsChecked = false;
                ClearInspectState();
            }

            RefreshViewport(true);
        }

        private void RowLimitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SyncRowLimit();
            RefreshViewport(true);
        }

        private void NucLinkTraceWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshViewport(false);
        }

        private void TimelineTape_SelectedTimeChanged(object sender, DateTime selectedTime)
        {
            if (!_windowStartTime.HasValue || !_windowEndTime.HasValue)
            {
                return;
            }

            MainViewModel vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            SetFollowRight(false);
            _isPaused = true;
            PauseViewButton.IsChecked = true;
            _isInspectFrozen = true;
            _inspectWindowStartTime = _windowStartTime;
            _inspectWindowEndTime = _windowEndTime;
            DateTime frozenSelected = ClampSelectedTime(selectedTime, _windowStartTime.Value, _windowEndTime.Value);
            DateTime bucketStart = GetBucketStart(_windowStartTime.Value, frozenSelected);

            List<RowWithTime> parsedA = ParseRows(vm.NucTraceLinkA);
            List<RowWithTime> parsedB = ParseRows(vm.NucTraceLinkB);
            DateTime anchorTime = ResolveAnchorTime(parsedA, parsedB, bucketStart, frozenSelected);

            _inspectBucketStartTime = bucketStart;
            _selectedAnchorTime = anchorTime;
            _selectedTime = anchorTime;
            RefreshViewport(false);
        }

        private void SyncGridSelections()
        {
            if (!_selectedTime.HasValue || _suppressGridSelectionSync)
            {
                return;
            }

            _suppressGridSelectionSync = true;
            try
            {
                SelectNearestRow(LinkAGrid, _linkAViewRows);
                SelectNearestRow(LinkBGrid, _linkBViewRows);
            }
            finally
            {
                _suppressGridSelectionSync = false;
            }
        }

        private void SelectNearestRow(DataGrid grid, ObservableCollection<LineMonitorRow> visibleRows)
        {
            if (!_selectedTime.HasValue || visibleRows.Count == 0)
            {
                grid.SelectedItem = null;
                return;
            }

            RowWithTime nearest = visibleRows
                .Select(r => new RowWithTime(r, ParseTime(r.Time)))
                .Where(r => r.Time.HasValue)
                .OrderBy(r => Math.Abs((r.Time.Value - _selectedTime.Value).TotalMilliseconds))
                .FirstOrDefault();

            if (nearest == null)
            {
                grid.SelectedItem = null;
                return;
            }

            if (!ReferenceEquals(grid.SelectedItem, nearest.Row))
            {
                grid.SelectedItem = nearest.Row;
                grid.ScrollIntoView(nearest.Row);
            }
        }

        private void UpdateReadout()
        {
            SelectedTimeTextBlock.Text = _selectedTime.HasValue
                ? _selectedTime.Value.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                : "--:--:--.---";
        }

        private void SetFollowRight(bool enabled)
        {
            _followRight = enabled;
            AutoScrollButton.IsChecked = enabled;
        }

        private static DateTime ClampSelectedTime(DateTime selected, DateTime windowStart, DateTime windowEnd)
        {
            if (selected < windowStart)
            {
                return windowStart;
            }

            if (selected > windowEnd)
            {
                return windowEnd;
            }

            return selected;
        }

        private static DateTime GetBucketStart(DateTime windowStart, DateTime selectedTime)
        {
            double bucketOffset = Math.Floor((selectedTime - windowStart).TotalSeconds / BucketSizeSeconds);
            if (bucketOffset < 0)
            {
                bucketOffset = 0;
            }

            return windowStart.AddSeconds(bucketOffset * BucketSizeSeconds);
        }

        private void ClearInspectState()
        {
            _isInspectFrozen = false;
            _inspectWindowStartTime = null;
            _inspectWindowEndTime = null;
            _inspectBucketStartTime = null;
            _selectedAnchorTime = null;
        }

        private static DateTime ResolveAnchorTime(IEnumerable<RowWithTime> parsedA, IEnumerable<RowWithTime> parsedB, DateTime bucketStart, DateTime selectedTime)
        {
            DateTime bucketEnd = bucketStart.AddSeconds(BucketSizeSeconds);
            DateTime bucketCenter = bucketStart.AddSeconds(BucketSizeSeconds / 2.0);

            List<RowWithTime> bucketEvents = parsedA
                .Concat(parsedB)
                .Where(r => r.Time.Value >= bucketStart && r.Time.Value < bucketEnd)
                .OrderBy(r => Math.Abs((r.Time.Value - bucketCenter).TotalMilliseconds))
                .ThenBy(r => r.Time.Value)
                .ToList();

            if (bucketEvents.Count > 0)
            {
                return bucketEvents[0].Time.Value;
            }

            List<RowWithTime> nearbyEvents = parsedA
                .Concat(parsedB)
                .Where(r => r.Time.Value >= bucketStart.AddSeconds(-BucketSizeSeconds) && r.Time.Value < bucketEnd.AddSeconds(BucketSizeSeconds))
                .OrderBy(r => Math.Abs((r.Time.Value - bucketCenter).TotalMilliseconds))
                .ThenBy(r => r.Time.Value)
                .ToList();

            if (nearbyEvents.Count > 0)
            {
                return nearbyEvents[0].Time.Value;
            }

            return selectedTime;
        }

        private static bool IsLinkAActive(MainViewModel vm)
        {
            return (vm.RedundancyActiveLinkText ?? string.Empty).IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLinkBActive(MainViewModel vm)
        {
            return (vm.RedundancyActiveLinkText ?? string.Empty).IndexOf("Backup", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class RowWithTime
        {
            public RowWithTime(LineMonitorRow row, DateTime? time)
            {
                Row = row;
                Time = time;
            }

            public LineMonitorRow Row { get; private set; }
            public DateTime? Time { get; private set; }
        }
    }
}
