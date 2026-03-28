using System.Windows;
using IEC101MasterTester.Services.Iec101;
using IEC101MasterTester.Services.Licensing;
using IEC101MasterTester.Services.Settings;
using IEC101MasterTester.ViewModels;
using IEC101MasterTester.Views;

namespace IEC101MasterTester
{
    public partial class App : Application
    {
        private MainViewModel _sharedViewModel;
        public static LicenseManager LicenseManager { get; private set; }

        private async void App_Startup(object sender, StartupEventArgs e)
        {
            LicenseCryptoService licenseCryptoService = new LicenseCryptoService();
            LicenseStore licenseStore = new LicenseStore(licenseCryptoService);
            HardwareFingerprintService hardwareFingerprintService = new HardwareFingerprintService();
            TrialPolicyEvaluator trialPolicyEvaluator = new TrialPolicyEvaluator();
            LicenseManager = new LicenseManager(
                licenseStore,
                hardwareFingerprintService,
                trialPolicyEvaluator,
                new PlaceholderActivationKeyValidator());
            await LicenseManager.InitializeAsync();
            Properties["LicenseSnapshot"] = LicenseManager.CurrentSnapshot;
            Properties["LicenseManager"] = LicenseManager;

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
