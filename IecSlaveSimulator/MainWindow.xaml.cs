using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IecSlaveSimulator.Models;
using IecSlaveSimulator.Services;
using IecSlaveSimulator.ViewModels;
using IecSlaveSimulator.Views;
using Microsoft.Win32;

namespace IecSlaveSimulator
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly SlaveConnectionSettingsStore _settingsStore;
        private readonly bool _skipInitialLoad;

        public MainWindow()
        {
            InitializeComponent();
            _settingsStore = new SlaveConnectionSettingsStore();
            _viewModel = new MainViewModel();
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
            Title = "IEC-101 RTU Database Editor";
            ApplyRuntimeWindowMode();
            Loaded += MainWindow_Loaded;
            PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        public MainWindow(MainViewModel viewModel, SlaveConnectionSettingsStore settingsStore, bool skipInitialLoad)
        {
            InitializeComponent();
            _settingsStore = settingsStore;
            _viewModel = viewModel;
            _skipInitialLoad = skipInitialLoad;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
            Title = "IEC-101 RTU Database Editor";
            ApplyRuntimeWindowMode();
            Loaded += MainWindow_Loaded;
            PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                WindowState = WindowState.Maximized;
                if (_skipInitialLoad)
                {
                    return;
                }

                SlaveConnectionSettings settings = await _settingsStore.LoadAsync();
                _viewModel.ApplyConnectionSettings(settings);

                string projectPath = NormalizeDialogPath(_viewModel.CurrentFilePath);
                if (File.Exists(projectPath))
                {
                    await _viewModel.LoadProjectAsync(projectPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "IecSlaveSimulator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            IEC101MasterTester.SharedUi.AboutWindow dialog = new IEC101MasterTester.SharedUi.AboutWindow { Owner = this };
            dialog.ShowDialog();
        }

        private async void ConnectionSetup_Click(object sender, RoutedEventArgs e)
        {
            SlaveConnectionSetupViewModel setupViewModel = new SlaveConnectionSetupViewModel(_viewModel.BuildConnectionSettings());
            ConnectionSetupWindow dialog = new ConnectionSetupWindow(setupViewModel) { Owner = this };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                _viewModel.ApplyConnectionSettings(dialog.Result);
                await SafeUiCallAsync(() => _settingsStore.SaveAsync(dialog.Result));
            }
        }

        private void OpenNucSlave_Click(object sender, RoutedEventArgs e)
        {
            NucSlaveWindow window = new NucSlaveWindow(_viewModel, _settingsStore, this) { Owner = this };
            Hide();
            window.Show();
            window.Activate();
        }

        private async void Load_Click(object sender, RoutedEventArgs e)
        {
            string initialPath = NormalizeDialogPath(_viewModel.CurrentFilePath);
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = Path.GetDirectoryName(initialPath),
                FileName = Path.GetFileName(initialPath)
            };

            if (dialog.ShowDialog(this) == true)
                await SafeUiCallAsync(() => _viewModel.LoadProjectAsync(dialog.FileName));
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string initialPath = NormalizeDialogPath(_viewModel.CurrentFilePath);
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = Path.GetDirectoryName(initialPath),
                FileName = Path.GetFileName(initialPath),
                AddExtension = true,
                DefaultExt = ".json",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) == true)
                await SafeUiCallAsync(() => _viewModel.SaveProjectAsync(dialog.FileName));
        }

        private void RuntimeSignalControl_Click(object sender, RoutedEventArgs e)
        {
            OpenRuntimeSignalControl();
        }

        private void RuntimeSignalsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenRuntimeSignalControl();
        }

        private void OpenRuntimeSignalControl()
        {
            if (!_viewModel.IsRuntime || _viewModel.SelectedRuntimeSignal == null)
            {
                MessageBox.Show(this, "Pilih satu signal runtime terlebih dahulu.", "IecSlaveSimulator", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SignalDefinition workingCopy = CloneRuntimeSignal(_viewModel.SelectedRuntimeSignal);
            RuntimeSignalControlWindow dialog = new RuntimeSignalControlWindow(workingCopy) { Owner = this };

            if (dialog.ShowDialog() == true)
                _viewModel.ApplyRuntimeSignalChanges(dialog.WorkingCopy);
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsRuntime))
                ApplyRuntimeWindowMode();
        }

        private void ApplyRuntimeWindowMode()
        {
            if (_viewModel.IsRuntime)
            {
                Width = 1120;
                Height = 760;
                MinWidth = 980;
                MinHeight = 680;
            }
            else
            {
                Width = 1680;
                Height = 980;
                MinWidth = 1200;
                MinHeight = 760;
            }
        }

        private string NormalizeDialogPath(string currentPath)
        {
            string path = string.IsNullOrWhiteSpace(currentPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IecSlaveSimulator", "slave-project.json")
                : currentPath;

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IecSlaveSimulator");
                path = Path.Combine(directory, Path.GetFileName(path));
            }

            Directory.CreateDirectory(directory);
            return path;
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

        private async Task SafeUiCallAsync(Func<Task> action)
        {
            try
            {
                WindowState = WindowState.Maximized;
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "IecSlaveSimulator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static SignalDefinition CloneRuntimeSignal(SignalDefinition source)
        {
            return new SignalDefinition
            {
                IsEnabled = source.IsEnabled,
                Ioa = source.Ioa,
                Label = source.Label,
                SignalType = source.SignalType,
                Casdu = source.Casdu,
                SignalClass = source.SignalClass,
                PublishMode = source.PublishMode,
                BackgroundEnabled = source.BackgroundEnabled,
                SpontaneousEnabled = source.SpontaneousEnabled,
                UseTimestamp = source.UseTimestamp,
                Quality = source.Quality,
                DefaultValue = source.DefaultValue,
                RuntimeValue = source.RuntimeValue,
                LiveCot = source.LiveCot,
                LinkedStatusIoa = source.LinkedStatusIoa,
                CommandSemantic = source.CommandSemantic,
                CommandBindingMode = source.CommandBindingMode,
                CommandOperateMode = source.CommandOperateMode,
                CommandDelayMs = source.CommandDelayMs,
                AnalogAnimation = source.AnalogAnimation,
                AnalogFrom = source.AnalogFrom,
                AnalogTo = source.AnalogTo,
                AnalogStep = source.AnalogStep,
                AnimationIntervalMs = source.AnimationIntervalMs,
                AnalogPingPong = source.AnalogPingPong,
                DiscreteAnimation = source.DiscreteAnimation,
                ToggleIntervalSeconds = source.ToggleIntervalSeconds,
                Notes = source.Notes
            };
        }
    }
}


