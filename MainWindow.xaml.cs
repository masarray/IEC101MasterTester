using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101;
using IEC101MasterTester.Services.Profiles;
using IEC101MasterTester.Services.Settings;
using IEC101MasterTester.ViewModels;
using IEC101MasterTester.Views;

namespace IEC101MasterTester
{
    public partial class MainWindow : Window
    {
        private readonly JsonSettingsStore _settingsStore;
        private readonly Iec101MasterService _masterService;
        private readonly MainViewModel _viewModel;
        private bool _isClosing;
        private LineMonitorWindow _lineMonitorWindow;
        private FindingsWindow _findingsWindow;
        private BufferedEventAuditWindow _bufferedEventAuditWindow;
        private NucRedundancyWindow _nucRedundancyWindow;
        private AvailabilityDashboardWindow _availabilityDashboardWindow;
        private GridLength _savedLeftPaneWidth = new GridLength(0.8, GridUnitType.Star);
        private GridLength _savedRightPaneWidth = new GridLength(1.12, GridUnitType.Star);
        private readonly DispatcherTimer _findingsAlertTimer;
        private bool _findingsAlertPulseOn;
        private readonly Brush _findingsAlertBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B1D12"));
        private readonly Brush _findingsAlertBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));

        public MainWindow()
        {
            InitializeComponent();

            _settingsStore = new JsonSettingsStore();
            _masterService = new Iec101MasterService();
            _viewModel = new MainViewModel(_masterService, _settingsStore);

            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            _findingsAlertTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(420)
            };
            _findingsAlertTimer.Tick += FindingsAlertTimer_Tick;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;
            await _viewModel.InitializeAsync();
            UpdateFindingsAlertVisual();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.HasUnreadFindings), StringComparison.Ordinal))
            {
                Dispatcher.Invoke(UpdateFindingsAlertVisual);
            }
        }

        private void FindingsAlertTimer_Tick(object sender, EventArgs e)
        {
            if (!_viewModel.HasUnreadFindings)
            {
                UpdateFindingsAlertVisual();
                return;
            }

            _findingsAlertPulseOn = !_findingsAlertPulseOn;
            FindingsAlertDot.Visibility = Visibility.Visible;
            FindingsAlertDot.Opacity = _findingsAlertPulseOn ? 1.0 : 0.25;
            FindingsButton.Background = _findingsAlertPulseOn ? _findingsAlertBackground : null;
            FindingsButton.BorderBrush = _findingsAlertBorder;
        }

        private void UpdateFindingsAlertVisual()
        {
            if (_viewModel.HasUnreadFindings)
            {
                if (!_findingsAlertTimer.IsEnabled)
                {
                    _findingsAlertPulseOn = false;
                    _findingsAlertTimer.Start();
                }

                return;
            }

            _findingsAlertTimer.Stop();
            _findingsAlertPulseOn = false;
            FindingsAlertDot.Visibility = Visibility.Collapsed;
            FindingsAlertDot.Opacity = 1.0;
            FindingsButton.ClearValue(Button.BackgroundProperty);
            FindingsButton.ClearValue(Button.BorderBrushProperty);
        }

        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            ApplyZoomStep(e.Delta > 0 ? 0.1 : -0.1);
            e.Handled = true;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            if (e.Key == Key.Add || e.Key == Key.OemPlus)
            {
                ApplyZoomStep(0.1);
                e.Handled = true;
            }
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
            {
                ApplyZoomStep(-0.1);
                e.Handled = true;
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                SetZoom(1.0);
                e.Handled = true;
            }
        }

        private void ApplyZoomStep(double delta)
        {
            SetZoom(UiScaleTransform.ScaleX + delta);
        }

        private void SetZoom(double zoom)
        {
            double clamped = Math.Max(0.7, Math.Min(1.6, zoom));
            UiScaleTransform.ScaleX = clamped;
            UiScaleTransform.ScaleY = clamped;
        }

        private void MainDataGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!(sender is DataGrid dataGrid) || dataGrid.Columns == null || dataGrid.Columns.Count == 0)
            {
                return;
            }

            foreach (DataGridColumn column in dataGrid.Columns)
            {
                if (column.Width.IsStar)
                {
                    double star = column.Width.Value;
                    column.Width = new DataGridLength(star, DataGridLengthUnitType.Star);
                }
            }
        }

        private void HideLeftPaneButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLeftPane();
        }

        private void ShowLeftPaneButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLeftPane();
        }

        private void HideRightPaneButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRightPane();
        }

        private void ShowRightPaneButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRightPane();
        }

        private void LeftRailToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            LeftPaneColumn.Width = new GridLength(0.8, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(6);
            LeftPaneHost.Visibility = Visibility.Visible;
        }

        private void LeftRailToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            LeftPaneColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            LeftPaneHost.Visibility = Visibility.Collapsed;
        }

        private void RightRailToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            RightPaneColumn.Width = new GridLength(1.12, GridUnitType.Star);
            RightPaneHost.Visibility = Visibility.Visible;
        }

        private void RightRailToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            RightPaneColumn.Width = new GridLength(0);
            RightPaneHost.Visibility = Visibility.Collapsed;
        }

        private void ToggleLeftPane()
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            if (LeftPaneHost.Visibility == Visibility.Visible && RightPaneHost.Visibility == Visibility.Visible)
            {
                HideLeftPane();
            }
            else
            {
                ShowLeftPane();
                ShowRightPane();
            }
        }

        private void ToggleRightPane()
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            if (LeftPaneHost.Visibility == Visibility.Visible && RightPaneHost.Visibility == Visibility.Visible)
            {
                HideRightPane();
            }
            else
            {
                ShowLeftPane();
                ShowRightPane();
            }
        }

        private void HideLeftPane()
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            if (LeftPaneHost.Visibility != Visibility.Visible)
            {
                return;
            }

            _savedLeftPaneWidth = LeftPaneColumn.Width.Value > 0 ? LeftPaneColumn.Width : _savedLeftPaneWidth;
            LeftPaneHost.Visibility = Visibility.Collapsed;
            LeftPaneColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            RightPaneColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        private void ShowLeftPane()
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            if (LeftPaneHost.Visibility == Visibility.Visible)
            {
                return;
            }

            LeftPaneHost.Visibility = Visibility.Visible;
            LeftPaneColumn.Width = _savedLeftPaneWidth.Value > 0 ? _savedLeftPaneWidth : new GridLength(0.8, GridUnitType.Star);
            RightPaneColumn.Width = RightPaneHost.Visibility == Visibility.Visible
                ? (_savedRightPaneWidth.Value > 0 ? _savedRightPaneWidth : new GridLength(1.12, GridUnitType.Star))
                : new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = RightPaneHost.Visibility == Visibility.Visible ? new GridLength(6) : new GridLength(0);
        }

        private void HideRightPane()
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            if (RightPaneHost.Visibility != Visibility.Visible)
            {
                return;
            }

            _savedRightPaneWidth = RightPaneColumn.Width.Value > 0 ? RightPaneColumn.Width : _savedRightPaneWidth;
            RightPaneHost.Visibility = Visibility.Collapsed;
            RightPaneColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            LeftPaneColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        private void ShowRightPane()
        {
            if (!ArePaneControlsReady())
            {
                return;
            }

            if (RightPaneHost.Visibility == Visibility.Visible)
            {
                return;
            }

            RightPaneHost.Visibility = Visibility.Visible;
            RightPaneColumn.Width = _savedRightPaneWidth.Value > 0 ? _savedRightPaneWidth : new GridLength(1.12, GridUnitType.Star);
            LeftPaneColumn.Width = LeftPaneHost.Visibility == Visibility.Visible
                ? (_savedLeftPaneWidth.Value > 0 ? _savedLeftPaneWidth : new GridLength(0.8, GridUnitType.Star))
                : new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = LeftPaneHost.Visibility == Visibility.Visible ? new GridLength(6) : new GridLength(0);
        }

        private bool ArePaneControlsReady()
        {
            return LeftPaneHost != null
                && RightPaneHost != null
                && LeftPaneColumn != null
                && RightPaneColumn != null
                && SplitterColumn != null;
        }

        private async void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            e.Cancel = true;
            IsEnabled = false;

            try
            {
                await _viewModel.ShutdownAsync();
            }
            finally
            {
                CloseChildWindow(_lineMonitorWindow);
                CloseChildWindow(_bufferedEventAuditWindow);
                CloseChildWindow(_findingsWindow);
                CloseChildWindow(_nucRedundancyWindow);
                CloseChildWindow(_availabilityDashboardWindow);

                Closing -= MainWindow_Closing;
                _ = Dispatcher.BeginInvoke(new Action(() => Application.Current.Shutdown()));
            }
        }

        private async void ConnectionSetup_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.CanEditSettings)
            {
                return;
            }

            ConnectionSettings settingsCopy = _viewModel.CurrentSettings.Clone();
            ConnectionSetupWindow window = new ConnectionSetupWindow(settingsCopy)
            {
                Owner = this
            };

            bool? result = window.ShowDialog();
            if (result == true && window.ResultSettings != null)
            {
                await _viewModel.UpdateSettingsAsync(window.ResultSettings);
            }
        }

        private void LineMonitor_Click(object sender, RoutedEventArgs e)
        {
            ShowLineMonitorWindow();
        }

        private void Findings_Click(object sender, RoutedEventArgs e)
        {
            ShowFindingsWindow();
        }

        private void BufferAudit_Click(object sender, RoutedEventArgs e)
        {
            ShowBufferedEventAuditWindow();
        }

        private async void NucRedundancy_Click(object sender, RoutedEventArgs e)
        {
            await ShowNucRedundancyWindowAsync();
        }

        private void Availability_Click(object sender, RoutedEventArgs e)
        {
            ShowAvailabilityDashboardWindow();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            SharedUi.AboutWindow window = new SharedUi.AboutWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void ValueViewer_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenSelectedSignalCommandWindow();
        }

        private void OpenSelectedSignalCommand_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedSignalCommandWindow();
        }

        private void OpenSelectedSignalCommandWindow()
        {
            if (!_viewModel.CanOpenSelectedValueCommand)
            {
                MessageBox.Show(this, "Pilih signal yang memiliki command IEC-101 terlebih dahulu.", "IEC101MasterTester", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string family = _viewModel.GetSelectedValueCommandFamily();
            if (string.Equals(family, "Setpoint", StringComparison.OrdinalIgnoreCase))
            {
                SetpointCommandWindowModel setpointModel = BuildSetpointCommandModel(false);
                SetpointCommandWindow setpointWindow = new SetpointCommandWindow(_viewModel, setpointModel)
                {
                    Owner = this
                };
                setpointWindow.ShowDialog();
                return;
            }

            SignalCommandWindowModel model = BuildSignalCommandModel(family);
            SignalCommandWindow window = new SignalCommandWindow(_viewModel, model)
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private SetpointCommandWindowModel BuildSetpointCommandModel(bool useNucSession)
        {
            ValueViewerRow row = _viewModel.SelectedValue;
            int commandIoa = _viewModel.GetSelectedValueSuggestedCommandIoa();
            int feedbackIoa = OfficialPointProfiles.TryGetRelatedFeedbackIoa(commandIoa) ?? (row != null ? row.IOA : 0);

            return new SetpointCommandWindowModel
            {
                SignalName = row != null ? OfficialPointProfiles.GetDisplayNameOrDefault(row.IOA, row.Name) : "Setpoint",
                SignalInfo = row == null ? string.Empty : string.Format("Feedback IOA {0} | {1}", row.IOA, row.Type),
                CommandIoa = commandIoa,
                FeedbackIoa = feedbackIoa,
                FeedbackName = OfficialPointProfiles.GetDisplayNameOrDefault(feedbackIoa, "POAQ"),
                CommandLifeMonitor = _viewModel.CommandLifeMonitor,
                UseNucSession = useNucSession
            };
        }

        private SignalCommandWindowModel BuildSignalCommandModel(string family)
        {
            ValueViewerRow row = _viewModel.SelectedValue;
            SignalCommandWindowModel model = new SignalCommandWindowModel
            {
                Family = family,
                SignalName = row != null ? OfficialPointProfiles.GetDisplayNameOrDefault(row.IOA, row.Name) : "Signal",
                SignalInfo = row == null ? string.Empty : string.Format("IOA {0} | {1}", row.IOA, row.Type),
                CommandIoa = _viewModel.GetSelectedValueSuggestedCommandIoa(),
                CommandLifeMonitor = _viewModel.CommandLifeMonitor
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

            return model;
        }

        private void ShowLineMonitorWindow()
        {
            if (_lineMonitorWindow == null)
            {
                _lineMonitorWindow = new LineMonitorWindow
                {
                    Owner = this,
                    DataContext = _viewModel
                };
            }

            if (!_lineMonitorWindow.IsVisible)
            {
                _lineMonitorWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _lineMonitorWindow.Show();
            }

            _lineMonitorWindow.Activate();
        }

        private void ShowFindingsWindow()
        {
            _viewModel.MarkFindingsViewed();
            UpdateFindingsAlertVisual();

            if (_findingsWindow == null)
            {
                _findingsWindow = new FindingsWindow
                {
                    Owner = this,
                    DataContext = _viewModel,
                    Left = Left + 90,
                    Top = Top + 90
                };
            }

            if (!_findingsWindow.IsVisible)
            {
                _findingsWindow.Show();
            }

            _findingsWindow.Activate();
        }

        private void ShowBufferedEventAuditWindow()
        {
            if (_bufferedEventAuditWindow == null)
            {
                _bufferedEventAuditWindow = new BufferedEventAuditWindow
                {
                    Owner = this,
                    DataContext = _viewModel,
                    Left = Left + 120,
                    Top = Top + 120
                };
            }

            if (!_bufferedEventAuditWindow.IsVisible)
            {
                _bufferedEventAuditWindow.Show();
            }

            _bufferedEventAuditWindow.Activate();
        }

        private async System.Threading.Tasks.Task ShowNucRedundancyWindowAsync()
        {
            if (_nucRedundancyWindow != null)
            {
                if (!_nucRedundancyWindow.IsVisible)
                {
                    _nucRedundancyWindow.Show();
                }

                _nucRedundancyWindow.Activate();
                return;
            }

            if (string.Equals(_viewModel.ConnectionStatus, "Connected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_viewModel.ConnectionStatus, "Connecting", StringComparison.OrdinalIgnoreCase))
            {
                await _viewModel.DisconnectForExclusiveWindowAsync();
            }

            Hide();

            _nucRedundancyWindow = new NucRedundancyWindow
            {
                DataContext = _viewModel
            };
            _nucRedundancyWindow.WindowClosedByUser += NucRedundancyWindow_WindowClosedByUser;
            _nucRedundancyWindow.Show();
            _nucRedundancyWindow.Activate();
        }

        private void NucRedundancyWindow_WindowClosedByUser(object sender, EventArgs e)
        {
            if (_nucRedundancyWindow != null)
            {
                _nucRedundancyWindow.WindowClosedByUser -= NucRedundancyWindow_WindowClosedByUser;
                _nucRedundancyWindow = null;
            }

            Show();
            Activate();
        }

        private void ShowAvailabilityDashboardWindow()
        {
            if (_availabilityDashboardWindow == null)
            {
                _availabilityDashboardWindow = new AvailabilityDashboardWindow
                {
                    Owner = this,
                    DataContext = _viewModel,
                    Left = Left + 150,
                    Top = Top + 150
                };
            }

            if (!_availabilityDashboardWindow.IsVisible)
            {
                _availabilityDashboardWindow.Show();
            }

            _availabilityDashboardWindow.Activate();
        }

        private static void CloseChildWindow(Window window)
        {
            if (window == null)
            {
                return;
            }

            if (window is LineMonitorWindow lineMonitorWindow)
            {
                lineMonitorWindow.AllowClose = true;
            }
            else if (window is BufferedEventAuditWindow bufferedEventAuditWindow)
            {
                bufferedEventAuditWindow.AllowClose = true;
            }
            else if (window is NucRedundancyWindow)
            {
            }
            else if (window is AvailabilityDashboardWindow availabilityDashboardWindow)
            {
                availabilityDashboardWindow.AllowClose = true;
            }
            else if (window is FindingsWindow findingsWindow)
            {
                findingsWindow.AllowClose = true;
            }

            window.Close();
        }
    }
}
