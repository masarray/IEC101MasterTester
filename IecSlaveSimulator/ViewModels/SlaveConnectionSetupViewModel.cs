using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.ViewModels
{
    public sealed class SlaveConnectionSetupViewModel : ViewModelBase
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
        private int _commonAddress;
        private int _ioaLength;
        private int _originatorAddress;
        private int _responseTimeoutMs;
        private int _backgroundPublishIntervalMs;
        private int _runLoopDelayMs;
        private int _class1QueueSize;
        private bool _enableMeasurementStreaming;
        private SlaveOperatingMode _operatingMode;
        private string _backupSerialPort;
        private int _backupLinkAddress;
        private bool _emitGatewayBaselineOnStart;
        private bool _shareEventBufferAcrossLinks;
        private BufferInjectionMode _bufferInjectionMode;
        private int _bufferInjectionSignalCount;
        private int _bufferInjectionBurstSize;
        private int _bufferInjectionIntervalMs;
        private string _validationMessage;

        public SlaveConnectionSetupViewModel(SlaveConnectionSettings settings)
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

            settings = settings ?? SlaveConnectionSettings.CreateDefault();
            _serialPort = settings.SerialPort;
            _baudRate = settings.BaudRate;
            _dataBits = settings.DataBits;
            _parity = settings.Parity;
            _stopBits = settings.StopBits;
            _linkLayerMode = settings.LinkLayerMode;
            _linkAddressLength = settings.LinkAddressLength;
            _linkAddress = settings.LinkAddress;
            _casduLength = settings.CasduLength;
            _commonAddress = settings.CommonAddress;
            _ioaLength = settings.IoaLength;
            _originatorAddress = settings.OriginatorAddress;
            _responseTimeoutMs = settings.ResponseTimeoutMs;
            _backgroundPublishIntervalMs = settings.BackgroundPublishIntervalMs;
            _runLoopDelayMs = settings.RunLoopDelayMs;
            _class1QueueSize = settings.Class1QueueSize;
            _enableMeasurementStreaming = settings.EnableMeasurementStreaming;
            _operatingMode = settings.OperatingMode;
            _backupSerialPort = settings.BackupSerialPort;
            _backupLinkAddress = settings.BackupLinkAddress > 0 ? settings.BackupLinkAddress : settings.LinkAddress;
            _emitGatewayBaselineOnStart = settings.EmitGatewayBaselineOnStart;
            _shareEventBufferAcrossLinks = settings.ShareEventBufferAcrossLinks;
            _bufferInjectionMode = settings.BufferInjectionMode;
            _bufferInjectionSignalCount = settings.BufferInjectionSignalCount;
            _bufferInjectionBurstSize = settings.BufferInjectionBurstSize;
            _bufferInjectionIntervalMs = settings.BufferInjectionIntervalMs;

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
        public ObservableCollection<SlaveOperatingMode> OperatingModeOptions { get; } = new ObservableCollection<SlaveOperatingMode>((SlaveOperatingMode[])Enum.GetValues(typeof(SlaveOperatingMode)));
        public ObservableCollection<BufferInjectionMode> BufferInjectionModeOptions { get; } = new ObservableCollection<BufferInjectionMode>((BufferInjectionMode[])Enum.GetValues(typeof(BufferInjectionMode)));

        public string SerialPort { get => _serialPort; set => SetProperty(ref _serialPort, value); }
        public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }
        public int DataBits { get => _dataBits; set => SetProperty(ref _dataBits, value); }
        public string Parity { get => _parity; set => SetProperty(ref _parity, value); }
        public string StopBits { get => _stopBits; set => SetProperty(ref _stopBits, value); }
        public string LinkLayerMode { get => _linkLayerMode; set => SetProperty(ref _linkLayerMode, value); }
        public int LinkAddressLength { get => _linkAddressLength; set => SetProperty(ref _linkAddressLength, value); }
        public int LinkAddress { get => _linkAddress; set => SetProperty(ref _linkAddress, value); }
        public int CasduLength { get => _casduLength; set => SetProperty(ref _casduLength, value); }
        public int CommonAddress { get => _commonAddress; set => SetProperty(ref _commonAddress, value); }
        public int IoaLength { get => _ioaLength; set => SetProperty(ref _ioaLength, value); }
        public int OriginatorAddress { get => _originatorAddress; set => SetProperty(ref _originatorAddress, value); }
        public int ResponseTimeoutMs { get => _responseTimeoutMs; set => SetProperty(ref _responseTimeoutMs, value); }
        public int BackgroundPublishIntervalMs { get => _backgroundPublishIntervalMs; set => SetProperty(ref _backgroundPublishIntervalMs, value); }
        public int RunLoopDelayMs { get => _runLoopDelayMs; set => SetProperty(ref _runLoopDelayMs, value); }
        public int Class1QueueSize { get => _class1QueueSize; set => SetProperty(ref _class1QueueSize, value); }
        public bool EnableMeasurementStreaming { get => _enableMeasurementStreaming; set => SetProperty(ref _enableMeasurementStreaming, value); }
        public SlaveOperatingMode OperatingMode { get => _operatingMode; set => SetProperty(ref _operatingMode, value); }
        public string BackupSerialPort { get => _backupSerialPort; set => SetProperty(ref _backupSerialPort, value); }
        public int BackupLinkAddress { get => _backupLinkAddress; set => SetProperty(ref _backupLinkAddress, value); }
        public bool EmitGatewayBaselineOnStart { get => _emitGatewayBaselineOnStart; set => SetProperty(ref _emitGatewayBaselineOnStart, value); }
        public bool ShareEventBufferAcrossLinks { get => _shareEventBufferAcrossLinks; set => SetProperty(ref _shareEventBufferAcrossLinks, value); }
        public BufferInjectionMode BufferInjectionMode { get => _bufferInjectionMode; set => SetProperty(ref _bufferInjectionMode, value); }
        public int BufferInjectionSignalCount { get => _bufferInjectionSignalCount; set => SetProperty(ref _bufferInjectionSignalCount, value); }
        public int BufferInjectionBurstSize { get => _bufferInjectionBurstSize; set => SetProperty(ref _bufferInjectionBurstSize, value); }
        public int BufferInjectionIntervalMs { get => _bufferInjectionIntervalMs; set => SetProperty(ref _bufferInjectionIntervalMs, value); }
        public string ValidationMessage { get => _validationMessage; set => SetProperty(ref _validationMessage, value); }

        public void RefreshSerialPorts()
        {
            string selected = SerialPort;
            string[] ports = System.IO.Ports.SerialPort.GetPortNames().OrderBy(port => port, StringComparer.OrdinalIgnoreCase).ToArray();

            SerialPortOptions.Clear();
            foreach (string port in ports)
                SerialPortOptions.Add(port);

            if (!string.IsNullOrWhiteSpace(selected) && !SerialPortOptions.Any(port => string.Equals(port, selected, StringComparison.OrdinalIgnoreCase)))
                SerialPortOptions.Add(selected);
        }

        public bool TryBuildSettings(out SlaveConnectionSettings settings)
        {
            settings = null;
            ValidationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(SerialPort))
            {
                ValidationMessage = "Serial Port is required.";
                return false;
            }

            if (BaudRate <= 0 || (DataBits != 7 && DataBits != 8))
            {
                ValidationMessage = "Serial parameter tidak valid.";
                return false;
            }

            if (LinkAddressLength != 1 && LinkAddressLength != 2)
            {
                ValidationMessage = "Link address length harus 1 atau 2 byte.";
                return false;
            }

            if (CasduLength != 1 && CasduLength != 2)
            {
                ValidationMessage = "CA length harus 1 atau 2 byte.";
                return false;
            }

            if (IoaLength < 1 || IoaLength > 3)
            {
                ValidationMessage = "IOA length harus 1 sampai 3 byte.";
                return false;
            }

            if (ResponseTimeoutMs < 100 || ResponseTimeoutMs > 60000)
            {
                ValidationMessage = "Response timeout harus 100 sampai 60000 ms.";
                return false;
            }

            if (BackgroundPublishIntervalMs < 50 || BackgroundPublishIntervalMs > 60000)
            {
                ValidationMessage = "Background publish interval harus 50 sampai 60000 ms.";
                return false;
            }

            if (OperatingMode == SlaveOperatingMode.NucDualLink && string.IsNullOrWhiteSpace(BackupSerialPort))
            {
                ValidationMessage = "Backup Serial Port wajib diisi untuk mode NUC dual-link.";
                return false;
            }

            if (RunLoopDelayMs < 10 || RunLoopDelayMs > 1000)
            {
                ValidationMessage = "Run loop delay harus 10 sampai 1000 ms.";
                return false;
            }

            settings = new SlaveConnectionSettings
            {
                SerialPort = SerialPort.Trim(),
                BaudRate = BaudRate,
                DataBits = DataBits,
                Parity = string.IsNullOrWhiteSpace(Parity) ? "Even" : Parity,
                StopBits = string.IsNullOrWhiteSpace(StopBits) ? "One" : StopBits,
                LinkLayerMode = string.IsNullOrWhiteSpace(LinkLayerMode) ? "Unbalanced" : LinkLayerMode,
                LinkAddressLength = LinkAddressLength,
                LinkAddress = LinkAddress,
                CasduLength = CasduLength,
                CommonAddress = CommonAddress,
                IoaLength = IoaLength,
                OriginatorAddress = OriginatorAddress,
                ResponseTimeoutMs = ResponseTimeoutMs,
                BackgroundPublishIntervalMs = BackgroundPublishIntervalMs,
                RunLoopDelayMs = RunLoopDelayMs,
                Class1QueueSize = Class1QueueSize,
                EnableMeasurementStreaming = EnableMeasurementStreaming,
                OperatingMode = OperatingMode,
                BackupSerialPort = string.IsNullOrWhiteSpace(BackupSerialPort) ? string.Empty : BackupSerialPort.Trim(),
                BackupLinkAddress = OperatingMode == SlaveOperatingMode.NucDualLink ? LinkAddress : BackupLinkAddress,
                EmitGatewayBaselineOnStart = EmitGatewayBaselineOnStart,
                ShareEventBufferAcrossLinks = ShareEventBufferAcrossLinks,
                BufferInjectionMode = BufferInjectionMode,
                BufferInjectionSignalCount = BufferInjectionSignalCount,
                BufferInjectionBurstSize = BufferInjectionBurstSize,
                BufferInjectionIntervalMs = BufferInjectionIntervalMs
            };

            return true;
        }
    }
}
