using System.Windows;
using IEC101MasterTester.Models;
using IEC101MasterTester.ViewModels;

namespace IEC101MasterTester.Views
{
    public partial class ConnectionSetupWindow : Window
    {
        private readonly ConnectionSetupViewModel _viewModel;

        public ConnectionSetupWindow(ConnectionSettings settings)
        {
            InitializeComponent();
            _viewModel = new ConnectionSetupViewModel(settings);
            DataContext = _viewModel;
        }

        public ConnectionSettings ResultSettings { get; private set; }

        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshSerialPorts();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.TryBuildSettings(out ConnectionSettings settings))
            {
                return;
            }

            ResultSettings = settings;
            DialogResult = true;
            Close();
        }
    }
}
