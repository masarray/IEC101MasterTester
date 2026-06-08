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

        private const int ErrorToleranceCount = 5;
        private const int WorkerSleepMs = 10;
        private const int StandbyWorkerSleepMs = 250;
        private const int CommandFollowUpMs = 4000;
        private const int BusyLogIntervalMs = 1500;

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
        private bool _startupHandshakeComplete;
        private bool _hasAccessDemand;
        private bool _linkBusy;
        private bool _fcb;
        private long _lastPollAt;
        private long _lastBusyAt;
        private long _lastBusyLogAt;
        private long _lastGoodResponseAt;
        private long _lastRunAt;
        private long _commandFollowUpUntil;
        private long _lastCommandSentAt;
        private string _lastCommandSummary;
        private bool _commandFollowUpObserved;
        private int _consecutiveLinkErrors;
        private bool _faultRaised;
        private string _currentFlowClass;
        private bool? _lastLoggedAcd;
        private bool? _lastLoggedDfc;
        private string _lastLoggedFlowClass;
        private string _lastRxFrameAcd;
        private string _lastRxFrameClass;
        private bool _exclusiveApplicationSequenceActive;
        private long _lastApplicationAsduAt;
        private long _lastGiResponseAt;
        private long _lastGiTerminationAt;
        private int _giResponseObjectsObserved;

        public NativeIec101MasterService()
        {
            _settings = ConnectionSettings.CreateDefault();
            _lineMonitorFormatter = new LineMonitorFormatter();
            _mapper = new Iec101DataMapper();
            _currentFlowClass = "Class 2";
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
                lock (_syncRoot)
                {
                    _isConnecting = true;
                }

                ResetSessionState();

                SerialPort port = CreateSerialPort(settings);
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();

                CancellationTokenSource cancellation = new CancellationTokenSource();
                lock (_syncRoot)
                {
                    _serialPort = port;
                    _workerCancellation = cancellation;
                    _isConnected = true;
                    _isConnecting = false;
                    _startupHandshakeComplete = false;
                }

                Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromValues(settings.LinkAddressLength, settings.CasduLength, settings.IoaLength, settings.OriginatorAddress);
                ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.ResetRemoteLink(settings.LinkAddress, profile), settings, true);
                ResetFcbState();
                ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.RequestLinkStatus(settings.LinkAddress, profile), settings, true);

                Task worker = Task.Run(() => RunWorker(settings, cancellation.Token), cancellation.Token);
                lock (_syncRoot)
                {
                    _workerTask = worker;
                    _startupHandshakeComplete = true;
                    _lastPollAt = 0;
                    _lastRunAt = NowMs();
                }

                RaiseConnectionState(ConnectionStatusInfo.Connected);
                RaiseLine(CreateFlowRow("Info", "Native master ready", "Class 2", "NativeCleanRoom unbalanced engine is active."));

                if (!string.Equals(settings.LinkLayerMode, "Unbalanced", StringComparison.OrdinalIgnoreCase))
                {
                    RaiseLine(CreateFlowRow("Warning", "Native balanced mode not implemented", "-", "NativeCleanRoom currently behaves as an IEC-101 unbalanced master. Use Unbalanced for protocol-parity validation."));
                }

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

                if (settings.UseClockSyncOnConnect)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(Math.Max(0, settings.GiStartupDelayMs + 250)).ConfigureAwait(false);
                            await SendClockSyncAsync().ConfigureAwait(false);
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
                    _startupHandshakeComplete = false;
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
            bool wasConnected;

            lock (_syncRoot)
            {
                cancellation = _workerCancellation;
                worker = _workerTask;
                port = _serialPort;
                wasConnected = _isConnected || _serialPort != null || _workerTask != null;
                _workerCancellation = null;
                _workerTask = null;
                _serialPort = null;
                _isConnected = false;
                _isConnecting = false;
                _startupHandshakeComplete = false;
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

            if (wasConnected)
            {
                RaiseLine(_lineMonitorFormatter.CreateSystemRow("STATE", "Disconnected", "Native communication stopped cleanly."));
                RaiseConnectionState(ConnectionStatusInfo.Disconnected);
            }
        }

        public Task SendGeneralInterrogationAsync()
        {
            return DispatchGeneralInterrogationAsync();
        }

        private Task DispatchGeneralInterrogationAsync()
        {
            ConnectionSettings settings = GetSettingsSnapshot();
            bool canExecuteImmediately;
            lock (_syncRoot)
            {
                canExecuteImmediately = _isConnected
                    && _startupHandshakeComplete
                    && !_linkBusy
                    && settings.ChannelOperationMode == Iec101ChannelOperationMode.FullActive;
            }

            if (!canExecuteImmediately)
            {
                Enqueue(new PendingCommand { Kind = "GI" });
                RaiseLine(CreateFlowRow("Info", "Native GI queued", "Class 1", "Activation C_IC_NA_1 queued until the active link worker is ready."));
                return Task.CompletedTask;
            }

            return Task.Run(() => ExecuteGeneralInterrogationSequence("Operator/NUC GI"));
        }

        public Task<bool> SendLinkLayerTestFunctionAsync()
        {
            ConnectionSettings settings = GetSettingsSnapshot();
            if (settings.ChannelOperationMode != Iec101ChannelOperationMode.StandbySupervision)
            {
                RaiseLine(CreateFlowRow("Info", "Link test skipped", "-", "Link-layer test is intended for NUC standby-supervision channel."));
                return Task.FromResult(false);
            }

            Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromValues(settings.LinkAddressLength, settings.CasduLength, settings.IoaLength, settings.OriginatorAddress);
            return Task.Run(() => ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.TestLink(settings.LinkAddress, profile), settings, true));
        }

        public void NotifyActiveLinkSwitchover()
        {
            lock (_syncRoot)
            {
                _hasAccessDemand = false;
                _linkBusy = false;
                _lastPollAt = 0;
                _commandFollowUpUntil = 0;
                _commandFollowUpObserved = false;
                _lastBusyAt = 0;
            }

            ConnectionSettings settings = GetSettingsSnapshot();
            if (settings.ChannelOperationMode != Iec101ChannelOperationMode.FullActive)
            {
                return;
            }

            Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromValues(settings.LinkAddressLength, settings.CasduLength, settings.IoaLength, settings.OriginatorAddress);
            UpdateFlowClass("Class 2");
            bool fcb = GetFcb();
            if (ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.RequestClass2Data(settings.LinkAddress, fcb, profile), settings, true))
            {
                ToggleFcb();
                lock (_syncRoot)
                {
                    _lastPollAt = NowMs();
                }
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
            Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromValues(settings.LinkAddressLength, settings.CasduLength, settings.IoaLength, settings.OriginatorAddress);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!IsStartupHandshakeComplete())
                    {
                        SleepWorker(cancellationToken, WorkerSleepMs);
                        continue;
                    }

                    if (settings.ChannelOperationMode != Iec101ChannelOperationMode.FullActive)
                    {
                        SleepWorker(cancellationToken, StandbyWorkerSleepMs);
                        continue;
                    }

                    long now = NowMs();
                    if (IsExclusiveApplicationSequenceActive())
                    {
                        SleepWorker(cancellationToken, WorkerSleepMs);
                        continue;
                    }

                    if (TryExecutePendingCommand(settings, profile, now))
                    {
                        SleepWorker(cancellationToken, WorkerSleepMs);
                        continue;
                    }

                    bool shouldPoll;
                    bool requestClass1;
                    lock (_syncRoot)
                    {
                        bool commandFollowUpActive = now < _commandFollowUpUntil;
                        bool busyBackoffActive = _linkBusy && (now - _lastBusyAt) < Math.Max(10, settings.BusyBackoffMs);
                        if (!busyBackoffActive)
                        {
                            _linkBusy = false;
                        }

                        requestClass1 = _hasAccessDemand || commandFollowUpActive;
                        int interval = requestClass1 ? settings.Class1PollIntervalMs : settings.PollIntervalMs;
                        shouldPoll = !busyBackoffActive && now - _lastPollAt >= Math.Max(10, interval);
                    }

                    if (shouldPoll)
                    {
                        UpdateFlowClass(requestClass1 ? "Class 1" : "Class 2");
                        bool fcb = GetFcb();
                        byte[] poll = requestClass1
                            ? Iec101PrimaryLinkFrameFactory.RequestClass1Data(settings.LinkAddress, fcb, profile)
                            : Iec101PrimaryLinkFrameFactory.RequestClass2Data(settings.LinkAddress, fcb, profile);

                        bool ok = ExecuteLinkExchange(poll, settings, true);
                        long completedAt = NowMs();
                        if (ok)
                        {
                            ToggleFcb();
                            lock (_syncRoot)
                            {
                                _lastPollAt = completedAt;
                                _lastRunAt = completedAt;
                            }
                        }
                        else
                        {
                            RegisterTransientLinkError(completedAt, settings);
                        }
                    }

                    RaiseCommandFollowUpTimeoutIfNeeded(now);
                }
                catch (Exception ex)
                {
                    RaiseLine(CreateErrorRow("Native worker error", ex.Message));
                    RegisterTransientLinkError(NowMs(), settings);
                }

                SleepWorker(cancellationToken, WorkerSleepMs);
            }
        }

        private void ExecuteGeneralInterrogationSequence(string reason)
        {
            ConnectionSettings settings = GetSettingsSnapshot();
            if (settings.ChannelOperationMode != Iec101ChannelOperationMode.FullActive)
            {
                RaiseLine(CreateFlowRow("Info", "GI skipped", "Class 1", "General interrogation is only executed on the active NUC/application channel."));
                return;
            }

            lock (_syncRoot)
            {
                if (!_isConnected || !_startupHandshakeComplete)
                {
                    Enqueue(new PendingCommand { Kind = "GI" });
                    RaiseLine(CreateFlowRow("Info", "Native GI queued", "Class 1", "Active link is not ready yet; GI will run from the worker."));
                    return;
                }

                if (_exclusiveApplicationSequenceActive)
                {
                    Enqueue(new PendingCommand { Kind = "GI" });
                    RaiseLine(CreateFlowRow("Info", "Native GI coalesced", "Class 1", "Another application sequence is active; GI was retained as pending work."));
                    return;
                }

                _exclusiveApplicationSequenceActive = true;
                _hasAccessDemand = true;
                _linkBusy = false;
                _commandFollowUpObserved = false;
                _lastCommandSentAt = NowMs();
                _commandFollowUpUntil = _lastCommandSentAt + CommandFollowUpMs;
                _lastCommandSummary = "General interrogation";
                _lastPollAt = 0;
                _lastGiResponseAt = 0;
                _lastGiTerminationAt = 0;
                _giResponseObjectsObserved = 0;
            }

            try
            {
                Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromValues(settings.LinkAddressLength, settings.CasduLength, settings.IoaLength, settings.OriginatorAddress);
                byte[] asdu = Iec101AsduCodec.EncodeInterrogationCommand(settings.CasduAddress, 0, 20, profile);
                RaiseLine(CreateFlowRow("Info", "Native GI dispatch", "Class 1", string.Format("C_IC_NA_1 activation sent through active link. Reason: {0}", string.IsNullOrWhiteSpace(reason) ? "-" : reason)));

                bool fcb = GetFcb();
                bool ok = ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.SendUserDataConfirmed(settings.LinkAddress, fcb, asdu, profile), settings, true);
                if (ok)
                {
                    ToggleFcb();
                    RaiseLine(CreateFlowRow("Info", "GI link ACK received", "Class 1", "GI activation was acknowledged at link layer. Draining Class 1 until GI data or termination is observed."));
                }
                else
                {
                    RaiseLine(CreateFlowRow("Warning", "GI activation not confirmed", "Class 1", "No valid link-layer response to C_IC_NA_1. Class 1 drain will still be attempted briefly."));
                    RegisterTransientLinkError(NowMs(), settings);
                }

                DrainClass1AfterGi(settings, profile);
            }
            finally
            {
                lock (_syncRoot)
                {
                    _exclusiveApplicationSequenceActive = false;
                    _lastPollAt = NowMs();
                }
            }
        }

        private void DrainClass1AfterGi(ConnectionSettings settings, Iec101ApplicationProfile profile)
        {
            long startedAt = NowMs();
            long deadline = startedAt + Math.Max(2500, settings.ResponseTimeoutMs * 4);
            int maxPolls = 96;
            int consecutiveNoApplicationAsdu = 0;
            bool observedGiData = false;
            bool observedTermination = false;

            UpdateFlowClass("Class 1");
            for (int i = 0; i < maxPolls && NowMs() < deadline; i++)
            {
                long beforeApplicationAsdu;
                long beforeGiResponse;
                long beforeGiTermination;
                int beforeGiObjects;
                lock (_syncRoot)
                {
                    beforeApplicationAsdu = _lastApplicationAsduAt;
                    beforeGiResponse = _lastGiResponseAt;
                    beforeGiTermination = _lastGiTerminationAt;
                    beforeGiObjects = _giResponseObjectsObserved;
                }

                bool fcb = GetFcb();
                bool ok = ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.RequestClass1Data(settings.LinkAddress, fcb, profile), settings, true);
                if (ok)
                {
                    ToggleFcb();
                }
                else
                {
                    RegisterTransientLinkError(NowMs(), settings);
                    consecutiveNoApplicationAsdu++;
                    Thread.Sleep(Math.Max(20, settings.Class1PollIntervalMs));
                    continue;
                }

                long afterApplicationAsdu;
                long afterGiResponse;
                long afterGiTermination;
                int afterGiObjects;
                lock (_syncRoot)
                {
                    afterApplicationAsdu = _lastApplicationAsduAt;
                    afterGiResponse = _lastGiResponseAt;
                    afterGiTermination = _lastGiTerminationAt;
                    afterGiObjects = _giResponseObjectsObserved;
                }

                bool gotAnyApplicationAsdu = afterApplicationAsdu > beforeApplicationAsdu;
                bool gotGiResponse = afterGiResponse > beforeGiResponse || afterGiObjects > beforeGiObjects;
                bool gotGiTermination = afterGiTermination > beforeGiTermination;

                if (gotGiResponse)
                {
                    observedGiData = true;
                    consecutiveNoApplicationAsdu = 0;
                }
                else if (gotAnyApplicationAsdu)
                {
                    consecutiveNoApplicationAsdu = 0;
                }
                else
                {
                    consecutiveNoApplicationAsdu++;
                }

                if (gotGiTermination)
                {
                    observedTermination = true;
                }

                if (observedTermination && consecutiveNoApplicationAsdu >= 1)
                {
                    break;
                }

                if (observedGiData && consecutiveNoApplicationAsdu >= 2)
                {
                    break;
                }

                Thread.Sleep(Math.Max(20, Math.Min(100, settings.Class1PollIntervalMs)));
            }

            lock (_syncRoot)
            {
                _hasAccessDemand = false;
                _commandFollowUpObserved = observedGiData || observedTermination;
            }

            RaiseLine(CreateFlowRow(
                observedGiData || observedTermination ? "Info" : "Warning",
                observedGiData || observedTermination ? "GI bootstrap drain completed" : "GI bootstrap drain incomplete",
                "Class 1",
                string.Format(
                    "GI drain result: data={0}, termination={1}, objects={2}. Normal Class 1/Class 2 polling resumes.",
                    observedGiData ? "yes" : "no",
                    observedTermination ? "yes" : "no",
                    _giResponseObjectsObserved)));
        }

        private bool TryExecutePendingCommand(ConnectionSettings settings, Iec101ApplicationProfile profile, long now)
        {
            PendingCommand command;
            lock (_syncRoot)
            {
                if (_pendingCommand == null)
                {
                    return false;
                }

                if (_linkBusy)
                {
                    return false;
                }

                command = _pendingCommand;
                _pendingCommand = null;
            }

            if (string.Equals(command.Kind, "GI", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteGeneralInterrogationSequence("Queued GI");
                return true;
            }

            return ExecutePendingCommandCore(command, settings, profile, now);
        }

        private bool ExecutePendingCommandCore(PendingCommand command, ConnectionSettings settings, Iec101ApplicationProfile profile, long now)
        {
            byte[] asdu = EncodePendingCommand(command, settings, profile);
            if (asdu == null)
            {
                return false;
            }

            string summary = BuildCommandSummary(command);
            bool fcb = GetFcb();
            bool ok = ExecuteLinkExchange(Iec101PrimaryLinkFrameFactory.SendUserDataConfirmed(settings.LinkAddress, fcb, asdu, profile), settings, true);

            lock (_syncRoot)
            {
                _lastCommandSentAt = now;
                _commandFollowUpUntil = now + CommandFollowUpMs;
                _commandFollowUpObserved = false;
                _lastCommandSummary = summary;
                _lastPollAt = 0;
                if (string.Equals(command.Kind, "GI", StringComparison.OrdinalIgnoreCase))
                {
                    _hasAccessDemand = true;
                }
            }

            if (ok)
            {
                ToggleFcb();
                RaiseLine(CreateFlowRow("Info", summary + " sent", "Class 1", "Awaiting command/application follow-up via Class 1 polling."));
            }
            else
            {
                RaiseLine(CreateFlowRow("Warning", summary + " TX not confirmed", "Class 1", "No valid link-layer response was observed. FCB was not toggled; command follow-up polling is still armed."));
                RegisterTransientLinkError(now, settings);
            }

            return true;
        }

        private byte[] EncodePendingCommand(PendingCommand command, ConnectionSettings settings, Iec101ApplicationProfile profile)
        {
            switch (command.Kind)
            {
                case "GI":
                    RaiseLine(CreateFlowRow("Info", "Native GI queued", "Class 2", "Activation C_IC_NA_1"));
                    return Iec101AsduCodec.EncodeInterrogationCommand(settings.CasduAddress, 0, 20, profile);
                case "ClockSync":
                    RaiseLine(CreateFlowRow("Info", "Native clock sync queued", "Class 2", "Activation C_CS_NA_1"));
                    return Iec101AsduCodec.EncodeClockSyncCommand(settings.CasduAddress, DateTime.Now, profile);
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

            if (port == null || !port.IsOpen || request == null || request.Length == 0)
            {
                return false;
            }

            lock (_serialLock)
            {
                try
                {
                    port.Write(request, 0, request.Length);
                    ProtocolEvidenceRecorder.Shared.RecordRaw("NativeCleanRoom", "TX", request, request.Length, settings);
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

                    return ProcessReceivedFrame(response, settings);
                }
                catch (Exception ex)
                {
                    RaiseLine(CreateErrorRow("Native exchange failed", ex.Message));
                    return false;
                }
            }
        }

        private bool ProcessReceivedFrame(byte[] frameBytes, ConnectionSettings settings)
        {
            ProtocolEvidenceRecorder.Shared.RecordRaw("NativeCleanRoom", "RX", frameBytes, frameBytes.Length, settings);
            RaiseLine(_lineMonitorFormatter.FromRawMessage("RX", frameBytes, frameBytes.Length, settings));

            Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromValues(settings.LinkAddressLength, settings.CasduLength, settings.IoaLength, settings.OriginatorAddress);
            Iec101Frame frame;
            string frameError;
            if (!Iec101FrameCodec.TryParse(frameBytes, frameBytes.Length, profile, out frame, out frameError))
            {
                RaiseLine(CreateFlowRow("Warning", "Native frame parse failed", "-", frameError));
                return false;
            }

            if (frame.Control != null && !frame.Control.IsPrimary)
            {
                UpdateSecondaryLinkState(frame.Control);
            }

            byte[] asduBytes = frame.GetAsduBytesOrEmpty();
            if (asduBytes.Length == 0)
            {
                return true;
            }

            Iec101Asdu asdu;
            string asduError;
            if (!Iec101AsduCodec.TryParse(asduBytes, profile, out asdu, out asduError))
            {
                RaiseLine(CreateFlowRow("Warning", "Native ASDU parse failed", "-", asduError));
                return true;
            }

            UpdateApplicationState(asdu);
            RegisterApplicationAsdu(asdu);

            if (!ShouldMapToValueViewer(asdu))
            {
                return true;
            }

            foreach (Iec101InformationObject obj in asdu.Objects)
            {
                ValueViewerRow row = _mapper.Map(asdu, obj);
                if (row != null)
                {
                    row.Acd = GetLastAcdText(frame);
                    row.TrafficClass = GetTrafficClass(asdu, frame);
                    row.DeliveryContext = GetDeliveryContext(asdu);
                    RaiseValue(row);
                }
            }

            return true;
        }

        private void UpdateSecondaryLinkState(Iec101ControlField control)
        {
            long now = NowMs();
            bool acd = control.Acd;
            bool dfc = control.Dfc;
            bool logAcd = false;
            bool logDfc = false;

            lock (_syncRoot)
            {
                _lastGoodResponseAt = now;
                _consecutiveLinkErrors = 0;
                _faultRaised = false;
                _hasAccessDemand = acd;
                _linkBusy = dfc;
                _lastRxFrameAcd = acd ? "1" : "0";
                _lastRxFrameClass = string.IsNullOrWhiteSpace(_currentFlowClass) ? "Unknown" : _currentFlowClass;
                if (acd)
                {
                    _lastPollAt = 0;
                }

                if (_lastLoggedAcd == null || _lastLoggedAcd.Value != acd)
                {
                    _lastLoggedAcd = acd;
                    logAcd = true;
                }

                if (_lastLoggedDfc == null || _lastLoggedDfc.Value != dfc)
                {
                    _lastLoggedDfc = dfc;
                    logDfc = true;
                }
            }

            if (logAcd)
            {
                RaiseLine(CreateFlowRow("Info", acd ? "ACD asserted" : "ACD cleared", "-", acd ? "Slave reported ACD=1; Class 1 polling will be prioritized." : "Slave reported ACD=0."));
            }

            if (dfc)
            {
                MarkBusy(now, "Slave set DFC=1.");
            }
            else if (logDfc)
            {
                RaiseLine(CreateFlowRow("Info", "DFC cleared", "-", "Slave reported DFC=0."));
            }
        }

        private void UpdateApplicationState(Iec101Asdu asdu)
        {
            if (asdu == null)
            {
                return;
            }

            if (IsCommandAsdu(asdu.TypeId))
            {
                lock (_syncRoot)
                {
                    if (_lastCommandSentAt > 0 && !_commandFollowUpObserved)
                    {
                        _commandFollowUpObserved = true;
                    }
                }
            }
        }

        private void RegisterApplicationAsdu(Iec101Asdu asdu)
        {
            if (asdu == null)
            {
                return;
            }

            long now = NowMs();
            lock (_syncRoot)
            {
                _lastApplicationAsduAt = now;
                if (asdu.Cause == Iec101CauseOfTransmission.InterrogatedByStation)
                {
                    _lastGiResponseAt = now;
                    _giResponseObjectsObserved += asdu.Objects == null ? 0 : Math.Max(1, asdu.Objects.Count);
                    _commandFollowUpObserved = true;
                }

                if (asdu.TypeId == Iec101TypeId.C_IC_NA_1
                    && asdu.Cause == Iec101CauseOfTransmission.ActivationTermination)
                {
                    _lastGiTerminationAt = now;
                    _commandFollowUpObserved = true;
                }
            }
        }

        private static bool ShouldMapToValueViewer(Iec101Asdu asdu)
        {
            return asdu != null && !IsCommandAsdu(asdu.TypeId);
        }

        private static bool IsCommandAsdu(Iec101TypeId typeId)
        {
            switch (typeId)
            {
                case Iec101TypeId.C_IC_NA_1:
                case Iec101TypeId.C_CS_NA_1:
                case Iec101TypeId.C_SC_NA_1:
                case Iec101TypeId.C_DC_NA_1:
                case Iec101TypeId.C_RC_NA_1:
                case Iec101TypeId.C_SE_NA_1:
                    return true;
                default:
                    return false;
            }
        }

        private string GetTrafficClass(Iec101Asdu asdu, Iec101Frame frame)
        {
            if (asdu == null)
            {
                return string.IsNullOrWhiteSpace(_lastRxFrameClass) ? "Unknown" : _lastRxFrameClass;
            }

            switch (asdu.Cause)
            {
                case Iec101CauseOfTransmission.InterrogatedByStation:
                case Iec101CauseOfTransmission.BackgroundScan:
                case Iec101CauseOfTransmission.Periodic:
                    return "Class 2";
            }

            if (IsCommandAsdu(asdu.TypeId))
            {
                return "Class 1";
            }

            if (!string.IsNullOrWhiteSpace(_lastRxFrameClass))
            {
                return _lastRxFrameClass;
            }

            if (frame != null && frame.Control != null && !frame.Control.IsPrimary && frame.Control.Acd)
            {
                return "Class 1";
            }

            return string.IsNullOrWhiteSpace(_currentFlowClass) ? "Unknown" : _currentFlowClass;
        }

        private string GetDeliveryContext(Iec101Asdu asdu)
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
                    return "GI Response";
                case Iec101CauseOfTransmission.BackgroundScan:
                case Iec101CauseOfTransmission.Periodic:
                    return "Response to FC11";
            }

            string rxClass = string.IsNullOrWhiteSpace(_lastRxFrameClass) ? _currentFlowClass : _lastRxFrameClass;
            if (string.Equals(rxClass, "Class 1", StringComparison.OrdinalIgnoreCase))
            {
                return "Response to FC10";
            }

            if (string.Equals(rxClass, "Class 2", StringComparison.OrdinalIgnoreCase))
            {
                return "Response to FC11";
            }

            return "Unknown";
        }

        private static string GetLastAcdText(Iec101Frame frame)
        {
            return frame != null && frame.Control != null && !frame.Control.IsPrimary ? (frame.Control.Acd ? "1" : "0") : "-";
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
            if (command == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                _pendingCommand = command;
                _lastPollAt = 0;
            }
        }

        private bool GetFcb()
        {
            lock (_syncRoot)
            {
                return _fcb;
            }
        }

        private void ToggleFcb()
        {
            lock (_syncRoot)
            {
                _fcb = !_fcb;
            }
        }

        private void ResetFcbState()
        {
            lock (_syncRoot)
            {
                _fcb = false;
            }
        }

        private ConnectionSettings GetSettingsSnapshot()
        {
            lock (_syncRoot)
            {
                return _settings.Clone();
            }
        }

        private bool IsStartupHandshakeComplete()
        {
            lock (_syncRoot)
            {
                return _startupHandshakeComplete;
            }
        }

        private bool IsExclusiveApplicationSequenceActive()
        {
            lock (_syncRoot)
            {
                return _exclusiveApplicationSequenceActive;
            }
        }

        private void UpdateFlowClass(string nextClass)
        {
            if (string.IsNullOrWhiteSpace(nextClass))
            {
                return;
            }

            bool shouldLog = false;
            lock (_syncRoot)
            {
                _currentFlowClass = nextClass;
                if (!string.Equals(_lastLoggedFlowClass, nextClass, StringComparison.OrdinalIgnoreCase))
                {
                    _lastLoggedFlowClass = nextClass;
                    shouldLog = true;
                }
            }

            if (shouldLog)
            {
                RaiseLine(CreateFlowRow("Info", "Polling switched", nextClass, "System is now serving " + nextClass + " traffic."));
            }
        }

        private void RegisterTransientLinkError(long now, ConnectionSettings settings)
        {
            bool shouldFault = false;
            lock (_syncRoot)
            {
                _consecutiveLinkErrors++;
                long timeoutWindow = Math.Max(1500, settings.ResponseTimeoutMs * 2);
                bool silentTooLong = _lastGoodResponseAt > 0 && now - _lastGoodResponseAt >= timeoutWindow;
                shouldFault = !_faultRaised && _consecutiveLinkErrors >= ErrorToleranceCount && silentTooLong;
                if (shouldFault)
                {
                    _faultRaised = true;
                }
            }

            if (shouldFault)
            {
                RaiseConnectionState(ConnectionStatusInfo.Faulted);
                RaiseLine(CreateErrorRow("Native link fault", "No valid response observed after repeated IEC-101 link-layer exchanges."));
            }
        }

        private void MarkBusy(long now, string detail)
        {
            bool shouldLog = false;
            lock (_syncRoot)
            {
                _linkBusy = true;
                _lastBusyAt = now;
                if (now - _lastBusyLogAt >= BusyLogIntervalMs)
                {
                    _lastBusyLogAt = now;
                    shouldLog = true;
                }
            }

            if (shouldLog)
            {
                RaiseLine(CreateFlowRow("Warning", "Link layer busy", string.IsNullOrWhiteSpace(_currentFlowClass) ? "-" : _currentFlowClass, detail ?? "Link layer is busy."));
            }
        }

        private void RaiseCommandFollowUpTimeoutIfNeeded(long now)
        {
            bool shouldRaise = false;
            string detail = null;
            lock (_syncRoot)
            {
                if (_lastCommandSentAt > 0 && !_commandFollowUpObserved && now > _commandFollowUpUntil)
                {
                    shouldRaise = true;
                    _commandFollowUpObserved = true;
                    detail = _lastCommandSummary;
                }
            }

            if (shouldRaise)
            {
                RaiseLine(CreateFlowRow("Warning", "Command follow-up timeout", "Class 1", detail ?? "No command follow-up observed."));
            }
        }

        private void ResetSessionState()
        {
            lock (_syncRoot)
            {
                _pendingCommand = null;
                _startupHandshakeComplete = false;
                _hasAccessDemand = false;
                _linkBusy = false;
                _fcb = false;
                _lastPollAt = 0;
                _lastBusyAt = 0;
                _lastBusyLogAt = 0;
                _lastGoodResponseAt = 0;
                _lastRunAt = 0;
                _commandFollowUpUntil = 0;
                _lastCommandSentAt = 0;
                _lastCommandSummary = null;
                _commandFollowUpObserved = false;
                _consecutiveLinkErrors = 0;
                _faultRaised = false;
                _currentFlowClass = "Class 2";
                _lastLoggedAcd = null;
                _lastLoggedDfc = null;
                _lastLoggedFlowClass = null;
                _lastRxFrameAcd = null;
                _lastRxFrameClass = null;
                _exclusiveApplicationSequenceActive = false;
                _lastApplicationAsduAt = 0;
                _lastGiResponseAt = 0;
                _lastGiTerminationAt = 0;
                _giResponseObjectsObserved = 0;
            }
        }

        private static string BuildCommandSummary(PendingCommand command)
        {
            if (command == null)
            {
                return "Command";
            }

            switch (command.Kind)
            {
                case "GI":
                    return "General interrogation";
                case "ClockSync":
                    return "Clock sync";
                case "Single":
                    return string.Format("Single command IOA={0} {1} Select={2}", command.Ioa, command.State ? "ON" : "OFF", command.Select ? 1 : 0);
                case "Double":
                    return string.Format("Double command IOA={0} {1} Select={2}", command.Ioa, command.State ? "CLOSE" : "OPEN", command.Select ? 1 : 0);
                case "Step":
                    return string.Format("Step command IOA={0} {1} Select={2}", command.Ioa, command.State ? "RAISE" : "LOWER", command.Select ? 1 : 0);
                case "SetpointNormalized":
                    return string.Format("Setpoint normalized IOA={0} Value={1:0.###} Select={2}", command.Ioa, command.NormalizedValue, command.Select ? 1 : 0);
                default:
                    return command.Kind ?? "Command";
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
                ReadTimeout = Math.Max(250, settings.ResponseTimeoutMs),
                WriteTimeout = Math.Max(250, settings.ResponseTimeoutMs),
                DtrEnable = false,
                RtsEnable = false
            };
        }

        private static void SleepWorker(CancellationToken cancellationToken, int milliseconds)
        {
            try
            {
                Thread.Sleep(Math.Max(1, milliseconds));
            }
            catch
            {
            }
        }

        private static long NowMs()
        {
            return (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
        }

        private static LineMonitorRow CreateErrorRow(string summary, string detail)
        {
            return new LineMonitorRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = "ERR",
                FrameType = "Error",
                Summary = summary,
                ControlFc = "-",
                ACD = "-",
                DFC = "-",
                AsduType = "-",
                COT = "-",
                CASDU = "-",
                IOA = "-",
                RawHex = detail ?? string.Empty,
                Detail = detail ?? string.Empty,
                DataClass = "-"
            };
        }

        private static LineMonitorRow CreateFlowRow(string level, string summary, string dataClass, string detail)
        {
            return new LineMonitorRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = "STATE",
                FrameType = level,
                Summary = summary,
                ControlFc = "-",
                ACD = "-",
                DFC = "-",
                AsduType = "-",
                COT = "-",
                CASDU = "-",
                IOA = "-",
                RawHex = detail ?? string.Empty,
                Detail = detail ?? string.Empty,
                DataClass = dataClass
            };
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
