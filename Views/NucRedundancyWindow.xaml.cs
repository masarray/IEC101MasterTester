using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using IEC101MasterTester.Models;
using IEC101MasterTester.ViewModels;
using IEC101MasterTester.Services.Profiles;

namespace IEC101MasterTester.Views
{
    public partial class NucRedundancyWindow : Window
    {
        private bool _allowClose;
        private bool _closeInProgress;
        private NucSoeAuditWindow _nucSoeAuditWindow;
        private AvailabilityDashboardWindow _availabilityDashboardWindow;
        private NucLinkTraceWindow _nucLinkTraceWindow;
        private MainWindow _legacyMainWindow;
        private MainViewModel _viewModel;
        private DispatcherTimer _flowAnimationTimer;
        private Storyboard _mainRibbonFlowStoryboard;
        private Storyboard _backupRibbonFlowStoryboard;
        private bool _mainFlowRunning;
        private bool _backupFlowRunning;
        private DateTime? _mainLastTrafficSampleUtc;
        private DateTime? _backupLastTrafficSampleUtc;
        private int _mainLastTrafficTotal;
        private int _backupLastTrafficTotal;
        private double _mainTrafficIntensity;
        private double _backupTrafficIntensity;
        private double _mainFlowDurationSeconds = 1.2;
        private double _backupFlowDurationSeconds = 1.2;
        private bool _isLineMonitorCollapsed;
        private double _lineMonitorExpandedHeight = 280;
        private const double CollapsedLineMonitorHeight = 56;
        private const double MinZoomScale = 0.80;
        private const double MaxZoomScale = 1.35;
        private const double ZoomStep = 0.10;
        private double _zoomScale = 1.00;

        public NucRedundancyWindow()
        {
            InitializeComponent();
            Loaded += NucRedundancyWindow_Loaded;
            Unloaded += NucRedundancyWindow_Unloaded;
            DataContextChanged += NucRedundancyWindow_DataContextChanged;
        }

        public event EventHandler WindowClosedByUser;

        private void NucRedundancyWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BindViewModel(DataContext as MainViewModel);
            MainRibbonFlowCanvas.SizeChanged += RibbonFlowCanvas_SizeChanged;
            BackupRibbonFlowCanvas.SizeChanged += RibbonFlowCanvas_SizeChanged;

            _flowAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(240)
            };
            _flowAnimationTimer.Tick += FlowAnimationTimer_Tick;
            _flowAnimationTimer.Start();

            UpdateRibbonFlowAnimation();
            ApplyZoomScale();
            ApplyLineMonitorDockState();
        }

        private void NucRedundancyWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_flowAnimationTimer != null)
            {
                _flowAnimationTimer.Stop();
                _flowAnimationTimer.Tick -= FlowAnimationTimer_Tick;
                _flowAnimationTimer = null;
            }

            MainRibbonFlowCanvas.SizeChanged -= RibbonFlowCanvas_SizeChanged;
            BackupRibbonFlowCanvas.SizeChanged -= RibbonFlowCanvas_SizeChanged;

            StopRibbonFlowAnimation(ref _mainRibbonFlowStoryboard, MainRibbonFlowLine, MainRibbonFlowCanvas, MainLinkNameChip, MainStateChip, ref _mainFlowRunning);
            StopRibbonFlowAnimation(ref _backupRibbonFlowStoryboard, BackupRibbonFlowLine, BackupRibbonFlowCanvas, BackupLinkNameChip, BackupStateChip, ref _backupFlowRunning);
            BindViewModel(null);
        }

        private void NucRedundancyWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BindViewModel(e.NewValue as MainViewModel);
            UpdateRibbonFlowAnimation();
        }

        private void BindViewModel(MainViewModel vm)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = vm;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(MainViewModel.IsNucMainTxRecent):
                case nameof(MainViewModel.IsNucMainRxRecent):
                case nameof(MainViewModel.IsNucMainTimeoutActive):
                case nameof(MainViewModel.IsNucMainConnectedIndicator):
                case nameof(MainViewModel.IsNucBackupTxRecent):
                case nameof(MainViewModel.IsNucBackupRxRecent):
                case nameof(MainViewModel.IsNucBackupTimeoutActive):
                case nameof(MainViewModel.IsNucBackupConnectedIndicator):
                    Dispatcher.BeginInvoke(new Action(UpdateRibbonFlowAnimation));
                    break;
            }
        }

        private void FlowAnimationTimer_Tick(object sender, EventArgs e)
        {
            UpdateTrafficIntensity();
            UpdateRibbonFlowAnimation();
            UpdateRibbonPulse();
        }

        private void RibbonFlowCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_mainFlowRunning)
            {
                RestartRibbonFlowAnimation(MainRibbonFlowLine, MainRibbonFlowCanvas, MainLinkNameChip, MainStateChip, ref _mainRibbonFlowStoryboard);
            }

            if (_backupFlowRunning)
            {
                RestartRibbonFlowAnimation(BackupRibbonFlowLine, BackupRibbonFlowCanvas, BackupLinkNameChip, BackupStateChip, ref _backupRibbonFlowStoryboard);
            }

            UpdateRibbonPulse();
        }

        private void UpdateRibbonFlowAnimation()
        {
            MainViewModel vm = _viewModel ?? DataContext as MainViewModel;
            if (vm == null)
            {
                StopRibbonFlowAnimation(ref _mainRibbonFlowStoryboard, MainRibbonFlowLine, MainRibbonFlowCanvas, MainLinkNameChip, MainStateChip, ref _mainFlowRunning);
                StopRibbonFlowAnimation(ref _backupRibbonFlowStoryboard, BackupRibbonFlowLine, BackupRibbonFlowCanvas, BackupLinkNameChip, BackupStateChip, ref _backupFlowRunning);
                return;
            }

            bool mainActive = vm.IsNucMainConnectedIndicator
                && !vm.IsNucMainTimeoutActive
                && (vm.IsNucMainTxRecent || vm.IsNucMainRxRecent || vm.NucLinkAVisual.IsDataFlowActive);
            bool backupActive = vm.IsNucBackupConnectedIndicator
                && !vm.IsNucBackupTimeoutActive
                && (vm.IsNucBackupTxRecent || vm.IsNucBackupRxRecent || vm.NucLinkBVisual.IsDataFlowActive);

            if (mainActive)
            {
                double mainDuration = ComputeFlowDurationSeconds(_mainTrafficIntensity);
                bool mainDurationChanged = Math.Abs(mainDuration - _mainFlowDurationSeconds) > 0.2;
                _mainFlowDurationSeconds = mainDuration;

                StartRibbonFlowAnimation(ref _mainRibbonFlowStoryboard, MainRibbonFlowLine, MainRibbonFlowCanvas, MainLinkNameChip, MainStateChip, ref _mainFlowRunning);

                if (mainDurationChanged && _mainFlowRunning)
                {
                    RestartRibbonFlowAnimation(MainRibbonFlowLine, MainRibbonFlowCanvas, MainLinkNameChip, MainStateChip, ref _mainRibbonFlowStoryboard);
                }
            }
            else
            {
                StopRibbonFlowAnimation(ref _mainRibbonFlowStoryboard, MainRibbonFlowLine, MainRibbonFlowCanvas, MainLinkNameChip, MainStateChip, ref _mainFlowRunning);
            }

            if (backupActive)
            {
                double backupDuration = ComputeFlowDurationSeconds(_backupTrafficIntensity);
                bool backupDurationChanged = Math.Abs(backupDuration - _backupFlowDurationSeconds) > 0.2;
                _backupFlowDurationSeconds = backupDuration;

                StartRibbonFlowAnimation(ref _backupRibbonFlowStoryboard, BackupRibbonFlowLine, BackupRibbonFlowCanvas, BackupLinkNameChip, BackupStateChip, ref _backupFlowRunning);

                if (backupDurationChanged && _backupFlowRunning)
                {
                    RestartRibbonFlowAnimation(BackupRibbonFlowLine, BackupRibbonFlowCanvas, BackupLinkNameChip, BackupStateChip, ref _backupRibbonFlowStoryboard);
                }
            }
            else
            {
                StopRibbonFlowAnimation(ref _backupRibbonFlowStoryboard, BackupRibbonFlowLine, BackupRibbonFlowCanvas, BackupLinkNameChip, BackupStateChip, ref _backupFlowRunning);
            }
        }

        private void StartRibbonFlowAnimation(ref Storyboard storyboard, Rectangle line, FrameworkElement host, FrameworkElement startAnchor, FrameworkElement endAnchor, ref bool isRunning)
        {
            if (line == null || host == null)
            {
                return;
            }

            if (isRunning)
            {
                return;
            }

            double durationSeconds = line == MainRibbonFlowLine ? _mainFlowDurationSeconds : _backupFlowDurationSeconds;
            storyboard = BuildRibbonFlowStoryboard(line, host, startAnchor, endAnchor, durationSeconds);
            storyboard.Begin(line, true);
            isRunning = true;
        }

        private void RestartRibbonFlowAnimation(Rectangle line, FrameworkElement host, FrameworkElement startAnchor, FrameworkElement endAnchor, ref Storyboard storyboard)
        {
            if (line == null || host == null)
            {
                return;
            }

            if (storyboard != null)
            {
                storyboard.Stop(line);
            }

            double durationSeconds = line == MainRibbonFlowLine ? _mainFlowDurationSeconds : _backupFlowDurationSeconds;

            storyboard = BuildRibbonFlowStoryboard(line, host, startAnchor, endAnchor, durationSeconds);
            storyboard.Begin(line, true);
        }

        private void StopRibbonFlowAnimation(ref Storyboard storyboard, Rectangle line, FrameworkElement host, FrameworkElement startAnchor, FrameworkElement endAnchor, ref bool isRunning)
        {
            if (line == null || host == null)
            {
                return;
            }

            if (storyboard != null)
            {
                storyboard.Stop(line);
            }

            TranslateTransform transform = line.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform();
                line.RenderTransform = transform;
            }

            FlowRoute route = GetFlowRoute(line, host, startAnchor, endAnchor);
            transform.X = route.StartX - line.Width;
            isRunning = false;
        }

        private static Storyboard BuildRibbonFlowStoryboard(Rectangle line, FrameworkElement host, FrameworkElement startAnchor, FrameworkElement endAnchor, double durationSeconds)
        {
            TranslateTransform transform = line.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform();
                line.RenderTransform = transform;
            }

            FlowRoute route = GetFlowRoute(line, host, startAnchor, endAnchor);
            double from = route.StartX - line.Width;
            double to = route.EndX + line.Width + 2;

            transform.X = from;
            double clipLeft = Math.Max(0, route.StartX);
            double clipWidth = Math.Max(line.Width + 12, route.EndX - clipLeft);
            host.Clip = new RectangleGeometry(new Rect(clipLeft, 0, clipWidth, host.ActualHeight));

            Storyboard storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            DoubleAnimation animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromSeconds(Math.Max(0.42, durationSeconds * 0.58))),
                EasingFunction = null
            };

            Storyboard.SetTarget(animation, line);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(animation);
            return storyboard;
        }

        private static FlowRoute GetFlowRoute(Rectangle line, FrameworkElement host, FrameworkElement startAnchor, FrameworkElement endAnchor)
        {
            double width = line != null ? line.Width : 96;
            double hostWidth = host != null ? host.ActualWidth : 0;
            double fallbackStart = 8;
            double fallbackEnd = hostWidth > 0
                ? Math.Max(fallbackStart + width + 8, Math.Min(hostWidth - 12, hostWidth * 0.24))
                : (fallbackStart + width + 8);

            if (host == null || startAnchor == null || endAnchor == null || hostWidth <= 0)
            {
                return new FlowRoute { StartX = fallbackStart, EndX = fallbackEnd };
            }

            Point startPoint = startAnchor.TranslatePoint(new Point(0, 0), host);
            Point endPoint = endAnchor.TranslatePoint(new Point(0, 0), host);

            double startX = Math.Max(0, Math.Min(hostWidth, startPoint.X));
            double endX = Math.Max(startX + width + 8, Math.Min(hostWidth, endPoint.X + endAnchor.ActualWidth));
            return new FlowRoute { StartX = startX, EndX = endX };
        }

        private struct FlowRoute
        {
            public double StartX;
            public double EndX;
        }

        private void UpdateTrafficIntensity()
        {
            MainViewModel vm = _viewModel ?? DataContext as MainViewModel;
            if (vm == null)
            {
                return;
            }

            UpdateSingleTrafficIntensity(
                vm.NucLinkAVisual,
                ref _mainLastTrafficSampleUtc,
                ref _mainLastTrafficTotal,
                ref _mainTrafficIntensity);

            UpdateSingleTrafficIntensity(
                vm.NucLinkBVisual,
                ref _backupLastTrafficSampleUtc,
                ref _backupLastTrafficTotal,
                ref _backupTrafficIntensity);
        }

        private static void UpdateSingleTrafficIntensity(
            NucLinkVisualViewModel link,
            ref DateTime? lastSampleUtc,
            ref int lastTotal,
            ref double intensity)
        {
            if (link == null || link.Rows == null || link.Rows.Count < 3)
            {
                intensity = 0;
                return;
            }

            int tx = ParseCounter(link.Rows[2].Value);
            int rx = ParseCounter(link.Rows[1].Value);
            int total = Math.Max(0, tx + rx);
            DateTime now = DateTime.UtcNow;

            if (!lastSampleUtc.HasValue)
            {
                lastSampleUtc = now;
                lastTotal = total;
                intensity = 0;
                return;
            }

            double dt = (now - lastSampleUtc.Value).TotalSeconds;
            if (dt < 0.08)
            {
                return;
            }

            int delta = Math.Max(0, total - lastTotal);
            double rate = delta / Math.Max(dt, 0.08);
            double mapped = Math.Max(0, Math.Min(1, rate / 120.0));
            intensity = intensity * 0.65 + mapped * 0.35;

            lastSampleUtc = now;
            lastTotal = total;
        }

        private void UpdateRibbonPulse()
        {
            if (MainRibbonBaseLine != null)
            {
                MainRibbonBaseLine.Opacity = _mainFlowRunning ? (0.22 + _mainTrafficIntensity * 0.62) : 0.22;
            }

            if (BackupRibbonBaseLine != null)
            {
                BackupRibbonBaseLine.Opacity = _backupFlowRunning ? (0.22 + _backupTrafficIntensity * 0.62) : 0.22;
            }

            if (MainRibbonFlowLine != null)
            {
                MainRibbonFlowLine.Width = 80 + _mainTrafficIntensity * 140;
                MainRibbonFlowLine.Opacity = _mainFlowRunning ? (0.48 + _mainTrafficIntensity * 0.5) : 0.22;
            }

            if (BackupRibbonFlowLine != null)
            {
                BackupRibbonFlowLine.Width = 80 + _backupTrafficIntensity * 140;
                BackupRibbonFlowLine.Opacity = _backupFlowRunning ? (0.48 + _backupTrafficIntensity * 0.5) : 0.22;
            }
        }

        private static int ParseCounter(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            string digits = string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsDigit(value[i]))
                {
                    digits += value[i];
                }
            }

            int parsed;
            if (int.TryParse(digits, out parsed))
            {
                return parsed;
            }

            return 0;
        }

        private static double ComputeFlowDurationSeconds(double intensity)
        {
            // Low traffic = slower pulse, high traffic = faster pulse.
            return 1.25 - (Math.Max(0, Math.Min(1, intensity)) * 0.65);
        }

        private void ToggleLineMonitorPane_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLineMonitorCollapsed && LineMonitorDockRow != null)
            {
                double currentHeight = LineMonitorDockRow.ActualHeight;
                if (currentHeight > CollapsedLineMonitorHeight + 12)
                {
                    _lineMonitorExpandedHeight = currentHeight;
                }
            }

            _isLineMonitorCollapsed = !_isLineMonitorCollapsed;
            ApplyLineMonitorDockState();
        }

        private void OpenLinkTrace_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (_nucLinkTraceWindow == null)
            {
                _nucLinkTraceWindow = new NucLinkTraceWindow
                {
                    Owner = this,
                    DataContext = viewModel
                };
                _nucLinkTraceWindow.Closed += (o, args) => _nucLinkTraceWindow = null;
            }

            if (!_nucLinkTraceWindow.IsVisible)
            {
                _nucLinkTraceWindow.Show();
            }

            _nucLinkTraceWindow.Activate();
        }

        private void LineMonitorDockSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (_isLineMonitorCollapsed || LineMonitorDockRow == null)
            {
                return;
            }

            double currentHeight = LineMonitorDockRow.ActualHeight;
            if (currentHeight > CollapsedLineMonitorHeight + 12)
            {
                _lineMonitorExpandedHeight = currentHeight;
            }
        }

        private void ApplyLineMonitorDockState()
        {
            if (LineMonitorDockRow == null)
            {
                return;
            }

            if (_isLineMonitorCollapsed)
            {
                LineMonitorDockRow.Height = new GridLength(CollapsedLineMonitorHeight);

                if (LineMonitorContentGrid != null)
                {
                    LineMonitorContentGrid.Visibility = Visibility.Collapsed;
                }

                if (LineMonitorDockSplitter != null)
                {
                    LineMonitorDockSplitter.Visibility = Visibility.Collapsed;
                    LineMonitorDockSplitter.IsEnabled = false;
                }

                if (LineMonitorDockBorder != null)
                {
                    LineMonitorDockBorder.Padding = new Thickness(16, 10, 16, 10);
                }

                if (LineMonitorDockToggleButton != null)
                {
                    LineMonitorDockToggleButton.Content = null;
                    LineMonitorDockToggleButton.ToolTip = "Expand line monitor";
                }

                SetLineMonitorChevronAngle(180);

                return;
            }

            double expandedHeight = Math.Max(180, _lineMonitorExpandedHeight);
            LineMonitorDockRow.Height = new GridLength(expandedHeight);

            if (LineMonitorContentGrid != null)
            {
                LineMonitorContentGrid.Visibility = Visibility.Visible;
            }

            if (LineMonitorDockSplitter != null)
            {
                LineMonitorDockSplitter.Visibility = Visibility.Visible;
                LineMonitorDockSplitter.IsEnabled = true;
            }

            if (LineMonitorDockBorder != null)
            {
                LineMonitorDockBorder.Padding = new Thickness(16);
            }

            if (LineMonitorDockToggleButton != null)
            {
                LineMonitorDockToggleButton.Content = null;
                LineMonitorDockToggleButton.ToolTip = "Collapse line monitor";
            }

            SetLineMonitorChevronAngle(0);
        }

        private void SetLineMonitorChevronAngle(double angle)
        {
            if (LineMonitorDockToggleButton == null)
            {
                return;
            }

            LineMonitorDockToggleButton.ApplyTemplate();
            FrameworkElement templateRoot = LineMonitorDockToggleButton.Template.FindName("Chevron", LineMonitorDockToggleButton) as FrameworkElement;
            if (templateRoot == null)
            {
                return;
            }

            RotateTransform rotate = templateRoot.RenderTransform as RotateTransform;
            if (rotate != null)
            {
                rotate.Angle = angle;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                AdjustZoom(ZoomStep);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                AdjustZoom(-ZoomStep);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                ResetZoom();
                e.Handled = true;
            }
        }

        private void AdjustZoom(double delta)
        {
            _zoomScale = Math.Max(MinZoomScale, Math.Min(MaxZoomScale, _zoomScale + delta));
            ApplyZoomScale();
        }

        private void ResetZoom()
        {
            _zoomScale = 1.00;
            ApplyZoomScale();
        }

        private void ApplyZoomScale()
        {
            if (WindowZoomTransform == null)
            {
                return;
            }

            WindowZoomTransform.ScaleX = _zoomScale;
            WindowZoomTransform.ScaleY = _zoomScale;
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
            {
                base.OnClosing(e);
                return;
            }

            if (_closeInProgress)
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = true;
            _closeInProgress = true;

            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null)
            {
                try
                {
                    await viewModel.StopNucRedundancySessionAsync();
                }
                catch (Exception ex)
                {
                    _closeInProgress = false;
                    MessageBox.Show(this, ex.Message, "NUC Redundancy", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _allowClose = true;
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_nucSoeAuditWindow != null)
                {
                    _nucSoeAuditWindow.Close();
                    _nucSoeAuditWindow = null;
                }

                if (_availabilityDashboardWindow != null)
                {
                    _availabilityDashboardWindow.AllowClose = true;
                    _availabilityDashboardWindow.Close();
                    _availabilityDashboardWindow = null;
                }

                if (_legacyMainWindow != null)
                {
                    _legacyMainWindow.Close();
                    _legacyMainWindow = null;
                }

                Close();
            }));
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_nucSoeAuditWindow != null)
            {
                _nucSoeAuditWindow.Close();
                _nucSoeAuditWindow = null;
            }

            if (_availabilityDashboardWindow != null)
            {
                _availabilityDashboardWindow.AllowClose = true;
                _availabilityDashboardWindow.Close();
                _availabilityDashboardWindow = null;
            }

            if (_legacyMainWindow != null)
            {
                _legacyMainWindow.Close();
                _legacyMainWindow = null;
            }

            WindowClosedByUser?.Invoke(this, EventArgs.Empty);
            base.OnClosed(e);
        }

        private void StartSession_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null)
            {
                return;
            }

            string validationMessage;
            if (!viewModel.TryStartNucRedundancySession(out validationMessage) && !string.IsNullOrWhiteSpace(validationMessage))
            {
                MessageBox.Show(this, validationMessage, "NUC Redundancy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StopSession_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null)
            {
                return;
            }

            viewModel.StopNucRedundancySession();
            StopRibbonFlowAnimation(ref _mainRibbonFlowStoryboard, MainRibbonFlowLine, MainRibbonFlowCanvas, MainLinkNameChip, MainStateChip, ref _mainFlowRunning);
            StopRibbonFlowAnimation(ref _backupRibbonFlowStoryboard, BackupRibbonFlowLine, BackupRibbonFlowCanvas, BackupLinkNameChip, BackupStateChip, ref _backupFlowRunning);
        }

        private async void SendGi_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null)
            {
                return;
            }

            try
            {
                await viewModel.SendNucRedundancyGiAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "NUC Redundancy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ConfigureLinks_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null)
            {
                return;
            }

            NucLinkSetupWindow window = new NucLinkSetupWindow(viewModel.CurrentSettings, viewModel.BuildCurrentNucRedundancySettings())
            {
                Owner = this
            };

            bool? result = window.ShowDialog();
            if (result != true || window.ResultSettings == null)
            {
                return;
            }

            try
            {
                await viewModel.SaveNucRedundancySettingsAsync(window.ResultSettings);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "NUC Link Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void NucValueViewer_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            DataGrid grid = sender as DataGrid;
            if (viewModel == null)
            {
                return;
            }

            if (grid != null && grid.SelectedItem is ValueViewerRow selectedRow)
            {
                viewModel.SelectedNucValue = selectedRow;
            }

            if (!viewModel.CanOpenSelectedNucValueCommand)
            {
                MessageBox.Show(this, "Pilih signal Single Point, Double Point, atau Step Position terlebih dahulu.", "NUC Redundancy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string family = viewModel.GetSelectedNucValueCommandFamily();
            ValueViewerRow row = viewModel.SelectedNucValue;
            SignalCommandWindowModel model = new SignalCommandWindowModel
            {
                Family = family,
                SignalName = row != null ? OfficialPointProfiles.GetDisplayNameOrDefault(row.IOA, row.Name) : "Signal",
                SignalInfo = row == null ? string.Empty : string.Format("IOA {0} | {1}", row.IOA, row.Type),
                CommandIoa = viewModel.GetSelectedNucValueSuggestedCommandIoa(),
                CommandLifeMonitor = viewModel.CommandLifeMonitor,
                UseNucSession = true
            };

            switch (family)
            {
                case "Double":
                    model.PrimaryOperation = "OPEN";
                    model.SecondaryOperation = "CLOSE";
                    model.DirectPrimaryLabel = "OPEN";
                    model.DirectSecondaryLabel = "CLOSE";
                    model.SelectPrimaryLabel = "Select Open";
                    model.SelectSecondaryLabel = "Select Close";
                    model.ExecPrimaryLabel = "Exec Open";
                    model.ExecSecondaryLabel = "Exec Close";
                    break;
                case "Regulating":
                    model.PrimaryOperation = "LOWER";
                    model.SecondaryOperation = "RAISE";
                    model.DirectPrimaryLabel = "LOWER";
                    model.DirectSecondaryLabel = "RAISE";
                    model.SelectPrimaryLabel = "Select Lower";
                    model.SelectSecondaryLabel = "Select Raise";
                    model.ExecPrimaryLabel = "Exec Lower";
                    model.ExecSecondaryLabel = "Exec Raise";
                    break;
                default:
                    model.PrimaryOperation = "ON";
                    model.SecondaryOperation = "OFF";
                    model.DirectPrimaryLabel = "ON";
                    model.DirectSecondaryLabel = "OFF";
                    model.SelectPrimaryLabel = "Select ON";
                    model.SelectSecondaryLabel = "Select OFF";
                    model.ExecPrimaryLabel = "Exec ON";
                    model.ExecSecondaryLabel = "Exec OFF";
                    break;
            }

            SignalCommandWindow window = new SignalCommandWindow(viewModel, model)
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void OpenSoeAudit_Click(object sender, RoutedEventArgs e)
        {
            if (_nucSoeAuditWindow == null)
            {
                _nucSoeAuditWindow = new NucSoeAuditWindow
                {
                    Owner = this,
                    DataContext = DataContext
                };
                _nucSoeAuditWindow.Closed += (o, args) => _nucSoeAuditWindow = null;
            }

            if (!_nucSoeAuditWindow.IsVisible)
            {
                _nucSoeAuditWindow.Show();
            }

            _nucSoeAuditWindow.Activate();
        }

        private void OpenAvailabilityDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (_availabilityDashboardWindow == null)
            {
                _availabilityDashboardWindow = new AvailabilityDashboardWindow
                {
                    Owner = this,
                    DataContext = DataContext,
                    Left = Left + 150,
                    Top = Top + 150
                };
                _availabilityDashboardWindow.Closed += (o, args) => _availabilityDashboardWindow = null;
            }

            if (!_availabilityDashboardWindow.IsVisible)
            {
                _availabilityDashboardWindow.Show();
            }

            _availabilityDashboardWindow.Activate();
        }

        private void OpenMainWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_legacyMainWindow == null)
            {
                _legacyMainWindow = new MainWindow
                {
                    DataContext = DataContext,
                    Left = Left + 180,
                    Top = Top + 180
                };
                _legacyMainWindow.Closed += (o, args) => _legacyMainWindow = null;
            }

            if (!_legacyMainWindow.IsVisible)
            {
                _legacyMainWindow.Show();
            }

            _legacyMainWindow.Activate();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            SharedUi.AboutWindow window = new SharedUi.AboutWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void ClearBufferEventStatistic_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null)
            {
                return;
            }

            string validationMessage;
            if (!viewModel.TryClearNucRuntimeObservability(out validationMessage))
            {
                MessageBox.Show(this, validationMessage, "NUC Redundancy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _mainLastTrafficSampleUtc = null;
            _backupLastTrafficSampleUtc = null;
            _mainLastTrafficTotal = 0;
            _backupLastTrafficTotal = 0;
            _mainTrafficIntensity = 0;
            _backupTrafficIntensity = 0;
            _mainFlowDurationSeconds = 1.2;
            _backupFlowDurationSeconds = 1.2;

            StopRibbonFlowAnimation(ref _mainRibbonFlowStoryboard, MainRibbonFlowLine, MainRibbonFlowCanvas, MainLinkNameChip, MainStateChip, ref _mainFlowRunning);
            StopRibbonFlowAnimation(ref _backupRibbonFlowStoryboard, BackupRibbonFlowLine, BackupRibbonFlowCanvas, BackupLinkNameChip, BackupStateChip, ref _backupFlowRunning);
            UpdateRibbonPulse();
        }
    }
}

