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
        private bool _isExpertExpanded;
        private int _baudRate;
        private int _dataBits;
        private string _parity;
        private string _stopBits;
        private string _linkLayerMode;
        private int _linkAddressLength;
        private int _linkAddress;
        private int _casduLength;
        private int _casduAddress;
        private int _ioaLength;
        private int _originatorAddress;
        private int _responseTimeoutMs;
        private int _pollIntervalMs;
        private int _class1PollIntervalMs;
        private int _busyBackoffMs;
        private int _giStartupDelayMs;
        private bool _useSingleCharAck;
        private string _validationMessage;

        public NucLinkSetupViewModel(NucRedundancySettings settings, ConnectionSettings baseSettings)
        {
            ConnectionSettings effectiveBaseSettings = settings?.BaseConnectionSettings ?? baseSettings ?? ConnectionSettings.CreateDefault();

            SerialPortOptions = new ObservableCollection<string>();
            BaudRateOptions = new ObservableCollection<int>(new[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 });
            DataBitOptions = new ObservableCollection<int>(new[] { 7, 8 });
            ParityOptions = new ObservableCollection<string>(Enum.GetNames(typeof(Parity)));
            StopBitOptions = new ObservableCollection<string>(new[] { System.IO.Ports.StopBits.One.ToString(), System.IO.Ports.StopBits.OnePointFive.ToString(), System.IO.Ports.StopBits.Two.ToString() });
            ModeOptions = new ObservableCollection<string>(new[] { "Hot-Standby" });
            GiPolicyOptions = new ObservableCollection<string>(new[] { "Required", "Optional", "Not Expected" });
            LinkLayerModeOptions = new ObservableCollection<string>(new[] { "Unbalanced", "Balanced" });
            LinkAddressLengthOptions = new ObservableCollection<int>(new[] { 1, 2 });
            CasduLengthOptions = new ObservableCollection<int>(new[] { 1, 2 });
            IoaLengthOptions = new ObservableCollection<int>(new[] { 1, 2, 3 });
            RefreshSerialPorts();

            PrimaryPort = settings?.PrimarySerialPort;
            BackupPort = settings?.BackupSerialPort;
            SelectedMode = NormalizeRedundancyMode(settings?.RedundancyMode);
            SelectedGiPolicy = string.IsNullOrWhiteSpace(settings?.GiPolicy) ? GiPolicyOptions.ElementAtOrDefault(1) : settings.GiPolicy;
            BaudRate = effectiveBaseSettings.BaudRate;
            DataBits = effectiveBaseSettings.DataBits;
            Parity = effectiveBaseSettings.Parity;
            StopBits = effectiveBaseSettings.StopBits;
            LinkLayerMode = effectiveBaseSettings.LinkLayerMode;
            LinkAddressLength = effectiveBaseSettings.LinkAddressLength;
            LinkAddress = effectiveBaseSettings.LinkAddress;
            CasduLength = effectiveBaseSettings.CasduLength;
            CasduAddress = effectiveBaseSettings.CasduAddress;
            IoaLength = effectiveBaseSettings.IoaLength;
            OriginatorAddress = effectiveBaseSettings.OriginatorAddress;
            ResponseTimeoutMs = effectiveBaseSettings.ResponseTimeoutMs;
            PollIntervalMs = effectiveBaseSettings.PollIntervalMs;
            Class1PollIntervalMs = effectiveBaseSettings.Class1PollIntervalMs;
            BusyBackoffMs = effectiveBaseSettings.BusyBackoffMs;
            GiStartupDelayMs = effectiveBaseSettings.GiStartupDelayMs;
            UseSingleCharAck = effectiveBaseSettings.UseSingleCharAck;

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
        public ObservableCollection<int> BaudRateOptions { get; }
        public ObservableCollection<int> DataBitOptions { get; }
        public ObservableCollection<string> ParityOptions { get; }
        public ObservableCollection<string> StopBitOptions { get; }
        public ObservableCollection<string> ModeOptions { get; }
        public ObservableCollection<string> GiPolicyOptions { get; }
        public ObservableCollection<string> LinkLayerModeOptions { get; }
        public ObservableCollection<int> LinkAddressLengthOptions { get; }
        public ObservableCollection<int> CasduLengthOptions { get; }
        public ObservableCollection<int> IoaLengthOptions { get; }

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

        public bool IsExpertExpanded
        {
            get => _isExpertExpanded;
            set => SetProperty(ref _isExpertExpanded, value);
        }

        public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }
        public int DataBits { get => _dataBits; set => SetProperty(ref _dataBits, value); }
        public string Parity { get => _parity; set => SetProperty(ref _parity, value); }
        public string StopBits { get => _stopBits; set => SetProperty(ref _stopBits, value); }
        public string LinkLayerMode { get => _linkLayerMode; set => SetProperty(ref _linkLayerMode, value); }
        public int LinkAddressLength { get => _linkAddressLength; set => SetProperty(ref _linkAddressLength, value); }
        public int LinkAddress { get => _linkAddress; set => SetProperty(ref _linkAddress, value); }
        public int CasduLength { get => _casduLength; set => SetProperty(ref _casduLength, value); }
        public int CasduAddress { get => _casduAddress; set => SetProperty(ref _casduAddress, value); }
        public int IoaLength { get => _ioaLength; set => SetProperty(ref _ioaLength, value); }
        public int OriginatorAddress { get => _originatorAddress; set => SetProperty(ref _originatorAddress, value); }
        public int ResponseTimeoutMs { get => _responseTimeoutMs; set => SetProperty(ref _responseTimeoutMs, value); }
        public int PollIntervalMs { get => _pollIntervalMs; set => SetProperty(ref _pollIntervalMs, value); }
        public int Class1PollIntervalMs { get => _class1PollIntervalMs; set => SetProperty(ref _class1PollIntervalMs, value); }
        public int BusyBackoffMs { get => _busyBackoffMs; set => SetProperty(ref _busyBackoffMs, value); }
        public int GiStartupDelayMs { get => _giStartupDelayMs; set => SetProperty(ref _giStartupDelayMs, value); }
        public bool UseSingleCharAck { get => _useSingleCharAck; set => SetProperty(ref _useSingleCharAck, value); }
        public string CotLengthText => "2 bytes (IEC-101 native default)";

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

        public void ApplyPlnPusertifDefaults()
        {
            ConnectionSettings defaults = ConnectionSettings.CreateDefault();
            BaudRate = defaults.BaudRate;
            DataBits = defaults.DataBits;
            Parity = defaults.Parity;
            StopBits = defaults.StopBits;
            LinkLayerMode = defaults.LinkLayerMode;
            LinkAddressLength = defaults.LinkAddressLength;
            LinkAddress = defaults.LinkAddress;
            CasduLength = defaults.CasduLength;
            CasduAddress = defaults.CasduAddress;
            IoaLength = defaults.IoaLength;
            OriginatorAddress = defaults.OriginatorAddress;
            ResponseTimeoutMs = defaults.ResponseTimeoutMs;
            PollIntervalMs = defaults.PollIntervalMs;
            Class1PollIntervalMs = defaults.Class1PollIntervalMs;
            BusyBackoffMs = defaults.BusyBackoffMs;
            GiStartupDelayMs = defaults.GiStartupDelayMs;
            UseSingleCharAck = defaults.UseSingleCharAck;
            ValidationMessage = string.Empty;
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

            if (BaudRate <= 0)
            {
                ValidationMessage = "Baud Rate must be a positive value.";
                return false;
            }

            if (DataBits != 7 && DataBits != 8)
            {
                ValidationMessage = "Data Bits must be 7 or 8.";
                return false;
            }

            if (LinkAddressLength != 1 && LinkAddressLength != 2)
            {
                ValidationMessage = "Link Address Length must be 1 or 2 bytes.";
                return false;
            }

            if (CasduLength != 1 && CasduLength != 2)
            {
                ValidationMessage = "CASDU Length must be 1 or 2 bytes.";
                return false;
            }

            if (IoaLength < 1 || IoaLength > 3)
            {
                ValidationMessage = "IOA Length must be between 1 and 3 bytes.";
                return false;
            }

            if (!IsInRange(LinkAddress, LinkAddressLength))
            {
                ValidationMessage = "Link Address is outside the valid range for the selected address length.";
                return false;
            }

            if (!IsInRange(CasduAddress, CasduLength))
            {
                ValidationMessage = "CASDU / ASDU Address is outside the valid range for the selected CASDU length.";
                return false;
            }

            if (OriginatorAddress < 0 || OriginatorAddress > 255)
            {
                ValidationMessage = "Originator Address must be between 0 and 255.";
                return false;
            }

            if (ResponseTimeoutMs < 200 || ResponseTimeoutMs > 60000)
            {
                ValidationMessage = "Response Timeout must be between 200 and 60000 ms.";
                return false;
            }

            if (PollIntervalMs < 50 || PollIntervalMs > 60000)
            {
                ValidationMessage = "Class 2 Poll Interval must be between 50 and 60000 ms.";
                return false;
            }

            if (Class1PollIntervalMs < 50 || Class1PollIntervalMs > 5000)
            {
                ValidationMessage = "Class 1 Poll Interval must be between 50 and 5000 ms.";
                return false;
            }

            if (BusyBackoffMs < 50 || BusyBackoffMs > 5000)
            {
                ValidationMessage = "Busy Backoff must be between 50 and 5000 ms.";
                return false;
            }

            if (GiStartupDelayMs < 0 || GiStartupDelayMs > 10000)
            {
                ValidationMessage = "GI Startup Delay must be between 0 and 10000 ms.";
                return false;
            }

            ValidationMessage = string.Empty;
            settings = new NucRedundancySettings
            {
                BaseConnectionSettings = new ConnectionSettings
                {
                    SerialPort = baseSettings == null ? ConnectionSettings.CreateDefault().SerialPort : baseSettings.SerialPort,
                    BaudRate = BaudRate,
                    DataBits = DataBits,
                    Parity = string.IsNullOrWhiteSpace(Parity) ? ParityOptions.FirstOrDefault() ?? "Even" : Parity,
                    StopBits = string.IsNullOrWhiteSpace(StopBits) ? StopBitOptions.FirstOrDefault() ?? System.IO.Ports.StopBits.One.ToString() : StopBits,
                    LinkLayerMode = string.IsNullOrWhiteSpace(LinkLayerMode) ? "Unbalanced" : LinkLayerMode,
                    LinkAddressLength = LinkAddressLength,
                    LinkAddress = LinkAddress,
                    CasduLength = CasduLength,
                    CasduAddress = CasduAddress,
                    IoaLength = IoaLength,
                    OriginatorAddress = OriginatorAddress,
                    ResponseTimeoutMs = ResponseTimeoutMs,
                    LinkStatusTimeoutMs = baseSettings == null ? ConnectionSettings.CreateDefault().LinkStatusTimeoutMs : baseSettings.LinkStatusTimeoutMs,
                    PollIntervalMs = PollIntervalMs,
                    UseGeneralInterrogationOnConnect = baseSettings == null || baseSettings.UseGeneralInterrogationOnConnect,
                    UseClockSyncOnConnect = baseSettings != null && baseSettings.UseClockSyncOnConnect,
                    UseSingleCharAck = UseSingleCharAck,
                    RunLoopDelayMs = baseSettings == null ? ConnectionSettings.CreateDefault().RunLoopDelayMs : baseSettings.RunLoopDelayMs,
                    Class1PollIntervalMs = Class1PollIntervalMs,
                    BusyBackoffMs = BusyBackoffMs,
                    GiStartupDelayMs = GiStartupDelayMs,
                    ChannelOperationMode = baseSettings == null ? ConnectionSettings.CreateDefault().ChannelOperationMode : baseSettings.ChannelOperationMode
                },
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

        private static bool IsInRange(int value, int byteLength)
        {
            if (value < 0)
            {
                return false;
            }

            if (byteLength <= 0 || byteLength > 4)
            {
                return false;
            }

            int max = (1 << (byteLength * 8)) - 1;
            return value <= max;
        }
    }
}
