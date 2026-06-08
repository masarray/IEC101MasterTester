using System.Linq;
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
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            JsonSettingsStore settingsStore = new JsonSettingsStore();
            IIec101MasterService masterService = new Iec101MasterServiceRouter();
            _sharedViewModel = new MainViewModel(masterService, settingsStore);

            await _sharedViewModel.InitializeAsync();

            NucRedundancyWindow window = new NucRedundancyWindow
            {
                DataContext = _sharedViewModel
            };

            MainWindow = window;
            window.Closed += StarterWindow_Closed;
            window.Show();
        }

        private void StarterWindow_Closed(object sender, System.EventArgs e)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            bool hasVisibleWindow = Windows.OfType<Window>().Any(window => !ReferenceEquals(window, sender) && window.IsVisible);
            if (!hasVisibleWindow)
            {
                Shutdown();
            }
        }
    }
}
