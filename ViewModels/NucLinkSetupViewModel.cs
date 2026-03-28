using IEC101MasterTester.Models;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;

namespace IEC101MasterTester.ViewModels
{
    public sealed class NucLinkSetupViewModel : ViewModelBase
    {
        private string _primaryPort;
        private string _backupPort;
        private string _selectedMode;
        private string _selectedGiPolicy;
        private string _validationMessage;

        public NucLinkSetupViewModel(NucRedundancySettings settings)
        {
            SerialPortOptions = new ObservableCollection<string>();
            ModeOptions = new ObservableCollection<string>(new[] { "Hot-Standby" });
            GiPolicyOptions = new ObservableCollection<string>(new[] { "Required", "Optional", "Not Expected" });
            RefreshSerialPorts();

            PrimaryPort = settings?.PrimarySerialPort;
            BackupPort = settings?.BackupSerialPort;
            SelectedMode = NormalizeRedundancyMode(settings?.RedundancyMode);
            SelectedGiPolicy = string.IsNullOrWhiteSpace(settings?.GiPolicy) ? GiPolicyOptions.ElementAtOrDefault(1) : settings.GiPolicy;

            if (string.IsNullOrWhiteSpace(PrimaryPort))
            {
                PrimaryPort = SerialPortOptions.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(BackupPort))
            {
                BackupPort = SerialPortOptions.FirstOrDefault(port => !string.Equals(port, PrimaryPort, StringComparison.OrdinalIgnoreCase))
                    ?? SerialPortOptions.FirstOrDefault();
            }
        }

        public ObservableCollection<string> SerialPortOptions { get; }
        public ObservableCollection<string> ModeOptions { get; }
        public ObservableCollection<string> GiPolicyOptions { get; }

        public string PrimaryPort
        {
            get => _primaryPort;
            set => SetProperty(ref _primaryPort, value);
        }

        public string BackupPort
        {
            get => _backupPort;
            set => SetProperty(ref _backupPort, value);
        }

        public string SelectedMode
        {
            get => _selectedMode;
            set => SetProperty(ref _selectedMode, value);
        }

        public string SelectedGiPolicy
        {
            get => _selectedGiPolicy;
            set => SetProperty(ref _selectedGiPolicy, value);
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set => SetProperty(ref _validationMessage, value);
        }

        public void RefreshSerialPorts()
        {
            string[] ports = SerialPort.GetPortNames()
                .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            SerialPortOptions.Clear();
            foreach (string port in ports)
            {
                SerialPortOptions.Add(port);
            }
        }

        public bool TryBuildSettings(ConnectionSettings baseSettings, out NucRedundancySettings settings)
        {
            settings = null;

            if (string.IsNullOrWhiteSpace(PrimaryPort))
            {
                ValidationMessage = "Link A / Main COM port is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(BackupPort))
            {
                ValidationMessage = "Link B / Backup COM port is required.";
                return false;
            }

            if (string.Equals(PrimaryPort, BackupPort, StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage = "Link A and Link B must use different COM ports.";
                return false;
            }

            ValidationMessage = string.Empty;
            settings = new NucRedundancySettings
            {
                BaseConnectionSettings = baseSettings == null ? ConnectionSettings.CreateDefault() : baseSettings.Clone(),
                PrimarySerialPort = PrimaryPort,
                BackupSerialPort = BackupPort,
                RedundancyMode = NormalizeRedundancyMode(SelectedMode),
                GiPolicy = string.IsNullOrWhiteSpace(SelectedGiPolicy) ? "Optional" : SelectedGiPolicy
            };
            return true;
        }

        private string NormalizeRedundancyMode(string mode)
        {
            return string.Equals(mode, "Concurrent/Parallel", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(mode)
                ? "Hot-Standby"
                : mode;
        }
    }
}
