using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IecSlaveSimulator.Models;
using lib60870;
using lib60870.CS101;
using lib60870.linklayer;

namespace IecSlaveSimulator.Services
{
    public sealed class Iec101SlaveService : IDisposable
    {
        private readonly object _sync = new object();
        private readonly Dictionary<int, SignalDefinition> _runtimeSignals = new Dictionary<int, SignalDefinition>();
        private readonly Dictionary<int, CommandIntent> _selectedCommandIntents = new Dictionary<int, CommandIntent>();

        private SerialPort _serialPort;
        private CS101Slave _slave;
        private CancellationTokenSource _cts;
        private Task _worker;
        private DateTime _lastBackgroundPublishAt = DateTime.MinValue;
        private bool _disposed;
        private SlaveRuntimeConfig _config;
        private long _workerTickCount;
        private DateTime _lastWorkerPulseUtc = DateTime.MinValue;

        public Action<string, string> StatusLogged { get; set; }
        public Action<string, string> LinkActivityLogged { get; set; }
        public Action<int, string, string> RuntimeSignalUpdated { get; set; }
        public Action<bool, string> ConnectionStateChanged { get; set; }
        public Action<bool, bool> LinkFrameObserved { get; set; }
        public Action WorkerPulseObserved { get; set; }
        public Func<bool> ApplicationTrafficEnabledProvider { get; set; }

        public void Start(SlaveRuntimeConfig config, IEnumerable<SignalDefinition> runtimeSignals)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(config.PortName))
                throw new InvalidOperationException("COM port belum dipilih.");

            Stop();

            _config = config;
            lock (_sync)
            {
                _runtimeSignals.Clear();
                _selectedCommandIntents.Clear();
                foreach (SignalDefinition signal in runtimeSignals ?? Enumerable.Empty<SignalDefinition>())
                    _runtimeSignals[signal.Ioa] = CloneSignal(signal);
            }

            _serialPort = new SerialPort
            {
                PortName = config.PortName,
                BaudRate = config.BaudRate,
                Parity = ParseParity(config.Parity),
                DataBits = config.DataBits,
                StopBits = ParseStopBits(config.StopBits),
                Handshake = Handshake.None,
                ReadTimeout = Math.Max(10, config.RunLoopDelayMs),
                WriteTimeout = Math.Max(100, config.RunLoopDelayMs * 5)
            };
            _serialPort.Open();
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            LinkLayerParameters linkLayerParameters = new LinkLayerParameters
            {
                AddressLength = 2,
                UseSingleCharACK = true,
                TimeoutForACK = Math.Max(100, config.ResponseTimeoutMs),
                TimeoutRepeat = Math.Max(100, config.ResponseTimeoutMs),
                TimeoutLinkState = Math.Max(100, config.ResponseTimeoutMs)
            };

            _slave = new CS101Slave(_serialPort, linkLayerParameters);
            _slave.LinkLayerMode = LinkLayerMode.UNBALANCED;
            _slave.LinkLayerAddress = config.LinkAddress;
            _slave.Parameters.SizeOfCA = 2;
            _slave.Parameters.SizeOfIOA = 3;
            _slave.SetUserDataQueueSizes(Math.Max(1024, config.Class1QueueSize), 200);
            _slave.SetInterrogationHandler(HandleInterrogation, null);
            _slave.SetASDUHandler(HandleAsdu, null);
            _slave.SetReceivedRawMessageHandler(HandleRawRx, null);
            _slave.SetSentRawMessageHandler(HandleRawTx, null);

            _cts = new CancellationTokenSource();
            _workerTickCount = 0;
            _lastWorkerPulseUtc = DateTime.UtcNow;
            _worker = Task.Run(() => WorkerLoop(_cts.Token), _cts.Token);

            LogStatus("RUN", string.Format("IEC-101 slave started on {0} {1}bps, link {2}, CA {3}.", config.PortName, config.BaudRate, config.LinkAddress, config.CommonAddress));
            if (ConnectionStateChanged != null)
                ConnectionStateChanged(true, "Started");
        }

        public void Stop()
        {
            CancellationTokenSource cts = _cts;
            Task worker = _worker;

            _cts = null;
            _worker = null;

            if (cts != null)
                cts.Cancel();

            try
            {
                if (worker != null)
                    worker.Wait(1500);
            }
            catch
            {
            }

            if (_slave != null)
            {
                try
                {
                    _slave.Stop();
                }
                catch
                {
                }
            }

            lock (_sync)
            {
                _selectedCommandIntents.Clear();
            }

            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                }
                catch
                {
                }

                _serialPort.Dispose();
            }

            _slave = null;
            _serialPort = null;
            if (cts != null)
                cts.Dispose();

            if (ConnectionStateChanged != null)
                ConnectionStateChanged(false, "Stopped");
        }

        public void UpdateSignal(SignalDefinition signal)
        {
            if (signal == null)
                return;

            SignalDefinition clone = CloneSignal(signal);
            lock (_sync)
            {
                _runtimeSignals[clone.Ioa] = clone;
            }

            if (_slave == null || !clone.IsEnabled || !IsApplicationTrafficEnabled())
                return;

            if (clone.SpontaneousEnabled || string.Equals(clone.LiveCot, "CmdFb", StringComparison.OrdinalIgnoreCase))
                EnqueueSignal(clone, ResolveCot(clone.LiveCot), true);
        }

        public bool EnqueueBufferedEvent(SharedBufferEvent entry, SignalDefinition signal)
        {
            if (entry == null || signal == null)
            {
                return false;
            }

            SignalDefinition clone = CloneSignal(signal);
            clone.RuntimeValue = entry.Value;
            clone.LiveCot = entry.Cot;
            clone.Quality = string.IsNullOrWhiteSpace(entry.Quality) ? clone.Quality : entry.Quality;
            clone.UseTimestamp = entry.UseTimestamp || clone.UseTimestamp;
            if (entry.Casdu > 0)
            {
                clone.Casdu = entry.Casdu;
            }

            lock (_sync)
            {
                _runtimeSignals[clone.Ioa] = CloneSignal(clone);
            }

            if (_slave == null || !clone.IsEnabled || !IsApplicationTrafficEnabled())
            {
                return false;
            }

            ASDU asdu = CreateSingleSignalAsdu(_slave.Parameters, clone, ResolveCot(entry.Cot), entry.TimestampUtc);
            if (asdu == null)
            {
                return false;
            }

            _slave.EnqueueUserDataClass1(asdu);
            return true;
        }

        public void SyncSnapshotCache(IEnumerable<SignalDefinition> runtimeSignals)
        {
            lock (_sync)
            {
                _runtimeSignals.Clear();
                foreach (SignalDefinition signal in runtimeSignals ?? Enumerable.Empty<SignalDefinition>())
                {
                    _runtimeSignals[signal.Ioa] = CloneSignal(signal);
                }
            }
        }

        private void WorkerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _workerTickCount++;
                    if ((DateTime.UtcNow - _lastWorkerPulseUtc).TotalSeconds >= 5)
                    {
                        _lastWorkerPulseUtc = DateTime.UtcNow;
                        LogStatus("LOOP", string.Format("Worker alive on {0}. ticks={1}", _config.PortName, _workerTickCount));
                    }
                    if (WorkerPulseObserved != null)
                    {
                        WorkerPulseObserved();
                    }
                    _slave.Run();
                    PublishBackgroundSignalsIfDue();
                }
                catch (Exception ex)
                {
                    LogStatus("ERR", "Slave worker error: " + ex.Message);
                    Thread.Sleep(250);
                }

                Thread.Sleep(Math.Max(10, _config.RunLoopDelayMs));
            }
        }

        private void PublishBackgroundSignalsIfDue()
        {
            if (!IsApplicationTrafficEnabled())
                return;

            DateTime now = DateTime.UtcNow;
            if ((now - _lastBackgroundPublishAt).TotalMilliseconds < _config.BackgroundPublishIntervalMs)
                return;

            _lastBackgroundPublishAt = now;

            List<SignalDefinition> snapshot;
            lock (_sync)
            {
                snapshot = _runtimeSignals.Values.Select(CloneSignal).ToList();
            }

            foreach (SignalDefinition signal in snapshot)
            {
                if (!signal.IsEnabled || !signal.BackgroundEnabled)
                    continue;

                if (!_config.EnableMeasurementStreaming && signal.IsMeasurement)
                    continue;

                EnqueueSignal(signal, CauseOfTransmission.BACKGROUND_SCAN, false);
            }
        }

        private bool HandleInterrogation(object parameter, IMasterConnection connection, ASDU asdu, byte qoi)
        {
            if (!IsApplicationTrafficEnabled())
            {
                LogStatus("GI", "GI deferred on standby port.");
                return false;
            }

            if (asdu.Ca != _config.CommonAddress)
            {
                connection.SendACT_CON(asdu, true);
                return true;
            }

            connection.SendACT_CON(asdu, false);

            List<SignalDefinition> snapshot;
            lock (_sync)
            {
                snapshot = _runtimeSignals.Values.Where(signal => signal.IsEnabled).Select(CloneSignal).ToList();
            }

            foreach (SignalDefinition signal in snapshot)
            {
                ASDU response = CreateSingleSignalAsdu(connection.GetApplicationLayerParameters(), signal, CauseOfTransmission.INTERROGATED_BY_STATION, null);
                if (response != null)
                    connection.SendASDU(response);
            }

            connection.SendACT_TERM(asdu);
            LogStatus("GI", string.Format("GI served with {0} signal(s).", snapshot.Count));
            return true;
        }

        private bool HandleAsdu(object parameter, IMasterConnection connection, ASDU asdu)
        {
            if (!IsApplicationTrafficEnabled())
            {
                LogStatus("CMD", "Application ASDU deferred on standby port.");
                return false;
            }

            switch (asdu.TypeId)
            {
                case TypeID.C_SC_NA_1:
                    return HandleSingleCommand(connection, asdu);
                case TypeID.C_DC_NA_1:
                    return HandleDoubleCommand(connection, asdu);
                default:
                    return false;
            }
        }

        private bool HandleSingleCommand(IMasterConnection connection, ASDU asdu)
        {
            SingleCommand command = asdu.GetElement(0) as SingleCommand;
            if (command == null)
                return false;

            SignalDefinition commandSignal;
            SignalDefinition targetSignal;
            lock (_sync)
            {
                _runtimeSignals.TryGetValue(command.ObjectAddress, out commandSignal);
                targetSignal = commandSignal != null && commandSignal.LinkedStatusIoa > 0 && _runtimeSignals.ContainsKey(commandSignal.LinkedStatusIoa)
                    ? _runtimeSignals[commandSignal.LinkedStatusIoa]
                    : null;
            }

            if (commandSignal == null || targetSignal == null)
                return RejectCommand(connection, asdu, "Single command rejected: binding not found.");

            CommandIntent intent = command.State ? CommandIntent.On : CommandIntent.Off;
            if (!ValidateAndTrackCommand(connection, asdu, commandSignal, intent, command.Select, "Single command"))
                return true;

            if (!command.Select)
                ApplyCommandToTarget(commandSignal, targetSignal, intent);

            return true;
        }

        private bool HandleDoubleCommand(IMasterConnection connection, ASDU asdu)
        {
            DoubleCommand command = asdu.GetElement(0) as DoubleCommand;
            if (command == null)
                return false;

            SignalDefinition commandSignal;
            SignalDefinition targetSignal;
            lock (_sync)
            {
                _runtimeSignals.TryGetValue(command.ObjectAddress, out commandSignal);
                targetSignal = commandSignal != null && commandSignal.LinkedStatusIoa > 0 && _runtimeSignals.ContainsKey(commandSignal.LinkedStatusIoa)
                    ? _runtimeSignals[commandSignal.LinkedStatusIoa]
                    : null;
            }

            if (commandSignal == null || targetSignal == null)
                return RejectCommand(connection, asdu, "Double command rejected: binding not found.");

            CommandIntent intent = command.State == DoubleCommand.ON ? CommandIntent.Close : CommandIntent.Open;
            if (!ValidateAndTrackCommand(connection, asdu, commandSignal, intent, command.Select, "Double command"))
                return true;

            if (!command.Select)
            {
                ApplyCommandToTarget(commandSignal, targetSignal, intent);
            }

            return true;
        }

        private void ApplyCommandToTarget(SignalDefinition commandSignal, SignalDefinition targetSignal, CommandIntent intent)
        {
            SignalDefinition updatedTarget = CloneSignal(targetSignal);
            updatedTarget.ApplyBoundCommand(intent);

            lock (_sync)
            {
                _runtimeSignals[updatedTarget.Ioa] = CloneSignal(updatedTarget);
            }

            if (RuntimeSignalUpdated != null)
                RuntimeSignalUpdated(updatedTarget.Ioa, updatedTarget.RuntimeValue, updatedTarget.LiveCot);

            LogStatus("CMD", string.Format("Master command on IOA {0} updated IOA {1} -> {2}.", commandSignal.Ioa, updatedTarget.Ioa, updatedTarget.RuntimeValue));

            if (updatedTarget.SpontaneousEnabled || string.Equals(updatedTarget.LiveCot, "CmdFb", StringComparison.OrdinalIgnoreCase))
                EnqueueSignal(updatedTarget, ResolveCot(updatedTarget.LiveCot), true);
        }

        private bool ValidateAndTrackCommand(IMasterConnection connection, ASDU asdu, SignalDefinition commandSignal, CommandIntent intent, bool isSelect, string label)
        {
            switch (commandSignal.CommandOperateMode)
            {
                case CommandOperateMode.DirectOperate:
                    if (isSelect)
                    {
                        RejectCommand(connection, asdu, label + " rejected: point is configured for DO only.");
                        return false;
                    }
                    break;

                case CommandOperateMode.SelectBeforeOperate:
                    if (isSelect)
                    {
                        lock (_sync)
                        {
                            _selectedCommandIntents[commandSignal.Ioa] = intent;
                        }

                        AcknowledgeCommand(connection, asdu, label + " SBO select accepted.");
                        return false;
                    }

                    lock (_sync)
                    {
                        CommandIntent selectedIntent;
                        if (!_selectedCommandIntents.TryGetValue(commandSignal.Ioa, out selectedIntent))
                        {
                            RejectCommand(connection, asdu, label + " rejected: execute received without prior select.");
                            return false;
                        }

                        if (selectedIntent != intent)
                        {
                            _selectedCommandIntents.Remove(commandSignal.Ioa);
                            RejectCommand(connection, asdu, label + " rejected: execute does not match selected operation.");
                            return false;
                        }

                        _selectedCommandIntents.Remove(commandSignal.Ioa);
                    }
                    break;

                case CommandOperateMode.Both:
                    if (isSelect)
                    {
                        lock (_sync)
                        {
                            _selectedCommandIntents[commandSignal.Ioa] = intent;
                        }

                        AcknowledgeCommand(connection, asdu, label + " SBO select accepted.");
                        return false;
                    }

                    lock (_sync)
                    {
                        _selectedCommandIntents.Remove(commandSignal.Ioa);
                    }
                    break;
            }

            AcknowledgeCommand(connection, asdu, label + " execute accepted.");
            return true;
        }

        private bool AcknowledgeCommand(IMasterConnection connection, ASDU asdu, string message)
        {
            asdu.Cot = CauseOfTransmission.ACTIVATION_CON;
            asdu.IsNegative = false;
            connection.SendASDU(asdu);
            LogStatus("CMD", message);
            return true;
        }

        private bool RejectCommand(IMasterConnection connection, ASDU asdu, string message)
        {
            asdu.Cot = CauseOfTransmission.ACTIVATION_CON;
            asdu.IsNegative = true;
            connection.SendASDU(asdu);
            LogStatus("CMD", message);
            return true;
        }

        private bool HandleRawTx(object parameter, byte[] message, int messageSize)
        {
            LogLink("TX", BitConverter.ToString(message, 0, messageSize).Replace("-", " "));
            LogStatus("RAW", string.Format("TX {0} bytes on {1}: {2}", messageSize, _config != null ? _config.PortName : "-", DescribeFrame(message, messageSize)));
            if (LinkFrameObserved != null)
                LinkFrameObserved(true, false);
            return true;
        }

        private bool HandleRawRx(object parameter, byte[] message, int messageSize)
        {
            LogLink("RX", BitConverter.ToString(message, 0, messageSize).Replace("-", " "));
            LogStatus("RAW", string.Format("RX {0} bytes on {1}: {2}", messageSize, _config != null ? _config.PortName : "-", DescribeFrame(message, messageSize)));
            if (LinkFrameObserved != null)
                LinkFrameObserved(false, true);
            return true;
        }

        private static string DescribeFrame(byte[] message, int messageSize)
        {
            if (message == null || messageSize <= 0)
            {
                return "empty";
            }

            byte start = message[0];
            if (start == 0x10)
            {
                return messageSize > 1
                    ? string.Format("short frame ctl=0x{0:X2}", message[1])
                    : "short frame";
            }

            if (start == 0x68 && messageSize > 5)
            {
                return string.Format("long frame ctl=0x{0:X2} addr={1}", message[4], message[5]);
            }

            if (start == 0xE5)
            {
                return "single-char ack";
            }

            return string.Format("frame start=0x{0:X2}", start);
        }

        private void EnqueueSignal(SignalDefinition signal, CauseOfTransmission cot, bool forceClass1)
        {
            if (_slave == null || !IsApplicationTrafficEnabled())
                return;

            ASDU asdu = CreateSingleSignalAsdu(_slave.Parameters, signal, cot, null);
            if (asdu == null)
                return;

            if (forceClass1 || signal.SignalClass == SignalClass.Class1)
                _slave.EnqueueUserDataClass1(asdu);
            else
                _slave.EnqueueUserDataClass2(asdu);
        }

        private ASDU CreateSingleSignalAsdu(ApplicationLayerParameters parameters, SignalDefinition signal, CauseOfTransmission cot, DateTime? originalTimestampUtc)
        {
            InformationObject informationObject = CreateInformationObject(signal, originalTimestampUtc);
            if (informationObject == null)
                return null;

            ASDU asdu = new ASDU(parameters, cot, false, false, 0, signal.Casdu, false);
            asdu.AddInformationObject(informationObject);
            return asdu;
        }

        private InformationObject CreateInformationObject(SignalDefinition signal, DateTime? originalTimestampUtc)
        {
            QualityDescriptor quality = new QualityDescriptor();
            DateTime timestampSource = originalTimestampUtc.HasValue
                ? (originalTimestampUtc.Value.Kind == DateTimeKind.Utc ? originalTimestampUtc.Value.ToLocalTime() : originalTimestampUtc.Value)
                : DateTime.Now;
            CP56Time2a timestamp = signal.UseTimestamp ? new CP56Time2a(timestampSource) : null;

            switch (signal.SignalType)
            {
                case SlaveSignalType.SinglePoint:
                    if (timestamp != null)
                        return new SinglePointWithCP56Time2a(signal.Ioa, ParseOnOff(signal.RuntimeValue), quality, timestamp);
                    return new SinglePointInformation(signal.Ioa, ParseOnOff(signal.RuntimeValue), quality);
                case SlaveSignalType.DoublePoint:
                    if (timestamp != null)
                        return new DoublePointWithCP56Time2a(signal.Ioa, ParseDoublePoint(signal.RuntimeValue), quality, timestamp);
                    return new DoublePointInformation(signal.Ioa, ParseDoublePoint(signal.RuntimeValue), quality);
                case SlaveSignalType.MeasuredNormalized:
                    if (timestamp != null)
                        return new MeasuredValueNormalizedWithCP56Time2a(signal.Ioa, (float)ParseDouble(signal.RuntimeValue, signal.AnalogFrom), quality, timestamp);
                    return new MeasuredValueNormalized(signal.Ioa, (float)ParseDouble(signal.RuntimeValue, signal.AnalogFrom), quality);
                case SlaveSignalType.MeasuredScaled:
                    if (timestamp != null)
                        return new MeasuredValueScaledWithCP56Time2a(signal.Ioa, (int)Math.Round(ParseDouble(signal.RuntimeValue, signal.AnalogFrom), MidpointRounding.AwayFromZero), quality, timestamp);
                    return new MeasuredValueScaled(signal.Ioa, (int)Math.Round(ParseDouble(signal.RuntimeValue, signal.AnalogFrom), MidpointRounding.AwayFromZero), quality);
                case SlaveSignalType.MeasuredShort:
                    if (timestamp != null)
                        return new MeasuredValueShortWithCP56Time2a(signal.Ioa, (float)ParseDouble(signal.RuntimeValue, signal.AnalogFrom), quality, timestamp);
                    return new MeasuredValueShort(signal.Ioa, (float)ParseDouble(signal.RuntimeValue, signal.AnalogFrom), quality);
                case SlaveSignalType.StepPosition:
                    if (timestamp != null)
                        return new StepPositionWithCP56Time2a(signal.Ioa, (int)Math.Round(ParseDouble(signal.RuntimeValue, signal.AnalogFrom), MidpointRounding.AwayFromZero), false, quality, timestamp);
                    return new StepPositionInformation(signal.Ioa, (int)Math.Round(ParseDouble(signal.RuntimeValue, signal.AnalogFrom), MidpointRounding.AwayFromZero), false, quality);
                default:
                    return null;
            }
        }

        private static CauseOfTransmission ResolveCot(string liveCot)
        {
            switch (liveCot)
            {
                case "Spont":
                    return CauseOfTransmission.SPONTANEOUS;
                case "GI":
                    return CauseOfTransmission.INTERROGATED_BY_STATION;
                case "CmdFb":
                    return CauseOfTransmission.ACTIVATION_CON;
                default:
                    return CauseOfTransmission.BACKGROUND_SCAN;
            }
        }

        private static bool ParseOnOff(string value)
        {
            return string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "CLOSE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static DoublePointValue ParseDoublePoint(string value)
        {
            return ParseOnOff(value) ? DoublePointValue.ON : DoublePointValue.OFF;
        }

        private static double ParseDouble(string value, double fallback)
        {
            double parsed;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return parsed;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
                return parsed;

            return fallback;
        }

        private static SignalDefinition CloneSignal(SignalDefinition signal)
        {
            if (signal == null)
                return null;

            return new SignalDefinition
            {
                IsEnabled = signal.IsEnabled,
                Ioa = signal.Ioa,
                Label = signal.Label,
                SignalType = signal.SignalType,
                Casdu = signal.Casdu,
                SignalClass = signal.SignalClass,
                PublishMode = signal.PublishMode,
                BackgroundEnabled = signal.BackgroundEnabled,
                SpontaneousEnabled = signal.SpontaneousEnabled,
                UseTimestamp = signal.UseTimestamp,
                Quality = signal.Quality,
                DefaultValue = signal.DefaultValue,
                RuntimeValue = signal.RuntimeValue,
                LiveCot = signal.LiveCot,
                LinkedStatusIoa = signal.LinkedStatusIoa,
                CommandSemantic = signal.CommandSemantic,
                CommandBindingMode = signal.CommandBindingMode,
                CommandOperateMode = signal.CommandOperateMode,
                CommandDelayMs = signal.CommandDelayMs,
                AnalogAnimation = signal.AnalogAnimation,
                AnalogFrom = signal.AnalogFrom,
                AnalogTo = signal.AnalogTo,
                AnalogStep = signal.AnalogStep,
                AnimationIntervalMs = signal.AnimationIntervalMs,
                AnalogPingPong = signal.AnalogPingPong,
                DiscreteAnimation = signal.DiscreteAnimation,
                ToggleIntervalSeconds = signal.ToggleIntervalSeconds,
                Notes = signal.Notes
            };
        }

        private static Parity ParseParity(string parity)
        {
            switch (parity)
            {
                case "Odd":
                    return Parity.Odd;
                case "Even":
                    return Parity.Even;
                default:
                    return Parity.None;
            }
        }

        private static StopBits ParseStopBits(string stopBits)
        {
            return string.Equals(stopBits, "Two", StringComparison.OrdinalIgnoreCase)
                ? StopBits.Two
                : StopBits.One;
        }

        private void LogStatus(string category, string message)
        {
            if (StatusLogged != null)
                StatusLogged(category, message);
        }

        private void LogLink(string category, string message)
        {
            if (LinkActivityLogged != null)
                LinkActivityLogged(category, message);
        }

        private bool IsApplicationTrafficEnabled()
        {
            return ApplicationTrafficEnabledProvider == null || ApplicationTrafficEnabledProvider();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
        }
    }

    public sealed class SlaveRuntimeConfig
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public string Parity { get; set; }
        public int DataBits { get; set; }
        public string StopBits { get; set; }
        public int CommonAddress { get; set; }
        public int LinkAddress { get; set; }
        public int Class1QueueSize { get; set; }
        public int RunLoopDelayMs { get; set; }
        public int ResponseTimeoutMs { get; set; }
        public int BackgroundPublishIntervalMs { get; set; }
        public bool EnableMeasurementStreaming { get; set; }
    }
}
