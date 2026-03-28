using IEC101MasterTester.ViewModels;
using IEC101MasterTester.Models;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace IEC101MasterTester.Views
{
    public partial class AvailabilityDashboardWindow : Window
    {
        private const double BaseDashboardWidth = 1248d;
        private const double BaseDashboardHeight = 728d;
        private const double MinManualZoom = 0.75d;
        private const double MaxManualZoom = 1.35d;
        private const double ZoomStep = 0.10d;

        private double _manualZoom = 1d;
        private readonly DispatcherTimer _liveGaugeTimer;
        private readonly Dictionary<string, string> _metricSnapshots = new Dictionary<string, string>();
        private readonly Dictionary<string, DateTime> _metricIndicatorUntilUtc = new Dictionary<string, DateTime>();
        private static readonly TimeSpan MetricIndicatorLifetime = TimeSpan.FromSeconds(5);

        public AvailabilityDashboardWindow()
        {
            InitializeComponent();
            Loaded += AvailabilityDashboardWindow_Loaded;
            _liveGaugeTimer = new DispatcherTimer
            {
                Interval = System.TimeSpan.FromSeconds(1)
            };
            _liveGaugeTimer.Tick += LiveGaugeTimer_Tick;
        }

        public bool AllowClose { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            _liveGaugeTimer.Stop();

            if (!AllowClose)
            {
                e.Cancel = true;
                Hide();
                AllowClose = false;
                return;
            }

            base.OnClosing(e);
        }

        private void AvailabilityDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDashboardScale();
            ApplyLiveGaugeVisuals();
            _liveGaugeTimer.Start();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateDashboardScale();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                _manualZoom = System.Math.Min(MaxManualZoom, _manualZoom + ZoomStep);
                UpdateDashboardScale();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                _manualZoom = System.Math.Max(MinManualZoom, _manualZoom - ZoomStep);
                UpdateDashboardScale();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                _manualZoom = 1d;
                UpdateDashboardScale();
                e.Handled = true;
            }
        }

        private void UpdateDashboardScale()
        {
            // Layout scaling removed.
        }

        private void LiveGaugeTimer_Tick(object sender, System.EventArgs e)
        {
            ApplyLiveGaugeVisuals();
        }

        private void ApplyGaugeVisuals(double reliabilityValue, double availabilityValue)
        {
            if (ReliabilityGauge == null || AvailabilityGauge == null || ReliabilityScoreTextBlock == null)
            {
                return;
            }

            ReliabilityGauge.Value = reliabilityValue;
            ReliabilityGauge.DisplayText = reliabilityValue.ToString("F0");
            ReliabilityGauge.StateText = GetReliabilityStateText(reliabilityValue);
            ReliabilityGauge.GaugeBrush = GetGaugeBrush(reliabilityValue);
            ReliabilityGauge.TrackBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            ReliabilityGauge.ValueTextBrush = new SolidColorBrush(Color.FromRgb(230, 238, 248));

            AvailabilityGauge.Value = availabilityValue;
            AvailabilityGauge.DisplayText = availabilityValue.ToString("F1") + "%";
            AvailabilityGauge.StateText = GetAvailabilityStateText(availabilityValue);
            AvailabilityGauge.GaugeBrush = GetGaugeBrush(availabilityValue);
            AvailabilityGauge.TrackBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            AvailabilityGauge.ValueTextBrush = new SolidColorBrush(Color.FromRgb(230, 238, 248));

            ReliabilityScoreTextBlock.Text = reliabilityValue.ToString("F0") + " / 100";
            ReliabilityScoreTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(230, 238, 248));
            if (ReliabilityStateTextBlock != null)
            {
                ReliabilityStateTextBlock.Foreground = GetGaugeBrush(reliabilityValue);
            }
        }

        private void ApplyLiveGaugeVisuals()
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null)
            {
                return;
            }

            viewModel.RefreshAvailabilityDashboardSnapshot();
            ApplyGaugeVisuals(viewModel.ReliabilityScoreValue, viewModel.AvailabilityPercentValue);
            ReliabilityGauge.StateText = viewModel.ReliabilityStateText;
            ReliabilityGauge.DisplayText = viewModel.ReliabilityScoreValue.ToString("F0");
            AvailabilityGauge.StateText = viewModel.AvailabilityStateText;
            AvailabilityGauge.DisplayText = viewModel.AvailabilityPercentText;
            ReliabilityScoreTextBlock.Text = viewModel.ReliabilityScoreText;
            UpdateMetricIndicators(viewModel);
            UpdateTimelineChart(viewModel);
        }

        private void UpdateMetricIndicators(MainViewModel viewModel)
        {
            DateTime nowUtc = System.DateTime.UtcNow;
            UpdateMetricIndicator("Reconnect", viewModel.AvailabilityReconnectCountText, ReconnectIndicator, nowUtc);
            UpdateMetricIndicator("SlaveRecovery", viewModel.AvailabilitySlaveRecoveryCountText, SlaveRecoveryIndicator, nowUtc);
            UpdateMetricIndicator("RtuRestart", viewModel.AvailabilityRtuRestartCountText, RtuRestartIndicator, nowUtc);
            UpdateMetricIndicator("TransportDowntime", viewModel.AvailabilityDowntimeText, TransportDowntimeIndicator, nowUtc);
            UpdateMetricIndicator("LongestTransport", viewModel.AvailabilityLongestDowntimeText, LongestTransportIndicator, nowUtc);
            UpdateMetricIndicator("SlaveUnavailable", viewModel.AvailabilitySlaveDowntimeText, SlaveUnavailableIndicator, nowUtc);
            UpdateMetricIndicator("LongestSlave", viewModel.AvailabilitySlaveLongestDowntimeText, LongestSlaveIndicator, nowUtc);
            UpdateMetricIndicator("Throughput", viewModel.AvailabilityEventThroughputText, ThroughputIndicator, nowUtc);
            UpdateMetricIndicator("ProtocolErrors", viewModel.AvailabilityProtocolErrorCountText, ProtocolErrorsIndicator, nowUtc);
            UpdateMetricIndicator("Acd", viewModel.AvailabilityAcdAssertCountText, AcdIndicator, nowUtc);
        }

        private void UpdateMetricIndicator(string key, string value, Shape indicator, DateTime nowUtc)
        {
            if (indicator == null)
            {
                return;
            }

            string lastValue;
            _metricSnapshots.TryGetValue(key, out lastValue);
            if (lastValue == null)
            {
                _metricSnapshots[key] = value ?? string.Empty;
                indicator.Visibility = Visibility.Collapsed;
                return;
            }

            if (!string.Equals(lastValue, value ?? string.Empty, System.StringComparison.Ordinal))
            {
                _metricSnapshots[key] = value ?? string.Empty;
                _metricIndicatorUntilUtc[key] = nowUtc.Add(MetricIndicatorLifetime);
            }

            DateTime untilUtc;
            if (_metricIndicatorUntilUtc.TryGetValue(key, out untilUtc) && untilUtc > nowUtc)
            {
                indicator.Visibility = Visibility.Visible;
            }
            else
            {
                indicator.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateTimelineChart(MainViewModel viewModel)
        {
            if (TimelinePolyline == null || TimelineMarkerLayer == null)
            {
                return;
            }

            List<AvailabilityTimelineRow> rows = viewModel.AvailabilityTimeline.ToList();
            DateTime nowLocal = DateTime.Now;
            DateTime windowStart = nowLocal.AddMinutes(-40);
            int bucketCount = 11;
            double bucketMinutes = 40d / (bucketCount - 1);
            double[] bucketValues = new double[bucketCount];

            foreach (AvailabilityTimelineRow row in rows)
            {
                DateTime time;
                if (!TryParseTimelineTime(row.Time, out time))
                {
                    continue;
                }

                if (time < windowStart || time > nowLocal)
                {
                    continue;
                }

                double minutesFromStart = (time - windowStart).TotalMinutes;
                int bucketIndex = Math.Max(0, Math.Min(bucketCount - 1, (int)Math.Round(minutesFromStart / bucketMinutes)));
                bucketValues[bucketIndex] += GetTimelineWeight(row);
            }

            double maxValue = Math.Max(1d, bucketValues.Max());
            PointCollection points = new PointCollection();
            double left = 20d;
            double top = 12d;
            double width = 1100d;
            double height = 128d;

            for (int index = 0; index < bucketCount; index++)
            {
                double x = left + ((width / (bucketCount - 1)) * index);
                double normalized = bucketValues[index] / maxValue;
                double y = (top + height) - (normalized * (height - 8d));
                points.Add(new Point(x, y));
            }

            TimelinePolyline.Points = points;
            TimelineMarkerLayer.Children.Clear();

            foreach (AvailabilityTimelineRow row in rows.Take(80))
            {
                DateTime time;
                if (!TryParseTimelineTime(row.Time, out time))
                {
                    continue;
                }

                if (time < windowStart || time > nowLocal)
                {
                    continue;
                }

                Brush fill = GetTimelineMarkerBrush(row);
                if (fill == null)
                {
                    continue;
                }

                double minutesFromStart = (time - windowStart).TotalMinutes;
                double x = left + (minutesFromStart / 40d) * width;
                double y = GetTimelineMarkerY(row);

                Ellipse marker = new Ellipse
                {
                    Width = 9,
                    Height = 9,
                    Fill = fill,
                    Stroke = Brushes.Transparent,
                    ToolTip = string.Format(
                        "{0}\nCategory: {1}\nEvent: {2}\nDetail: {3}",
                        row.Time,
                        row.Category,
                        row.Event,
                        row.Detail)
                };
                Canvas.SetLeft(marker, x - 4.5d);
                Canvas.SetTop(marker, y);
                TimelineMarkerLayer.Children.Add(marker);
            }

            TimelineLabel1.Text = windowStart.ToString("HH:mm");
            TimelineLabel2.Text = windowStart.AddMinutes(10).ToString("HH:mm");
            TimelineLabel3.Text = windowStart.AddMinutes(20).ToString("HH:mm");
            TimelineLabel4.Text = windowStart.AddMinutes(30).ToString("HH:mm");
            TimelineLabel5.Text = nowLocal.ToString("HH:mm");
        }

        private static bool TryParseTimelineTime(string value, out DateTime parsed)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed)
                || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsed);
        }

        private static double GetTimelineWeight(AvailabilityTimelineRow row)
        {
            string category = row != null ? (row.Category ?? string.Empty) : string.Empty;
            string availabilityEvent = row != null ? (row.Event ?? string.Empty) : string.Empty;

            if (category.IndexOf("Finding", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 3.5d;
            }

            if (availabilityEvent.IndexOf("Disconnected", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Silent", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("restart", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 3d;
            }

            if (availabilityEvent.IndexOf("Recovered", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Switchover", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2d;
            }

            return 1d;
        }

        private static Brush GetTimelineMarkerBrush(AvailabilityTimelineRow row)
        {
            string category = row != null ? (row.Category ?? string.Empty) : string.Empty;
            string availabilityEvent = row != null ? (row.Event ?? string.Empty) : string.Empty;

            if (category.IndexOf("Finding", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Disconnected", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Silent", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("restart", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }

            if (availabilityEvent.IndexOf("Switchover", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Recovered", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }

            if (category.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0
                || category.IndexOf("Slave", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }

            return null;
        }

        private static double GetTimelineMarkerY(AvailabilityTimelineRow row)
        {
            string category = row != null ? (row.Category ?? string.Empty) : string.Empty;
            string availabilityEvent = row != null ? (row.Event ?? string.Empty) : string.Empty;

            if (category.IndexOf("Finding", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Disconnected", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("restart", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 20d;
            }

            if (availabilityEvent.IndexOf("Switchover", StringComparison.OrdinalIgnoreCase) >= 0
                || availabilityEvent.IndexOf("Recovered", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 58d;
            }

            return 94d;
        }

        private static Brush GetGaugeBrush(double value)
        {
            if (value >= 90d)
            {
                return new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }

            if (value >= 75d)
            {
                return new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }

            return new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private static string GetReliabilityStateText(double value)
        {
            if (value >= 90d)
            {
                return "Reliable";
            }

            if (value >= 75d)
            {
                return "Degraded";
            }

            return "Critical";
        }

        private static string GetAvailabilityStateText(double value)
        {
            if (value >= 95d)
            {
                return "Healthy";
            }

            if (value >= 80d)
            {
                return "Warning";
            }

            return "Unstable";
        }
    }
}
