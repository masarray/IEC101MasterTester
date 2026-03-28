using System.Windows;
using IecSlaveSimulator.Models;
using IecSlaveSimulator.ViewModels;

namespace IecSlaveSimulator.Views
{
    public partial class ConnectionSetupWindow : Window
    {
        public ConnectionSetupWindow(SlaveConnectionSetupViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public SlaveConnectionSettings Result { get; private set; }

        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            ((SlaveConnectionSetupViewModel)DataContext).RefreshSerialPorts();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SlaveConnectionSettings settings;
            if (((SlaveConnectionSetupViewModel)DataContext).TryBuildSettings(out settings))
            {
                Result = settings;
                DialogResult = true;
            }
        }
    }
}
