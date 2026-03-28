using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IecSlaveSimulator.Models;
using IecSlaveSimulator.Services;
using IecSlaveSimulator.ViewModels;

namespace IecSlaveSimulator.Views
{
    public partial class NucSlaveWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly SlaveConnectionSettingsStore _settingsStore;
        private readonly Window _returnWindow;
        private MainWindow _databaseEditorWindow;

        public NucSlaveWindow(MainViewModel viewModel, SlaveConnectionSettingsStore settingsStore, Window returnWindow)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _settingsStore = settingsStore;
            _returnWindow = returnWindow;
            DataContext = _viewModel;
            Closed += NucSlaveWindow_Closed;
            Loaded += NucSlaveWindow_Loaded;
        }

        private async void NucSlaveWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SlaveConnectionSettings settings = await _settingsStore.LoadAsync();
                if (settings != null)
                {
                    _viewModel.ApplyConnectionSettings(settings);
                }
            }
            catch
            {
            }
        }

        private async void ConnectionSetup_Click(object sender, RoutedEventArgs e)
        {
            NucSlaveLinkSetupWindow dialog = new NucSlaveLinkSetupWindow(_viewModel.BuildConnectionSettings()) { Owner = this };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                _viewModel.ApplyConnectionSettings(dialog.Result);
                await _settingsStore.SaveAsync(dialog.Result);
            }
        }

        private void RuntimeSignalControl_Click(object sender, RoutedEventArgs e)
        {
            OpenRuntimeSignalControl();
        }

        private void RuntimeSignalsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DataGrid grid = sender as DataGrid;
            if (grid != null)
            {
                DataGridRow clickedRow = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
                if (clickedRow != null && clickedRow.Item is SignalDefinition signal)
                {
                    grid.SelectedItem = signal;
                    _viewModel.SelectedRuntimeSignal = signal;
                }
            }

            OpenRuntimeSignalControl();
        }

        private void OpenRuntimeSignalControl()
        {
            if (!_viewModel.IsRuntime || _viewModel.SelectedRuntimeSignal == null)
            {
                MessageBox.Show(this, "Pilih satu signal runtime terlebih dahulu.", "IEC-101 NUC Slave", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SignalDefinition workingCopy = CloneRuntimeSignal(_viewModel.SelectedRuntimeSignal);
            RuntimeSignalControlWindow dialog = new RuntimeSignalControlWindow(workingCopy) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.ApplyRuntimeSignalChanges(dialog.WorkingCopy);
            }
        }

        private void OpenDatabaseEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_databaseEditorWindow != null)
            {
                _databaseEditorWindow.Show();
                _databaseEditorWindow.Activate();
                return;
            }

            _databaseEditorWindow = new MainWindow(_viewModel, _settingsStore, skipInitialLoad: true)
            {
                Owner = this
            };
            _databaseEditorWindow.Closed += DatabaseEditorWindow_Closed;
            _databaseEditorWindow.Show();
            _databaseEditorWindow.Activate();
        }

        private void DatabaseEditorWindow_Closed(object sender, System.EventArgs e)
        {
            if (_databaseEditorWindow != null)
            {
                _databaseEditorWindow.Closed -= DatabaseEditorWindow_Closed;
                _databaseEditorWindow = null;
            }
        }

        private static SignalDefinition CloneRuntimeSignal(SignalDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            return source.CloneForRuntime();
        }

        private static T FindAncestor<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T typed)
                {
                    return typed;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }

        private void NucSlaveWindow_Closed(object sender, System.EventArgs e)
        {
            _databaseEditorWindow?.Close();

            if (_returnWindow != null)
            {
                _returnWindow.Show();
                _returnWindow.Activate();
            }
        }
    }
}
