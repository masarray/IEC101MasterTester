using IEC101MasterTester.Models.Licensing;
using IEC101MasterTester.Services.Licensing;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace IEC101MasterTester.SharedUi
{
    public partial class DemoModeAlert : Window
    {
        private const string SerialKeyPlaceholder = "Enter serial key here...";
        private const int ContinueCountdownSeconds = 20;
        private const bool ForceCountdownForTesting = true;
        private readonly LicenseManager _licenseManager;
        private readonly DispatcherTimer _countdownTimer;
        private LicenseSnapshot _snapshot;
        private int _countdownSeconds;
        private bool _canClose;

        public DemoModeAlert(LicenseManager licenseManager)
        {
            _licenseManager = licenseManager ?? throw new ArgumentNullException(nameof(licenseManager));
            InitializeComponent();

            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick;

            Loaded += DemoModeAlert_Loaded;
            Closed += (s, e) => _countdownTimer.Stop();
            PreviewKeyDown += DemoModeAlert_PreviewKeyDown;
        }

        public bool ContinuedInDemo { get; private set; }

        public bool ActivationSucceeded { get; private set; }

        private async void DemoModeAlert_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshFromManagerAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task RefreshFromManagerAsync()
        {
            _snapshot = _licenseManager.CurrentSnapshot ?? await _licenseManager.InitializeAsync().ConfigureAwait(true);
            ApplySnapshotToView();

            if (_snapshot != null && _snapshot.IsReminderDue)
            {
                await _licenseManager.MarkReminderShownAsync(DateTime.UtcNow).ConfigureAwait(true);
                _snapshot = _licenseManager.CurrentSnapshot;
                ApplySnapshotToView();
            }
        }

        private void ApplySnapshotToView()
        {
            LicenseSnapshot snapshot = _snapshot;
            if (snapshot == null)
            {
                return;
            }

            bool canContinue = ForceCountdownForTesting || snapshot.CanContinueInDemo;
            HeaderTitleTextBlock.Text = GetHeaderTitle(snapshot);
            HeaderDetailTextBlock.Text = GetHeaderDetail(snapshot);
            HeaderTrialTextBlock.Text = GetHeaderTrialText(snapshot);
            HeaderStateTextBlock.Text = "State: " + GetStateLabel(snapshot);
            CurrentStatusTextBlock.Text = GetCurrentStatusText(snapshot);
            StatusSummaryTextBlock.Text =
                "Continue in Demo will become available after the short acknowledgement delay. You may also activate immediately using a valid serial key.";
            HardwareIdTextBlock.Text = snapshot.HardwareId ?? "-";
            SummaryHardwareIdTextBlock.Text = snapshot.HardwareId ?? "-";
            LicenseStateValueTextBlock.Text = GetStateLabel(snapshot);
            MachineValueTextBlock.Text = Environment.MachineName;
            TrialStartValueTextBlock.Text = snapshot.FirstRunUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            DaysLeftValueTextBlock.Text = snapshot.IsLicensed
                ? "Licensed"
                : snapshot.IsExpired
                    ? "0 days remaining"
                    : snapshot.DaysRemaining.ToString(CultureInfo.InvariantCulture) + " days remaining";
            TamperValueTextBlock.Text = snapshot.IsTamperDetected
                ? (snapshot.TamperReason ?? "Integrity issue detected")
                : "No integrity issue detected";
            ContinueTitleTextBlock.Text = "Continue in Demo";
            ContinueDetailTextBlock.Text =
                "You may continue with evaluation restrictions after the short acknowledgement delay.";
            BottomStatusTextBlock.Text = GetBottomStatusText(snapshot);

            if (snapshot.IsLicensed)
            {
                ActivationSucceeded = true;
                DialogResult = true;
                Close();
                return;
            }

            if (!_countdownTimer.IsEnabled && _countdownSeconds <= 0)
            {
                _countdownSeconds = ContinueCountdownSeconds;
            }

            UpdateCountdownUi();

            _countdownTimer.Stop();
            if (_countdownSeconds > 0)
            {
                _countdownTimer.Start();
            }
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            if (_countdownSeconds > 0)
            {
                _countdownSeconds--;
            }

            if (_countdownSeconds <= 0)
            {
                _countdownSeconds = 0;
                _countdownTimer.Stop();
            }

            UpdateCountdownUi();
        }

        private void UpdateCountdownOverlayVisual()
        {
            if (_snapshot == null)
            {
                return;
            }

            bool canContinue = ForceCountdownForTesting || _snapshot.CanContinueInDemo;
            bool isCountdownFinished = _countdownSeconds <= 0;

            if (!canContinue || isCountdownFinished)
            {
                ContinueCountdownOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            ContinueCountdownOverlay.Visibility = Visibility.Visible;
            ContinueCountdownOverlayTextBlock.Text = string.Format(
                CultureInfo.InvariantCulture,
                "00:{0:00}",
                Math.Max(0, _countdownSeconds));
        }

        private void UpdateCountdownUi()
        {
            if (_snapshot == null)
            {
                return;
            }

            bool canContinue = ForceCountdownForTesting || _snapshot.CanContinueInDemo;
            bool isCountdownFinished = _countdownSeconds <= 0;

            ContinueInDemoButton.IsEnabled = canContinue && isCountdownFinished;

            if (canContinue && !isCountdownFinished)
            {
                ContinueCountdownFooterTextBlock.Visibility = Visibility.Visible;
                ContinueCountdownFooterTextBlock.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Available in 00:{0:00}",
                    Math.Max(0, _countdownSeconds));
            }
            else
            {
                ContinueCountdownFooterTextBlock.Visibility = Visibility.Collapsed;
            }

            if (ContinueInDemoButton.IsEnabled)
            {
                ContinueInDemoButton.Opacity = 1.0;
            }
            else
            {
                ContinueInDemoButton.Opacity = 1.0;
            }

            UpdateCountdownOverlayVisual();
        }

        private void CopyHardwareIdButton_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshot == null || string.IsNullOrWhiteSpace(_snapshot.HardwareId))
            {
                return;
            }

            Clipboard.SetText(_snapshot.HardwareId);
        }

        private void ContinueInDemoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshot == null)
            {
                return;
            }

            if (!ForceCountdownForTesting && !_snapshot.CanContinueInDemo)
            {
                return;
            }

            if (_countdownSeconds > 0)
            {
                return;
            }

            ContinuedInDemo = true;
            _canClose = true;
            DialogResult = true;
            Close();
        }

        private async void ActivateLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            string activationKey = GetEnteredSerialKey();
            if (string.IsNullOrWhiteSpace(activationKey))
            {
                MessageBox.Show(this, "Enter a serial key first.", "License Activation", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ActivateLicenseButton.IsEnabled = false;
            try
            {
                string reason;
                if (!_licenseManager.ValidateActivationKey(activationKey, out reason))
                {
                    MessageBox.Show(this, reason ?? "Activation key is not valid.", "License Activation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool activated = await _licenseManager.ActivateAsync(activationKey).ConfigureAwait(true);
                if (!activated)
                {
                    MessageBox.Show(this, "Activation failed.", "License Activation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ActivationSucceeded = true;
                _snapshot = _licenseManager.CurrentSnapshot;
                MessageBox.Show(this, "License activated successfully.", "License Activation", MessageBoxButton.OK, MessageBoxImage.Information);
                _canClose = true;
                DialogResult = true;
                Close();
            }
            finally
            {
                ActivateLicenseButton.IsEnabled = true;
            }
        }

        private void SerialKeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.Equals(SerialKeyTextBox.Text, SerialKeyPlaceholder, StringComparison.Ordinal))
            {
                SerialKeyTextBox.Text = string.Empty;
            }
        }

        private void SerialKeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SerialKeyTextBox.Text))
            {
                SerialKeyTextBox.Text = SerialKeyPlaceholder;
            }
        }

        private string GetEnteredSerialKey()
        {
            string value = SerialKeyTextBox.Text;
            return string.Equals(value, SerialKeyPlaceholder, StringComparison.Ordinal) ? string.Empty : value.Trim();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_canClose)
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        private void DemoModeAlert_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_canClose && e.SystemKey == System.Windows.Input.Key.F4 && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt)
            {
                e.Handled = true;
            }
        }

        private static string GetHeaderTitle(LicenseSnapshot snapshot)
        {
            if (snapshot.IsPermanentDemoLocked)
            {
                return "Activation Required";
            }

            return snapshot.IsExpired ? "Evaluation Expired" : "Evaluation Mode";
        }

        private static string GetHeaderDetail(LicenseSnapshot snapshot)
        {
            if (snapshot.IsPermanentDemoLocked)
            {
                return "This installation is locked because license integrity checks detected a problem. Activate the license to resume use on this machine.";
            }

            if (snapshot.IsExpired)
            {
                return "The evaluation period has expired. You may still continue in demo mode with restrictions, or activate the license to unlock uninterrupted engineering workflow.";
            }

            return "This application is running under evaluation restrictions. Activate the license to unlock uninterrupted engineering workflow.";
        }

        private static string GetHeaderTrialText(LicenseSnapshot snapshot)
        {
            if (snapshot.IsLicensed)
            {
                return "Licensed";
            }

            if (snapshot.IsExpired)
            {
                return "Trial expired";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Trial day {0} / {1}",
                Math.Min(snapshot.DaysElapsed + 1, TrialPolicyEvaluator.TrialDays),
                TrialPolicyEvaluator.TrialDays);
        }

        private static string GetCurrentStatusText(LicenseSnapshot snapshot)
        {
            if (snapshot.IsPermanentDemoLocked)
            {
                return "License integrity protection has locked demo access for this installation. Activation is now required before the application can continue.";
            }

            if (snapshot.IsExpired)
            {
                return "The evaluation period has expired. Core protocol visibility may remain available in demo mode, while advanced capabilities stay restricted until activation.";
            }

            return "This software is currently running in evaluation mode. Core protocol visibility remains available for review and testing, while selected advanced capabilities remain restricted until activation.";
        }

        private static string GetStateLabel(LicenseSnapshot snapshot)
        {
            if (snapshot.IsLicensed)
            {
                return "Licensed";
            }

            if (snapshot.IsPermanentDemoLocked)
            {
                return "Permanent Demo Locked";
            }

            return snapshot.IsExpired ? "Evaluation Expired" : "Evaluation Mode";
        }

        private static string GetBottomStatusText(LicenseSnapshot snapshot)
        {
            if (snapshot.IsPermanentDemoLocked)
            {
                return "A tamper or clock-integrity issue was detected. Activation is required before the application can continue.";
            }

            return snapshot.IsExpired
                ? "Evaluation has expired. Demo mode can still be used with restrictions unless activation is completed."
                : "Evaluation mode keeps the application usable for review while selected advanced capabilities remain restricted.";
        }
    }
}
