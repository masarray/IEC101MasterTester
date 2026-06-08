using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IecSlaveSimulator.Models;
using IEC101MasterTester.Services.Iec101.Native;
using IEC101MasterTester.Services.Iec101.Native.Asdu;
using IEC101MasterTester.Services.Iec101.Native.Frames;

namespace IecSlaveSimulator.Services
{
    public sealed class Iec101SlaveService : IDisposable
    {
        private readonly object _sync = new object();
        private readonly Dictionary<int, SignalDefinition> _runtimeSignals = new Dictionary<int, SignalDefinition>();
        private readonly Dictionary<int, CommandIntent> _selectedCommandIntents = new Dictionary<int, CommandIntent>();
        private readonly Queue<byte[]> _class1Queue = new Queue<byte[]>();
        private readonly Queue<byte[]> _class2Queue = new Queue<byte[]>();

        private SerialPort _serialPort;
        private CancellationTokenSource _cts;
        private Task _worker;
        private DateTime _lastBackgroundPublishAt = DateTime.MinValue;
        private bool _disposed;
        private SlaveRuntimeConfig _config;
        private long _workerTickCount;
        private DateTime _lastWorkerPulseUtc = DateTime.MinValue;
        private Iec101ApplicationProfile _profile = Iec101ApplicationProfile.DefaultPln101();

        public Action<string, string> StatusLogged { get; set; }
        public Action<string, string> LinkActivityLogged { get; set; }
        public Action<int, string, string> RuntimeSignalUpdated { get; set; }
        public Action<bool, string> ConnectionStateChanged { get; set; }
        public Action<bool, bool> LinkFrameObserved { get; set; }
        public Action MasterApplicationTrafficObserved { get; set; }
        public Action WorkerPulseObserved { get; set; }
        public Func<bool> ApplicationTrafficEnabledProvider { get; set; }

        public void Start(SlaveRuntimeConfig config, IEnumerable<SignalDefinition> runtimeSignals)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrWhiteSpace(config.PortName))
            {
                throw new InvalidOperationException("COM port belum dipilih.");
            }

            Stop();

            _config = config;
            _profile = Iec101ApplicationProfile.FromValues(2, 2, 3, 0);
            lock (_sync)
            {
                _runtimeSignals.Clear();
                _selectedCommandIntents.Clear();
                _class1Queue.Clear();
                _class2Queue.Clear();
                foreach (SignalDefinition signal in runtimeSignals ?? Enumerable.Empty<SignalDefinition>())
                {
                    _runtimeSignals[signal.Ioa] = CloneSignal(signal);
                }
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
                WriteTimeout = Math.Max(100, config.RunLoopDelayMs * 5),
                DtrEnable = false,
                RtsEnable = false
            };
            _serialPort.Open();
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            _cts = new CancellationTokenSource();
            _workerTickCount = 0;
            _lastWorkerPulseUtc = DateTime.UtcNow;
            _worker = Task.Run(() => WorkerLoop(_cts.Token), _cts.Token);

            LogStatus("RUN", string.Format("Native IEC-101 slave started on {0} {1}bps, link {2}, CA {3}.", config.PortName, config.BaudRate, config.LinkAddress, config.CommonAddress));
            if (ConnectionStateChanged != null)
            {
                ConnectionStateChanged(true, "Started");
            }
        }

        public void Stop()
        {
            CancellationTokenSource cts = _cts;
            Task worker = _worker;

            _cts = null;
            _worker = null;

            if (cts != null)
            {
                cts.Cancel();
            }

            try
            {
                if (worker != null)
                {
                    worker.Wait(1500);
                }
            }
            catch
            {
            }

            lock (_sync)
            {
                _selectedCommandIntents.Clear();
                _class1Queue.Clear();
                _class2Queue.Clear();
            }

            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                }
                catch
                {
                }

                _serialPort.Dispose();
            }

            _serialPort = null;
            if (cts != null)
            {
                cts.Dispose();
            }

            if (ConnectionStateChanged != null)
            {
                ConnectionStateChanged(false, "Stopped");
            }
        }

        public void UpdateSignal(SignalDefinition signal)
        {
            if (signal == null)
            {
                return;
            }

            SignalDefinition clone = CloneSignal(signal);
            lock (_sync)
            {
                _runtimeSignals[clone.Ioa] = clone;
            }

            if (_serialPort == null || !clone.IsEnabled || !IsApplicationTrafficEnabled())
            {
                return;
            }

            if (clone.SpontaneousEnabled || string.Equals(clone.LiveCot, "CmdFb", StringComparison.OrdinalIgnoreCase))
            {
                EnqueueSignal(clone, ResolveCot(clone.LiveCot), true);
            }
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

            if (_serialPort == null || !clone.IsEnabled || !IsApplicationTrafficEnabled())
            {
                return false;
            }

            byte[] asdu = CreateSingleSignalAsdu(clone, ResolveCot(entry.Cot), entry.TimestampUtc);
            if (asdu == null)
            {
                return false;
            }

            EnqueueAsdu(asdu, true);
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
                        LogStatus("LOOP", string.Format("Native worker alive on {0}. ticks={1}", _config.PortName, _workerTickCount));
                    }

                    if (WorkerPulseObserved != null)
                    {
                        WorkerPulseObserved();
                    }

                    ProcessIncomingFrameIfAvailable();
                    PublishBackgroundSignalsIfDue();
                }
                catch (Exception ex)
                {
                    LogStatus("ERR", "Native slave worker error: " + ex.Message);
                    Thread.Sleep(250);
                }

                Thread.Sleep(Math.Max(10, _config.RunLoopDelayMs));
            }
        }

        private void ProcessIncomingFrameIfAvailable()
        {
            SerialPort port = _serialPort;
            if (port == null || !port.IsOpen || port.BytesToRead <= 0)
            {
                return;
            }

            byte[] raw = ReadFrame(port);
            if (raw == null || raw.Length == 0)
            {
                return;
            }

            LogRawRx(raw);

            Iec101Frame frame;
            string error;
            if (!Iec101FrameCodec.TryParse(raw, raw.Length, _profile, out frame, out error))
            {
                LogStatus("WARN", "Native slave ignored invalid frame: " + error);
                return;
            }

            if (frame.Control != null && frame.Control.IsPrimary && IsPrimaryApplicationTraffic(frame))
            {
                NotifyMasterApplicationTraffic();
            }

            if (frame.FrameType == Iec101FrameType.Fixed && frame.Control != null && frame.Control.IsPrimary)
            {
                HandlePrimaryFixed(frame);
                return;
            }

            if (frame.FrameType == Iec101FrameType.Variable && frame.Control != null && frame.Control.IsPrimary)
            {
                HandlePrimaryVariable(frame);
            }
        }

        private byte[] ReadFrame(SerialPort port)
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

            if (first == Iec101FrameCodec.FixedStart)
            {
                byte[] frame = new byte[6];
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


        private static bool IsPrimaryApplicationTraffic(Iec101Frame frame)
        {
            if (frame == null || frame.Control == null || !frame.Control.IsPrimary)
            {
                return false;
            }

            if (frame.FrameType == Iec101FrameType.Variable && frame.GetAsduBytesOrEmpty().Length > 0)
            {
                return true;
            }

            if (frame.FrameType == Iec101FrameType.Fixed)
            {
                int fc = frame.Control.FunctionCode;
                return fc == 10 || fc == 11; // request Class 1 / Class 2 data = active polling, not standby supervision
            }

            return false;
        }

        private void NotifyMasterApplicationTraffic()
        {
            if (MasterApplicationTrafficObserved != null)
            {
                MasterApplicationTrafficObserved();
            }
        }

        private void HandlePrimaryFixed(Iec101Frame frame)
        {
            int fc = frame.Control.FunctionCode;
            switch (fc)
            {
                case 0: // reset remote link
                case 2: // test link
                case 8: // reset FCB
                    SendFixedSecondary(0);
                    break;
                case 9: // request link status
                    SendFixedSecondary(11);
                    break;
                case 10: // request class 1
                    SendQueuedAsduOrNoData(true);
                    break;
                case 11: // request class 2
                    SendQueuedAsduOrNoData(false);
                    break;
                default:
                    SendFixedSecondary(1);
                    break;
            }
        }

        private void HandlePrimaryVariable(Iec101Frame frame)
        {
            byte[] asduBytes = frame.GetAsduBytesOrEmpty();
            if (asduBytes.Length == 0)
            {
                SendFixedSecondary(0);
                return;
            }

            Iec101Asdu asdu;
            string error;
            if (!Iec101AsduCodec.TryParse(asduBytes, _profile, out asdu, out error))
            {
                LogStatus("WARN", "Native slave ASDU parse failed: " + error);
                SendFixedSecondary(1);
                return;
            }

            if (!IsApplicationTrafficEnabled())
            {
                SendFixedSecondary(0);
                LogStatus("APP", "Application ASDU deferred on standby port.");
                return;
            }

            switch (asdu.TypeId)
            {
                case Iec101TypeId.C_IC_NA_1:
                    HandleInterrogation(asdu);
                    break;
                case Iec101TypeId.C_SC_NA_1:
                    HandleSingleCommand(asdu);
                    break;
                case Iec101TypeId.C_DC_NA_1:
                    HandleDoubleCommand(asdu);
                    break;
                case Iec101TypeId.C_RC_NA_1:
                    HandleStepCommand(asdu);
                    break;
                case Iec101TypeId.C_SE_NA_1:
                    HandleNormalizedSetpointCommand(asdu);
                    break;
                case Iec101TypeId.C_CS_NA_1:
                    EnqueueCommandConfirmation(asdu, false);
                    LogStatus("CLOCK", "Clock sync command acknowledged by native slave.");
                    break;
                default:
                    LogStatus("APP", "Unsupported ASDU received: " + asdu.TypeId);
                    break;
            }

            // A confirmed primary ASDU is acknowledged after application handling so the
            // secondary ACD bit reflects newly queued Class 1 data (GI responses, command
            // confirmations). This mirrors real IEC-101 behaviour better than acknowledging
            // first and polling blind afterwards.
            SendFixedSecondary(0);
        }

        private void HandleInterrogation(Iec101Asdu asdu)
        {
            if (asdu.CommonAddress != _config.CommonAddress)
            {
                EnqueueCommandConfirmation(asdu, true);
                return;
            }

            EnqueueCommandConfirmation(asdu, false);

            List<SignalDefinition> snapshot;
            lock (_sync)
            {
                snapshot = _runtimeSignals.Values.Where(signal => signal.IsEnabled).Select(CloneSignal).ToList();
            }

            foreach (SignalDefinition signal in snapshot)
            {
                byte[] response = CreateSingleSignalAsdu(signal, Iec101CauseOfTransmission.InterrogatedByStation, null);
                if (response != null)
                {
                    EnqueueAsdu(response, true);
                }
            }

            byte[] termination = Iec101AsduCodec.EncodeInformationObjectAsdu(
                Iec101TypeId.C_IC_NA_1,
                Iec101CauseOfTransmission.ActivationTermination,
                false,
                asdu.CommonAddress,
                GetFirstIoaOrZero(asdu),
                GetFirstObjectPayloadOrDefault(asdu, new byte[] { 20 }),
                _profile);
            EnqueueAsdu(termination, true);
            LogStatus("GI", string.Format("Native GI served with {0} signal(s).", snapshot.Count));
        }

        private void HandleSingleCommand(Iec101Asdu asdu)
        {
            Iec101InformationObject command = GetFirstObject(asdu);
            if (command == null)
            {
                return;
            }

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
            {
                RejectCommand(asdu, "Single command rejected: binding not found.");
                return;
            }

            CommandIntent intent = string.Equals(command.ValueText, "ON", StringComparison.OrdinalIgnoreCase) ? CommandIntent.On : CommandIntent.Off;
            bool isSelect = command.Select.HasValue && command.Select.Value;
            if (!ValidateAndTrackCommand(asdu, commandSignal, intent, isSelect, "Single command"))
            {
                return;
            }

            if (!isSelect)
            {
                ApplyCommandToTarget(commandSignal, targetSignal, intent);
            }
        }

        private void HandleDoubleCommand(Iec101Asdu asdu)
        {
            Iec101InformationObject command = GetFirstObject(asdu);
            if (command == null)
            {
                return;
            }

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
            {
                RejectCommand(asdu, "Double command rejected: binding not found.");
                return;
            }

            CommandIntent intent = string.Equals(command.ValueText, "ON", StringComparison.OrdinalIgnoreCase) ? CommandIntent.Close : CommandIntent.Open;
            bool isSelect = command.Select.HasValue && command.Select.Value;
            if (!ValidateAndTrackCommand(asdu, commandSignal, intent, isSelect, "Double command"))
            {
                return;
            }

            if (!isSelect)
            {
                ApplyCommandToTarget(commandSignal, targetSignal, intent);
            }
        }

        private void HandleStepCommand(Iec101Asdu asdu)
        {
            Iec101InformationObject command = GetFirstObject(asdu);
            if (command == null)
            {
                return;
            }

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
            {
                RejectCommand(asdu, "Step command rejected: binding not found.");
                return;
            }

            CommandIntent intent = string.Equals(command.ValueText, "RAISE", StringComparison.OrdinalIgnoreCase) ? CommandIntent.Raise : CommandIntent.Lower;
            bool isSelect = command.Select.HasValue && command.Select.Value;
            if (!ValidateAndTrackCommand(asdu, commandSignal, intent, isSelect, "Step command"))
            {
                return;
            }

            if (!isSelect)
            {
                ApplyCommandToTarget(commandSignal, targetSignal, intent);
            }
        }

        private void HandleNormalizedSetpointCommand(Iec101Asdu asdu)
        {
            Iec101InformationObject command = GetFirstObject(asdu);
            if (command == null)
            {
                return;
            }

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
            {
                RejectCommand(asdu, "Normalized setpoint rejected: binding not found.");
                return;
            }

            bool isSelect = command.Select.HasValue && command.Select.Value;
            if (!ValidateAndTrackCommand(asdu, commandSignal, CommandIntent.On, isSelect, "Normalized setpoint"))
            {
                return;
            }

            SignalDefinition updatedTarget = CloneSignal(targetSignal);
            updatedTarget.RuntimeValue = (command.NumericValue.HasValue ? command.NumericValue.Value : 0d).ToString("0.###", CultureInfo.InvariantCulture);
            updatedTarget.LiveCot = updatedTarget.ResolveBindingCot();

            lock (_sync)
            {
                _runtimeSignals[updatedTarget.Ioa] = CloneSignal(updatedTarget);
            }

            if (RuntimeSignalUpdated != null)
            {
                RuntimeSignalUpdated(updatedTarget.Ioa, updatedTarget.RuntimeValue, updatedTarget.LiveCot);
            }

            LogStatus("CMD", string.Format("Normalized setpoint on IOA {0} updated IOA {1} -> {2}.", commandSignal.Ioa, updatedTarget.Ioa, updatedTarget.RuntimeValue));

            SignalDefinition publishSignal = CloneSignal(updatedTarget);
            publishSignal.LiveCot = "CmdFb";
            EnqueueSignal(publishSignal, ResolveCot(publishSignal.LiveCot), true);
        }

        private bool ValidateAndTrackCommand(Iec101Asdu asdu, SignalDefinition commandSignal, CommandIntent intent, bool isSelect, string label)
        {
            switch (commandSignal.CommandOperateMode)
            {
                case CommandOperateMode.DirectOperate:
                    if (isSelect)
                    {
                        RejectCommand(asdu, label + " rejected: point is configured for DO only.");
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

                        AcknowledgeCommand(asdu, label + " SBO select accepted.");
                        return false;
                    }

                    lock (_sync)
                    {
                        CommandIntent selectedIntent;
                        if (!_selectedCommandIntents.TryGetValue(commandSignal.Ioa, out selectedIntent))
                        {
                            RejectCommand(asdu, label + " rejected: execute received without prior select.");
                            return false;
                        }

                        if (selectedIntent != intent)
                        {
                            _selectedCommandIntents.Remove(commandSignal.Ioa);
                            RejectCommand(asdu, label + " rejected: execute does not match selected operation.");
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

                        AcknowledgeCommand(asdu, label + " SBO select accepted.");
                        return false;
                    }

                    lock (_sync)
                    {
                        _selectedCommandIntents.Remove(commandSignal.Ioa);
                    }
                    break;
            }

            AcknowledgeCommand(asdu, label + " execute accepted.");
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
            {
                RuntimeSignalUpdated(updatedTarget.Ioa, updatedTarget.RuntimeValue, updatedTarget.LiveCot);
            }

            LogStatus("CMD", string.Format("Master command on IOA {0} updated IOA {1} -> {2}.", commandSignal.Ioa, updatedTarget.Ioa, updatedTarget.RuntimeValue));

            if (updatedTarget.SpontaneousEnabled || string.Equals(updatedTarget.LiveCot, "CmdFb", StringComparison.OrdinalIgnoreCase))
            {
                EnqueueSignal(updatedTarget, ResolveCot(updatedTarget.LiveCot), true);
            }
        }

        private bool AcknowledgeCommand(Iec101Asdu asdu, string message)
        {
            EnqueueCommandConfirmation(asdu, false);
            LogStatus("CMD", message);
            return true;
        }

        private bool RejectCommand(Iec101Asdu asdu, string message)
        {
            EnqueueCommandConfirmation(asdu, true);
            LogStatus("CMD", message);
            return true;
        }

        private void EnqueueCommandConfirmation(Iec101Asdu asdu, bool negative)
        {
            if (asdu == null)
            {
                return;
            }

            Iec101InformationObject obj = GetFirstObject(asdu);
            byte[] payload = obj == null ? new byte[0] : GetObjectPayload(obj);
            byte[] response = Iec101AsduCodec.EncodeInformationObjectAsdu(
                asdu.TypeId,
                Iec101CauseOfTransmission.ActivationCon,
                negative,
                asdu.CommonAddress,
                obj == null ? 0 : obj.ObjectAddress,
                payload,
                _profile);
            EnqueueAsdu(response, true);
        }

        private void SendQueuedAsduOrNoData(bool class1)
        {
            byte[] asdu = null;
            bool class1Pending = false;
            lock (_sync)
            {
                class1Pending = _class1Queue.Count > 0;
                if (!class1 && class1Pending)
                {
                    // Do not let cyclic Class 2/background traffic hide pending Class 1 data.
                    // Return no Class 2 data with ACD=1 so an unbalanced master immediately
                    // switches to FC10/Class 1 and drains GI/events/command confirmations.
                    asdu = null;
                }
                else
                {
                    Queue<byte[]> queue = class1 ? _class1Queue : _class2Queue;
                    if (queue.Count > 0)
                    {
                        asdu = queue.Dequeue();
                    }
                }
            }

            if (asdu == null)
            {
                SendFixedSecondary(9);
                return;
            }

            SendVariableSecondary(asdu);
        }

        private void EnqueueSignal(SignalDefinition signal, Iec101CauseOfTransmission cot, bool forceClass1)
        {
            if (_serialPort == null || !IsApplicationTrafficEnabled())
            {
                return;
            }

            byte[] asdu = CreateSingleSignalAsdu(signal, cot, null);
            if (asdu == null)
            {
                return;
            }

            EnqueueAsdu(asdu, forceClass1 || signal.SignalClass == SignalClass.Class1);
        }

        private void EnqueueAsdu(byte[] asdu, bool class1)
        {
            if (asdu == null || asdu.Length == 0)
            {
                return;
            }

            lock (_sync)
            {
                if (class1)
                {
                    _class1Queue.Enqueue(asdu);
                }
                else
                {
                    _class2Queue.Enqueue(asdu);
                }
            }
        }

        private byte[] CreateSingleSignalAsdu(SignalDefinition signal, Iec101CauseOfTransmission cot, DateTime? originalTimestampUtc)
        {
            if (signal == null)
            {
                return null;
            }

            Iec101TypeId typeId;
            byte[] payload;
            if (!TryBuildSignalPayload(signal, originalTimestampUtc, out typeId, out payload))
            {
                return null;
            }

            return Iec101AsduCodec.EncodeInformationObjectAsdu(typeId, cot, false, signal.Casdu, signal.Ioa, payload, _profile);
        }

        private bool TryBuildSignalPayload(SignalDefinition signal, DateTime? originalTimestampUtc, out Iec101TypeId typeId, out byte[] payload)
        {
            typeId = Iec101TypeId.Unknown;
            payload = null;

            DateTime timestampSource = originalTimestampUtc.HasValue
                ? (originalTimestampUtc.Value.Kind == DateTimeKind.Utc ? originalTimestampUtc.Value : originalTimestampUtc.Value.ToUniversalTime())
                : DateTime.UtcNow;
            byte qds = BuildQualityByte(signal.Quality);
            bool withTimestamp = signal.UseTimestamp;
            List<byte> bytes = new List<byte>();

            switch (signal.SignalType)
            {
                case SlaveSignalType.SinglePoint:
                    typeId = withTimestamp ? Iec101TypeId.M_SP_TB_1 : Iec101TypeId.M_SP_NA_1;
                    bytes.Add((byte)((ParseOnOff(signal.RuntimeValue) ? 0x01 : 0x00) | qds));
                    break;
                case SlaveSignalType.DoublePoint:
                    typeId = withTimestamp ? Iec101TypeId.M_DP_TB_1 : Iec101TypeId.M_DP_NA_1;
                    bytes.Add((byte)((ParseOnOff(signal.RuntimeValue) ? 0x02 : 0x01) | qds));
                    break;
                case SlaveSignalType.MeasuredNormalized:
                    typeId = withTimestamp ? Iec101TypeId.M_ME_TD_1 : Iec101TypeId.M_ME_NA_1;
                    short normalizedRaw = (short)Math.Round(Math.Max(-1d, Math.Min(1d, ParseDouble(signal.RuntimeValue, signal.AnalogFrom))) * 32767d, MidpointRounding.AwayFromZero);
                    bytes.Add((byte)(normalizedRaw & 0xFF));
                    bytes.Add((byte)((normalizedRaw >> 8) & 0xFF));
                    bytes.Add(qds);
                    break;
                case SlaveSignalType.MeasuredScaled:
                    typeId = withTimestamp ? Iec101TypeId.M_ME_TE_1 : Iec101TypeId.M_ME_NB_1;
                    short scaledRaw = (short)Math.Round(ParseDouble(signal.RuntimeValue, signal.AnalogFrom), MidpointRounding.AwayFromZero);
                    bytes.Add((byte)(scaledRaw & 0xFF));
                    bytes.Add((byte)((scaledRaw >> 8) & 0xFF));
                    bytes.Add(qds);
                    break;
                case SlaveSignalType.MeasuredShort:
                    typeId = withTimestamp ? Iec101TypeId.M_ME_TF_1 : Iec101TypeId.M_ME_NC_1;
                    byte[] shortValue = BitConverter.GetBytes((float)ParseDouble(signal.RuntimeValue, signal.AnalogFrom));
                    bytes.AddRange(shortValue);
                    bytes.Add(qds);
                    break;
                case SlaveSignalType.StepPosition:
                    typeId = withTimestamp ? Iec101TypeId.M_ST_TB_1 : Iec101TypeId.M_ST_NA_1;
                    int step = (int)Math.Round(ParseDouble(signal.RuntimeValue, signal.AnalogFrom), MidpointRounding.AwayFromZero);
                    bytes.Add((byte)(step & 0x7F));
                    bytes.Add(qds);
                    break;
                default:
                    return false;
            }

            if (withTimestamp)
            {
                bytes.AddRange(Iec101AsduCodec.EncodeCp56Time(timestampSource));
            }

            payload = bytes.ToArray();
            return true;
        }

        private void SendFixedSecondary(int functionCode)
        {
            byte control = BuildSecondaryControl(functionCode, HasClass1Data(), false);
            byte[] response = Iec101FrameCodec.EncodeFixed(control, _config.LinkAddress, _profile);
            WriteFrame(response);
        }

        private void SendVariableSecondary(byte[] asdu)
        {
            byte control = BuildSecondaryControl(8, HasClass1Data(), false);
            byte[] response = Iec101FrameCodec.EncodeVariable(control, _config.LinkAddress, asdu, _profile);
            WriteFrame(response);
        }

        private void WriteFrame(byte[] frame)
        {
            SerialPort port = _serialPort;
            if (port == null || !port.IsOpen || frame == null || frame.Length == 0)
            {
                return;
            }

            port.Write(frame, 0, frame.Length);
            LogRawTx(frame);
        }

        private bool HasClass1Data()
        {
            lock (_sync)
            {
                return _class1Queue.Count > 0;
            }
        }

        private static byte BuildSecondaryControl(int functionCode, bool acd, bool dfc)
        {
            int control = functionCode & 0x0F;
            if (acd)
            {
                control |= 0x20;
            }

            if (dfc)
            {
                control |= 0x10;
            }

            return (byte)control;
        }

        private void PublishBackgroundSignalsIfDue()
        {
            if (!IsApplicationTrafficEnabled())
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if ((now - _lastBackgroundPublishAt).TotalMilliseconds < _config.BackgroundPublishIntervalMs)
            {
                return;
            }

            _lastBackgroundPublishAt = now;

            List<SignalDefinition> snapshot;
            lock (_sync)
            {
                snapshot = _runtimeSignals.Values.Select(CloneSignal).ToList();
            }

            foreach (SignalDefinition signal in snapshot)
            {
                if (!signal.IsEnabled || !signal.BackgroundEnabled)
                {
                    continue;
                }

                if (!_config.EnableMeasurementStreaming && signal.IsMeasurement)
                {
                    continue;
                }

                EnqueueSignal(signal, Iec101CauseOfTransmission.BackgroundScan, false);
            }
        }

        private static Iec101InformationObject GetFirstObject(Iec101Asdu asdu)
        {
            return asdu != null && asdu.Objects.Count > 0 ? asdu.Objects[0] : null;
        }

        private static int GetFirstIoaOrZero(Iec101Asdu asdu)
        {
            Iec101InformationObject obj = GetFirstObject(asdu);
            return obj == null ? 0 : obj.ObjectAddress;
        }

        private byte[] GetFirstObjectPayloadOrDefault(Iec101Asdu asdu, byte[] fallback)
        {
            Iec101InformationObject obj = GetFirstObject(asdu);
            return obj == null ? fallback : GetObjectPayload(obj);
        }

        private byte[] GetObjectPayload(Iec101InformationObject obj)
        {
            if (obj == null || obj.RawBytes == null || obj.RawBytes.Length <= _profile.IoaLength)
            {
                return new byte[0];
            }

            byte[] payload = new byte[obj.RawBytes.Length - _profile.IoaLength];
            Buffer.BlockCopy(obj.RawBytes, _profile.IoaLength, payload, 0, payload.Length);
            return payload;
        }

        private void LogRawTx(byte[] frame)
        {
            LogLink("TX", ToHex(frame));
            LogStatus("RAW", string.Format("TX {0} bytes on {1}: {2}", frame.Length, _config != null ? _config.PortName : "-", DescribeFrame(frame, frame.Length)));
            if (LinkFrameObserved != null)
            {
                LinkFrameObserved(true, false);
            }
        }

        private void LogRawRx(byte[] frame)
        {
            LogLink("RX", ToHex(frame));
            LogStatus("RAW", string.Format("RX {0} bytes on {1}: {2}", frame.Length, _config != null ? _config.PortName : "-", DescribeFrame(frame, frame.Length)));
            if (LinkFrameObserved != null)
            {
                LinkFrameObserved(false, true);
            }
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

        private static Iec101CauseOfTransmission ResolveCot(string liveCot)
        {
            switch (liveCot)
            {
                case "Spont":
                    return Iec101CauseOfTransmission.Spontaneous;
                case "GI":
                    return Iec101CauseOfTransmission.InterrogatedByStation;
                case "CmdFb":
                    return Iec101CauseOfTransmission.ActivationCon;
                default:
                    return Iec101CauseOfTransmission.BackgroundScan;
            }
        }

        private static byte BuildQualityByte(string quality)
        {
            if (string.IsNullOrWhiteSpace(quality))
            {
                return 0x00;
            }

            string normalized = quality.Trim().ToUpperInvariant();
            if (normalized.Contains("INVALID")) return 0x80;
            if (normalized.Contains("OLD") || normalized.Contains("NONTOPICAL")) return 0x40;
            if (normalized.Contains("SUB")) return 0x20;
            if (normalized.Contains("BLOCK")) return 0x10;
            if (normalized.Contains("OVER")) return 0x01;
            return 0x00;
        }

        private static bool ParseOnOff(string value)
        {
            return string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "CLOSE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static double ParseDouble(string value, double fallback)
        {
            double parsed;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static SignalDefinition CloneSignal(SignalDefinition signal)
        {
            if (signal == null)
            {
                return null;
            }

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

        private static void ReadExact(SerialPort port, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int value = ReadByte(port);
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

        private static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            return BitConverter.ToString(data).Replace("-", " ");
        }

        private void LogStatus(string category, string message)
        {
            if (StatusLogged != null)
            {
                StatusLogged(category, message);
            }
        }

        private void LogLink(string direction, string raw)
        {
            if (LinkActivityLogged != null)
            {
                LinkActivityLogged(direction, raw);
            }
        }

        private bool IsApplicationTrafficEnabled()
        {
            return ApplicationTrafficEnabledProvider == null || ApplicationTrafficEnabledProvider();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

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
