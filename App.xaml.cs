using System.Windows;
using IEC101MasterTester.Services.Iec101;
using IEC101MasterTester.Services.Settings;
using IEC101MasterTester.ViewModels;
using IEC101MasterTester.Views;

namespace IEC101MasterTester
{
    public partial class App : Application
    {
        private MainViewModel _sharedViewModel;

        private async void App_Startup(object sender, StartupEventArgs e)
        {
            JsonSettingsStore settingsStore = new JsonSettingsStore();
            Iec101MasterService masterService = new Iec101MasterService();
            _sharedViewModel = new MainViewModel(masterService, settingsStore);

            await _sharedViewModel.InitializeAsync();

            NucRedundancyWindow window = new NucRedundancyWindow
            {
                DataContext = _sharedViewModel
            };

            MainWindow = window;
            window.Show();
        }
    }
}
