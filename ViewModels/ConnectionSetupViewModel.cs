using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.ViewModels
{
    public sealed class ConnectionSetupViewModel : ViewModelBase
    {
        private string _serialPort;
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
        private int _linkStatusTimeoutMs;
        private int _pollIntervalMs;
        private int _runLoopDelayMs;
        private int _class1PollIntervalMs;
        private int _busyBackoffMs;
        private int _giStartupDelayMs;
        private bool _useGeneralInterrogationOnConnect;
        private bool _useClockSyncOnConnect;
        private bool _useSingleCharAck;
        private string _validationMessage;

        public ConnectionSetupViewModel(ConnectionSettings settings)
        {
            SerialPortOptions = new ObservableCollection<string>();
            BaudRateOptions = new ObservableCollection<int>(new[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 });
            DataBitOptions = new ObservableCollection<int>(new[] { 7, 8 });
            ParityOptions = new ObservableCollection<string>(Enum.GetNames(typeof(Parity)));
            StopBitOptions = new ObservableCollection<string>(new[] { System.IO.Ports.StopBits.One.ToString(), System.IO.Ports.StopBits.OnePointFive.ToString(), System.IO.Ports.StopBits.Two.ToString() });
            LinkLayerModeOptions = new ObservableCollection<string>(new[] { "Unbalanced", "Balanced" });
            LinkAddressLengthOptions = new ObservableCollection<int>(new[] { 1, 2 });
            CasduLengthOptions = new ObservableCollection<int>(new[] { 1, 2 });
            IoaLengthOptions = new ObservableCollection<int>(new[] { 1, 2, 3 });

            _serialPort = settings.SerialPort;
            _baudRate = settings.BaudRate;
            _dataBits = settings.DataBits;
            _parity = settings.Parity;
            _stopBits = settings.StopBits;
            _linkLayerMode = settings.LinkLayerMode;
            _linkAddressLength = settings.LinkAddressLength;
            _linkAddress = settings.LinkAddress;
            _casduLength = settings.CasduLength;
            _casduAddress = settings.CasduAddress;
            _ioaLength = settings.IoaLength;
            _originatorAddress = settings.OriginatorAddress;
            _responseTimeoutMs = settings.ResponseTimeoutMs;
            _linkStatusTimeoutMs = settings.LinkStatusTimeoutMs;
            _pollIntervalMs = settings.PollIntervalMs;
            _runLoopDelayMs = settings.RunLoopDelayMs;
            _class1PollIntervalMs = settings.Class1PollIntervalMs;
            _busyBackoffMs = settings.BusyBackoffMs;
            _giStartupDelayMs = settings.GiStartupDelayMs;
            _useGeneralInterrogationOnConnect = settings.UseGeneralInterrogationOnConnect;
            _useClockSyncOnConnect = settings.UseClockSyncOnConnect;
            _useSingleCharAck = settings.UseSingleCharAck;

            RefreshSerialPorts();
        }

        public ObservableCollection<string> SerialPortOptions { get; }
        public ObservableCollection<int> BaudRateOptions { get; }
        public ObservableCollection<int> DataBitOptions { get; }
        public ObservableCollection<string> ParityOptions { get; }
        public ObservableCollection<string> StopBitOptions { get; }
        public ObservableCollection<string> LinkLayerModeOptions { get; }
        public ObservableCollection<int> LinkAddressLengthOptions { get; }
        public ObservableCollection<int> CasduLengthOptions { get; }
        public ObservableCollection<int> IoaLengthOptions { get; }

        public string SerialPort { get => _serialPort; set => SetProperty(ref _serialPort, value); }
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
        public int LinkStatusTimeoutMs { get => _linkStatusTimeoutMs; set => SetProperty(ref _linkStatusTimeoutMs, value); }
        public int PollIntervalMs { get => _pollIntervalMs; set => SetProperty(ref _pollIntervalMs, value); }
        public int RunLoopDelayMs { get => _runLoopDelayMs; set => SetProperty(ref _runLoopDelayMs, value); }
        public int Class1PollIntervalMs { get => _class1PollIntervalMs; set => SetProperty(ref _class1PollIntervalMs, value); }
        public int BusyBackoffMs { get => _busyBackoffMs; set => SetProperty(ref _busyBackoffMs, value); }
        public int GiStartupDelayMs { get => _giStartupDelayMs; set => SetProperty(ref _giStartupDelayMs, value); }
        public bool UseGeneralInterrogationOnConnect { get => _useGeneralInterrogationOnConnect; set => SetProperty(ref _useGeneralInterrogationOnConnect, value); }
        public bool UseClockSyncOnConnect { get => _useClockSyncOnConnect; set => SetProperty(ref _useClockSyncOnConnect, value); }
        public bool UseSingleCharAck { get => _useSingleCharAck; set => SetProperty(ref _useSingleCharAck, value); }
        public string ValidationMessage { get => _validationMessage; set => SetProperty(ref _validationMessage, value); }

        public void RefreshSerialPorts()
        {
            string selected = SerialPort;
            string[] ports = System.IO.Ports.SerialPort.GetPortNames().OrderBy(port => port, StringComparer.OrdinalIgnoreCase).ToArray();

            SerialPortOptions.Clear();
            foreach (string port in ports)
            {
                SerialPortOptions.Add(port);
            }

            if (!string.IsNullOrWhiteSpace(selected) && !SerialPortOptions.Any(port => string.Equals(port, selected, StringComparison.OrdinalIgnoreCase)))
            {
                SerialPortOptions.Add(selected);
            }
        }

        public bool TryBuildSettings(out ConnectionSettings settings)
        {
            settings = null;
            ValidationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(SerialPort))
            {
                ValidationMessage = "Serial Port is required.";
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

            if (LinkStatusTimeoutMs < 500 || LinkStatusTimeoutMs > 120000)
            {
                ValidationMessage = "Link Status Timeout must be between 500 and 120000 ms.";
                return false;
            }

            if (LinkStatusTimeoutMs <= ResponseTimeoutMs)
            {
                ValidationMessage = "Link Status Timeout should be greater than Response Timeout.";
                return false;
            }

            if (PollIntervalMs < 50 || PollIntervalMs > 60000)
            {
                ValidationMessage = "Poll Interval must be between 50 and 60000 ms.";
                return false;
            }

            if (RunLoopDelayMs < 20 || RunLoopDelayMs > 1000)
            {
                ValidationMessage = "Run Loop Delay must be between 20 and 1000 ms.";
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

            settings = new ConnectionSettings
            {
                SerialPort = SerialPort.Trim(),
                BaudRate = BaudRate,
                DataBits = DataBits,
                Parity = string.IsNullOrWhiteSpace(Parity) ? ParityOptions.FirstOrDefault() ?? "Even" : Parity,
                StopBits = string.IsNullOrWhiteSpace(StopBits) ? StopBitOptions.FirstOrDefault() ?? "One" : StopBits,
                LinkLayerMode = string.IsNullOrWhiteSpace(LinkLayerMode) ? "Unbalanced" : LinkLayerMode,
                LinkAddressLength = LinkAddressLength,
                LinkAddress = LinkAddress,
                CasduLength = CasduLength,
                CasduAddress = CasduAddress,
                IoaLength = IoaLength,
                OriginatorAddress = OriginatorAddress,
                ResponseTimeoutMs = ResponseTimeoutMs,
                LinkStatusTimeoutMs = LinkStatusTimeoutMs,
                PollIntervalMs = PollIntervalMs,
                RunLoopDelayMs = RunLoopDelayMs,
                Class1PollIntervalMs = Class1PollIntervalMs,
                BusyBackoffMs = BusyBackoffMs,
                GiStartupDelayMs = GiStartupDelayMs,
                UseGeneralInterrogationOnConnect = UseGeneralInterrogationOnConnect,
                UseClockSyncOnConnect = UseClockSyncOnConnect,
                UseSingleCharAck = UseSingleCharAck
            };

            return true;
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
