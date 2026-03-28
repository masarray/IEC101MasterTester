using System.Windows;
using IEC101MasterTester.Models;
using IEC101MasterTester.ViewModels;

namespace IEC101MasterTester.Views
{
    public partial class NucLinkSetupWindow : Window
    {
        private readonly NucLinkSetupViewModel _viewModel;
        private readonly ConnectionSettings _baseSettings;

        public NucLinkSetupWindow(ConnectionSettings baseSettings, NucRedundancySettings currentSettings)
        {
            InitializeComponent();
            _baseSettings = baseSettings == null ? ConnectionSettings.CreateDefault() : baseSettings.Clone();
            _viewModel = new NucLinkSetupViewModel(currentSettings);
            DataContext = _viewModel;
        }

        public NucRedundancySettings ResultSettings { get; private set; }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            NucRedundancySettings settings;
            if (!_viewModel.TryBuildSettings(_baseSettings, out settings))
            {
                return;
            }

            ResultSettings = settings;
            DialogResult = true;
            Close();
        }
    }
}
