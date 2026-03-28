using System.Windows;
using IecSlaveSimulator.Models;
using IecSlaveSimulator.ViewModels;

namespace IecSlaveSimulator.Views
{
    public partial class NucSlaveLinkSetupWindow : Window
    {
        private readonly NucSlaveLinkSetupViewModel _viewModel;
        private readonly SlaveConnectionSettings _settings;

        public NucSlaveLinkSetupWindow(SlaveConnectionSettings settings)
        {
            InitializeComponent();
            _settings = settings ?? SlaveConnectionSettings.CreateDefault();
            _viewModel = new NucSlaveLinkSetupViewModel(_settings);
            DataContext = _viewModel;
        }

        public SlaveConnectionSettings Result { get; private set; }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.TryApplyTo(_settings))
            {
                Result = _settings;
                DialogResult = true;
                Close();
            }
        }
    }
}
