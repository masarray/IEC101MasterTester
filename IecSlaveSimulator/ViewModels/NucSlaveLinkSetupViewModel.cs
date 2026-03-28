using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.ViewModels
{
    public sealed class NucSlaveLinkSetupViewModel : ViewModelBase
    {
        private string _primaryPort;
        private string _backupPort;
        private int _primaryLinkAddress;
        private string _validationMessage;

        public NucSlaveLinkSetupViewModel(SlaveConnectionSettings settings)
        {
            SerialPortOptions = new ObservableCollection<string>();
            RefreshSerialPorts();

            PrimaryPort = settings?.SerialPort;
            BackupPort = settings?.BackupSerialPort;
            PrimaryLinkAddress = settings == null || settings.LinkAddress <= 0 ? 1 : settings.LinkAddress;

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

        public string PrimaryPort
        {
            get => _primaryPort;
            set
            {
                if (SetProperty(ref _primaryPort, value))
                {
                    if (string.IsNullOrWhiteSpace(BackupPort) ||
                        string.Equals(BackupPort, value, StringComparison.OrdinalIgnoreCase))
                    {
                        BackupPort = SerialPortOptions.FirstOrDefault(port => !string.Equals(port, value, StringComparison.OrdinalIgnoreCase))
                            ?? BackupPort;
                    }
                }
            }
        }

        public string BackupPort
        {
            get => _backupPort;
            set => SetProperty(ref _backupPort, value);
        }

        public int PrimaryLinkAddress
        {
            get => _primaryLinkAddress;
            set => SetProperty(ref _primaryLinkAddress, value);
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

        public bool TryApplyTo(SlaveConnectionSettings settings)
        {
            if (settings == null)
            {
                ValidationMessage = "NUC slave settings target is missing.";
                return false;
            }

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

            if (PrimaryLinkAddress <= 0)
            {
                ValidationMessage = "Link address must be greater than zero.";
                return false;
            }

            ValidationMessage = string.Empty;
            settings.SerialPort = PrimaryPort.Trim();
            settings.BackupSerialPort = BackupPort.Trim();
            settings.LinkAddress = PrimaryLinkAddress;
            settings.BackupLinkAddress = PrimaryLinkAddress;
            settings.OperatingMode = SlaveOperatingMode.NucDualLink;
            return true;
        }
    }
}
