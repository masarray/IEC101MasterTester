using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Diagnostics;
using IEC101MasterTester.Services.Iec101.Native.Asdu;
using IEC101MasterTester.Services.Iec101.Native.Frames;

namespace IEC101MasterTester.Services.Iec101.Native.Master
{
    public sealed class NativeIec101MasterService : IIec101MasterService
    {
        private sealed class PendingCommand
        {
            public string Kind { get; set; }
            public int Ioa { get; set; }
            public bool State { get; set; }
            public bool Select { get; set; }
            public int Quality { get; set; }
            public float NormalizedValue { get; set; }
        }

        private const int WorkerSleepMs = 10;
        private const int CommandFollowUpMs = 4000;

        private readonly object _syncRoot = new object();
        private readonly object _serialLock = new object();
        private readonly LineMonitorFormatter _lineMonitorFormatter;
        private readonly Iec101DataMapper _mapper;

        private ConnectionSettings _settings;
        private SerialPort _serialPort;
        private CancellationTokenSource _workerCancellation;
        private Task _workerTask;
        private PendingCommand _pendingCommand;
        private bool _isConnected;
        private bool _isConnecting;
        private bool _hasAccessDemand;
        private bool _fcb;
        private long _lastPollAt;
        private long _commandFollowUpUntil;

        public NativeIec101MasterService()
        {
            _settings = ConnectionSettings.CreateDefault();
            _lineMonitorFormatter = new LineMonitorFormatter();
            _mapper = new Iec101DataMapper();
        }

        public event EventHandler<ConnectionStatusInfo> ConnectionStateChanged;
        public event EventHandler<LineMonitorRow> LineMonitorRecordReceived;
        public event EventHandler<ValueViewerRow> ValueReceived;

        public bool IsConnected
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isConnected;
                }
            }
        }

        public void ApplySettings(ConnectionSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            lock (_syncRoot)
            {
                _settings = settings.Clone();
            }
        }

        public async Task ConnectAsync()
        {
            ConnectionSettings settings;
            lock (_syncRoot)
            {
                if (_isConnected || _isConnecting)
                {
                    return;
                }

                _isConnecting = true;
                settings = _settings.Clone();
            }

            RaiseConnectionState(ConnectionStatusInfo.Connecting);
            RaiseLine(_lineMonitorFormatter.CreateSystemRow("STATE", "Native connect requested", settings.SerialSummary));

            try
            {
                await DisconnectAsync().ConfigureAwait(false);

                SerialPort port = CreateSerialPort(settings);
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();

                CancellationTokenSource cancellation = new CancellationTokenSource();
                Task worker = Task.Run(() => RunWorker(settings, cancellation.Token), cancellation.Token);

                lock (_syncRoot)
                {
                    _serialPort = port;
                    _workerCancellation = cancellation;
                    _workerTask = worker;
                    _isConnected = true;
                    _isConnecting = false;
                    _hasAccessDemand = false;
                    _fcb = false;
                    _lastPollAt = 0;
                    _commandFollowUpUntil = 0;
                }

                Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromSettings(settings);
                ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.ResetRemoteLink(settings.LinkAddress, profile), settings, true);
                ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.RequestLinkStatus(settings.LinkAddress, profile), settings, true);

                RaiseConnectionState(ConnectionStatusInfo.Connected);
                RaiseLine(_lineMonitorFormatter.CreateSystemRow("STATE", "Native master ready", "NativeExperimental unbalanced engine is active."));

                if (settings.UseGeneralInterrogationOnConnect)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(Math.Max(0, settings.GiStartupDelayMs)).ConfigureAwait(false);
                            await SendGeneralInterrogationAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                lock (_syncRoot)
                {
                    _isConnecting = false;
                    _isConnected = false;
                }

                RaiseLine(_lineMonitorFormatter.CreateSystemRow("ERR", "Native connect failed", ex.Message));
                RaiseConnectionState(ConnectionStatusInfo.Faulted);
                await DisconnectAsync().ConfigureAwait(false);
            }
        }

        public async Task DisconnectAsync()
        {
            CancellationTokenSource cancellation;
            Task worker;
            SerialPort port;

            lock (_syncRoot)
            {
                cancellation = _workerCancellation;
                worker = _workerTask;
                port = _serialPort;
                _workerCancellation = null;
                _workerTask = null;
                _serialPort = null;
                _isConnected = false;
                _isConnecting = false;
                _pendingCommand = null;
            }

            if (cancellation != null)
            {
                cancellation.Cancel();
            }

            if (worker != null)
            {
                try
                {
                    await Task.WhenAny(worker, Task.Delay(1000)).ConfigureAwait(false);
                }
                catch
                {
                }
            }

            if (port != null)
            {
                try
                {
                    if (port.IsOpen)
                    {
                        port.Close();
                    }
                }
                catch
                {
                }

                port.Dispose();
            }

            if (cancellation != null)
            {
                cancellation.Dispose();
            }

            RaiseConnectionState(ConnectionStatusInfo.Disconnected);
        }

        public Task SendGeneralInterrogationAsync()
        {
            Enqueue(new PendingCommand { Kind = "GI" });
            return Task.CompletedTask;
        }

        public Task<bool> SendLinkLayerTestFunctionAsync()
        {
            ConnectionSettings settings = GetSettingsSnapshot();
            Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromSettings(settings);
            return Task.Run(() => ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.TestLink(settings.LinkAddress, profile), settings, true));
        }

        public void NotifyActiveLinkSwitchover()
        {
            lock (_syncRoot)
            {
                _hasAccessDemand = false;
                _lastPollAt = 0;
            }
        }

        public Task SendClockSyncAsync()
        {
            Enqueue(new PendingCommand { Kind = "ClockSync" });
            return Task.CompletedTask;
        }

        public Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0)
        {
            Enqueue(new PendingCommand { Kind = "Single", Ioa = ioa, State = state, Select = select, Quality = quality });
            return Task.CompletedTask;
        }

        public Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0)
        {
            Enqueue(new PendingCommand { Kind = "Double", Ioa = ioa, State = on, Select = select, Quality = quality });
            return Task.CompletedTask;
        }

        public Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0)
        {
            Enqueue(new PendingCommand { Kind = "Step", Ioa = ioa, State = raise, Select = select, Quality = quality });
            return Task.CompletedTask;
        }

        public Task SendSetpointNormalizedCommandAsync(int ioa, float normalizedValue, bool select = false, int quality = 0)
        {
            Enqueue(new PendingCommand { Kind = "SetpointNormalized", Ioa = ioa, NormalizedValue = normalizedValue, Select = select, Quality = quality });
            return Task.CompletedTask;
        }

        private void RunWorker(ConnectionSettings settings, CancellationToken cancellationToken)
        {
            Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromSettings(settings);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    PendingCommand command = TakePendingCommand();
                    if (command != null && settings.ChannelOperationMode == Iec101ChannelOperationMode.FullActive)
                    {
                        byte[] asdu = EncodePendingCommand(command, settings, profile);
                        if (asdu != null)
                        {
                            bool fcb = GetAndToggleFcb();
                            ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.SendUserDataConfirmed(settings.LinkAddress, fcb, asdu, profile), settings, true);
                            lock (_syncRoot)
                            {
                                _commandFollowUpUntil = NowMs() + CommandFollowUpMs;
                                _lastPollAt = 0;
                            }
                        }
                    }

                    long now = NowMs();
                    bool shouldPoll;
                    bool requestClass1;
                    lock (_syncRoot)
                    {
                        requestClass1 = _hasAccessDemand || now < _commandFollowUpUntil;
                        int interval = requestClass1 ? settings.Class1PollIntervalMs : settings.PollIntervalMs;
                        shouldPoll = settings.ChannelOperationMode == Iec101ChannelOperationMode.FullActive
                            && now - _lastPollAt >= Math.Max(10, interval);
                    }

                    if (shouldPoll)
                    {
                        bool fcb = GetAndToggleFcb();
                        byte[] poll = requestClass1
                            ? Iec101PrimaryLinkFrameFactory.RequestClass1Data(settings.LinkAddress, fcb, profile)
                            : Iec101PrimaryLinkFrameFactory.RequestClass2Data(settings.LinkAddress, fcb, profile);

                        ExecuteLinkExchange(poll, settings, true);
                        lock (_syncRoot)
                        {
                            _lastPollAt = now;
                        }
                    }
                }
                catch (Exception ex)
                {
                    RaiseLine(_lineMonitorFormatter.CreateSystemRow("ERR", "Native worker error", ex.Message));
                }

                try
                {
                    Thread.Sleep(WorkerSleepMs);
                }
                catch
                {
                }
            }
        }

        private byte[] EncodePendingCommand(PendingCommand command, ConnectionSettings settings, Iec101ApplicationProfile profile)
        {
            switch (command.Kind)
            {
                case "GI":
                    RaiseLine(_lineMonitorFormatter.CreateSystemRow("Info", "Native GI queued", "Activation C_IC_NA_1"));
                    return Iec101AsduCodec.EncodeInterrogationCommand(settings.CasduAddress, 0, 20, profile);
                case "ClockSync":
                    RaiseLine(_lineMonitorFormatter.CreateSystemRow("Info", "Native clock sync queued", "Activation C_CS_NA_1"));
                    return Iec101AsduCodec.EncodeClockSyncCommand(settings.CasduAddress, DateTime.UtcNow, profile);
                case "Single":
                    return Iec101AsduCodec.EncodeSingleCommand(settings.CasduAddress, command.Ioa, command.State, command.Select, command.Quality, profile);
                case "Double":
                    return Iec101AsduCodec.EncodeDoubleCommand(settings.CasduAddress, command.Ioa, command.State, command.Select, command.Quality, profile);
                case "Step":
                    return Iec101AsduCodec.EncodeStepCommand(settings.CasduAddress, command.Ioa, command.State, command.Select, command.Quality, profile);
                case "SetpointNormalized":
                    return Iec101AsduCodec.EncodeSetpointNormalizedCommand(settings.CasduAddress, command.Ioa, command.NormalizedValue, command.Select, command.Quality, profile);
                default:
                    return null;
            }
        }

        private bool ExecuteLinkExchange(byte[] request, ConnectionSettings settings, bool expectResponse)
        {
            SerialPort port;
            lock (_syncRoot)
            {
                port = _serialPort;
            }

            if (port == null || !port.IsOpen)
            {
                return false;
            }

            lock (_serialLock)
            {
                try
                {
                    port.Write(request, 0, request.Length);
                    ProtocolEvidenceRecorder.Shared.RecordRaw("NativeExperimental", "TX", request, request.Length, settings);
                    RaiseLine(_lineMonitorFormatter.FromRawMessage("TX", request, request.Length, settings));

                    if (!expectResponse)
                    {
                        return true;
                    }

                    byte[] response = ReadFrame(port, settings);
                    if (response == null || response.Length == 0)
                    {
                        return false;
                    }

                    ProcessReceivedFrame(response, settings);
                    return true;
                }
                catch (Exception ex)
                {
                    RaiseLine(_lineMonitorFormatter.CreateSystemRow("ERR", "Native exchange failed", ex.Message));
                    return false;
                }
            }
        }

        private void ProcessReceivedFrame(byte[] frameBytes, ConnectionSettings settings)
        {
            ProtocolEvidenceRecorder.Shared.RecordRaw("NativeExperimental", "RX", frameBytes, frameBytes.Length, settings);
            RaiseLine(_lineMonitorFormatter.FromRawMessage("RX", frameBytes, frameBytes.Length, settings));

            Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromSettings(settings);
            Iec101Frame frame;
            string frameError;
            if (!Iec101FrameCodec.TryParse(frameBytes, frameBytes.Length, profile, out frame, out frameError))
            {
                return;
            }

            if (frame.Control != null && !frame.Control.IsPrimary)
            {
                lock (_syncRoot)
                {
                    _hasAccessDemand = frame.Control.Acd;
                }
            }

            byte[] asduBytes = frame.GetAsduBytesOrEmpty();
            if (asduBytes.Length == 0)
            {
                return;
            }

            Iec101Asdu asdu;
            string asduError;
            if (!Iec101AsduCodec.TryParse(asduBytes, profile, out asdu, out asduError))
            {
                RaiseLine(_lineMonitorFormatter.CreateSystemRow("WARN", "Native ASDU parse failed", asduError));
                return;
            }

            foreach (Iec101InformationObject obj in asdu.Objects)
            {
                ValueViewerRow row = _mapper.Map(asdu, obj);
                if (row != null)
                {
                    row.Acd = frame.Control != null && !frame.Control.IsPrimary ? (frame.Control.Acd ? "1" : "0") : "-";
                    row.TrafficClass = GetTrafficClass(asdu, frame);
                    row.DeliveryContext = GetDeliveryContext(asdu);
                    RaiseValue(row);
                }
            }
        }

        private static string GetTrafficClass(Iec101Asdu asdu, Iec101Frame frame)
        {
            if (frame != null && frame.Control != null && !frame.Control.IsPrimary && frame.Control.Acd)
            {
                return "Class 1";
            }

            if (asdu != null && (asdu.Cause == Iec101CauseOfTransmission.InterrogatedByStation ||
                asdu.Cause == Iec101CauseOfTransmission.BackgroundScan ||
                asdu.Cause == Iec101CauseOfTransmission.Periodic))
            {
                return "Class 2";
            }

            return "Unknown";
        }

        private static string GetDeliveryContext(Iec101Asdu asdu)
        {
            if (asdu == null)
            {
                return "Unknown";
            }

            switch (asdu.Cause)
            {
                case Iec101CauseOfTransmission.Spontaneous:
                    return "Spontaneous";
                case Iec101CauseOfTransmission.InterrogatedByStation:
                    return "GI";
                case Iec101CauseOfTransmission.BackgroundScan:
                case Iec101CauseOfTransmission.Periodic:
                    return "Cyclic";
                default:
                    return "Unknown";
            }
        }

        private byte[] ReadFrame(SerialPort port, ConnectionSettings settings)
        {
            int first = ReadByte(port);
            if (first < 0)
            {
                return null;
            }

            if (first == Iec101FrameCodec.SingleCharacterAck)
            {
                return new byte[] { (byte)first };
            }

            int linkAddressLength = Math.Max(1, Math.Min(2, settings.LinkAddressLength));
            if (first == Iec101FrameCodec.FixedStart)
            {
                byte[] frame = new byte[4 + linkAddressLength];
                frame[0] = (byte)first;
                ReadExact(port, frame, 1, frame.Length - 1);
                return frame;
            }

            if (first == Iec101FrameCodec.VariableStart)
            {
                byte[] header = new byte[4];
                header[0] = (byte)first;
                ReadExact(port, header, 1, 3);
                int dataLength = header[1];
                byte[] frame = new byte[6 + dataLength];
                Buffer.BlockCopy(header, 0, frame, 0, header.Length);
                ReadExact(port, frame, 4, dataLength + 2);
                return frame;
            }

            return new byte[] { (byte)first };
        }

        private static void ReadExact(SerialPort port, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int value = port.ReadByte();
                if (value < 0)
                {
                    throw new IOException("Serial read ended before frame completed.");
                }

                buffer[offset + read] = (byte)value;
                read++;
            }
        }

        private static int ReadByte(SerialPort port)
        {
            try
            {
                return port.ReadByte();
            }
            catch (TimeoutException)
            {
                return -1;
            }
        }

        private void Enqueue(PendingCommand command)
        {
            lock (_syncRoot)
            {
                _pendingCommand = command;
                _lastPollAt = 0;
            }
        }

        private PendingCommand TakePendingCommand()
        {
            lock (_syncRoot)
            {
                PendingCommand command = _pendingCommand;
                _pendingCommand = null;
                return command;
            }
        }

        private bool GetAndToggleFcb()
        {
            lock (_syncRoot)
            {
                bool value = _fcb;
                _fcb = !_fcb;
                return value;
            }
        }

        private ConnectionSettings GetSettingsSnapshot()
        {
            lock (_syncRoot)
            {
                return _settings.Clone();
            }
        }

        private static SerialPort CreateSerialPort(ConnectionSettings settings)
        {
            return new SerialPort
            {
                PortName = settings.SerialPort,
                BaudRate = settings.BaudRate,
                DataBits = settings.DataBits,
                Parity = (Parity)Enum.Parse(typeof(Parity), settings.Parity),
                StopBits = (StopBits)Enum.Parse(typeof(StopBits), settings.StopBits),
                Handshake = Handshake.None,
                ReadTimeout = Math.Max(100, settings.ResponseTimeoutMs),
                WriteTimeout = Math.Max(100, settings.ResponseTimeoutMs),
                DtrEnable = false,
                RtsEnable = false
            };
        }

        private static long NowMs()
        {
            return (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
        }

        private void RaiseConnectionState(ConnectionStatusInfo info)
        {
            ConnectionStateChanged?.Invoke(this, info);
        }

        private void RaiseLine(LineMonitorRow row)
        {
            if (row != null)
            {
                LineMonitorRecordReceived?.Invoke(this, row);
            }
        }

        private void RaiseValue(ValueViewerRow row)
        {
            if (row != null)
            {
                ValueReceived?.Invoke(this, row);
            }
        }
    }
}
