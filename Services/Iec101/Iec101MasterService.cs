using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using IEC101MasterTester.Models;
using lib60870;
using lib60870.CS101;
using lib60870.linklayer;

namespace IEC101MasterTester.Services.Iec101
{
    public sealed class Iec101MasterService : IIec101MasterService
    {
        private sealed class PendingCommandRequest
        {
            public string Kind { get; set; }
            public int Ioa { get; set; }
            public bool State { get; set; }
            public float NormalizedValue { get; set; }
            public bool Select { get; set; }
            public int Quality { get; set; }
            public long EnqueuedAt { get; set; }
        }

        private const int ErrorToleranceCount = 5;
        private const int BusyLogIntervalMs = 1500;
        private const int WorkerSleepMs = 10;
        private const int StandbyWorkerSleepMs = 250;
        private const int CommandFollowUpWindowMs = 4000;

        private readonly object _syncRoot = new object();
        private readonly object _masterOperationLock = new object();
        private readonly Iec101DataMapper _mapper;
        private readonly LineMonitorFormatter _lineMonitorFormatter;
        private PendingCommandRequest _pendingCommand;

        private ConnectionSettings _settings;
        private SerialPort _serialPort;
        private CS101Master _master;
        private CancellationTokenSource _workerCancellationSource;
        private Task _workerTask;
        private bool _isConnected;
        private bool _isConnecting;
        private bool _isDisconnecting;
        private bool _hasAccessDemand;
        private bool _linkBusy;
        private bool _linkAvailable;
        private bool _generalInterrogationSent;
        private bool _generalInterrogationInProgress;
        private bool _autoGeneralInterrogationArmed;
        private bool _autoGeneralInterrogationAttempted;
        private long _lastBusyAt;
        private long _lastBusyLogAt;
        private long _lastGoodResponseAt;
        private long _lastRunAt;
        private long _lastPollAt;
        private int _consecutiveLinkErrors;
        private bool _faultRaised;
        private string _currentFlowClass;
        private bool? _lastLoggedAcd;
        private string _lastLoggedFlowClass;
        private string _lastRxFrameAcd;
        private string _lastRxFrameClass;
        private int _giAttemptCounter;
        private int _manualGiRequestCount;
        private int _txGiAsduCount;
        private CancellationTokenSource _startupGiCancellationSource;
        private long _commandFollowUpUntil;

        private long _lastCommandSentAt;
        private string _lastCommandSummary;
        private bool _commandFollowUpObserved;

        public Iec101MasterService()
        {
            _mapper = new Iec101DataMapper();
            _lineMonitorFormatter = new LineMonitorFormatter();
            _settings = ConnectionSettings.CreateDefault();
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
            RaiseLine(_lineMonitorFormatter.CreateSystemRow("STATE", "Connect requested", settings.SerialSummary));

            try
            {
                await DisconnectInternalAsync(false).ConfigureAwait(false);
                ResetSessionState();

                SerialPort serialPort = CreateSerialPort(settings);
                serialPort.Open();
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                LinkLayerParameters linkLayerParameters = CreateLinkLayerParameters(settings);
                ApplicationLayerParameters applicationLayerParameters = CreateApplicationLayerParameters(settings);

                CS101Master master = new CS101Master(serialPort, ParseLinkLayerMode(settings.LinkLayerMode), linkLayerParameters, applicationLayerParameters)
                {
                    OwnAddress = settings.OriginatorAddress,
                    SlaveAddress = settings.LinkAddress,
                    DIR = false,
                    DebugOutput = false
                };

                master.SetReceivedRawMessageHandler(OnRawMessageReceived, null);
                master.SetSentRawMessageHandler(OnRawMessageSent, null);
                master.SetASDUReceivedHandler(OnAsduReceived, null);
                master.SetLinkLayerStateChangedHandler(OnLinkLayerStateChanged, null);
                int transceiverMessageTimeout = Math.Max(10, Math.Min(settings.RunLoopDelayMs, settings.PollIntervalMs));
                int transceiverCharacterTimeout = Math.Max(10, Math.Min(transceiverMessageTimeout, settings.Class1PollIntervalMs));
                master.SetTimeouts(transceiverMessageTimeout, transceiverCharacterTimeout);
                master.AddSlave(settings.LinkAddress);

                CancellationTokenSource workerCancellation = new CancellationTokenSource();
                CancellationTokenSource startupGiCancellation = new CancellationTokenSource();
                Task workerTask = RunWorkerAsync(master, settings, workerCancellation.Token);

                lock (_syncRoot)
                {
                    _serialPort = serialPort;
                    _master = master;
                    _workerCancellationSource = workerCancellation;
                    _workerTask = workerTask;
                    _startupGiCancellationSource = startupGiCancellation;
                    _isConnected = true;
                    _autoGeneralInterrogationArmed = settings.UseGeneralInterrogationOnConnect;
                    _autoGeneralInterrogationAttempted = false;
                }
                if (settings.UseGeneralInterrogationOnConnect)
                {
                    _ = TriggerStartupGiAsync(settings, startupGiCancellation.Token);
                }
                TraceGi("Connect completed; session armed for auto GI.");
                if (settings.UseGeneralInterrogationOnConnect)
                {
                    RaiseLine(CreateFlowRow("Info", "Auto GI armed", "Class 2", string.Format("Waiting link ready and startup delay {0} ms.", settings.GiStartupDelayMs)));
                }
                else if (settings.ChannelOperationMode == Iec101ChannelOperationMode.StandbySupervision)
                {
                    RaiseLine(CreateFlowRow("Info", "Standby supervision", "-", "NUC standby channel connected without Class 1/Class 2 polling."));
                }

                RaiseConnectionState(ConnectionStatusInfo.Connected);
                RaiseLine(CreateFlowRow("Info", "Flow ready", "Class 2", string.Format(
                    "Run={0}ms, PollC1={1}ms, PollC2={2}ms, BusyBackoff={3}ms, GI={4}ms",
                    settings.RunLoopDelayMs,
                    settings.Class1PollIntervalMs,
                    settings.PollIntervalMs,
                    settings.BusyBackoffMs,
                    settings.GiStartupDelayMs)));
                RaiseLine(_lineMonitorFormatter.CreateSystemRow("STATE", "Master ready", string.Format("LinkAddress={0}, CA={1}, DIR=0", settings.LinkAddress, settings.CasduAddress)));
            }
            catch (Exception ex)
            {
                await DisconnectInternalAsync(false).ConfigureAwait(false);
                RaiseLine(CreateErrorRow("Connect failed", ex.Message));
                RaiseConnectionState(ConnectionStatusInfo.Faulted);
                throw;
            }
            finally
            {
                lock (_syncRoot)
                {
                    _isConnecting = false;
                }
            }
        }

        public Task DisconnectAsync()
        {
            return DisconnectInternalAsync(true);
        }

        public Task SendGeneralInterrogationAsync()
        {
            return Task.Run(() =>
            {
                CS101Master master;
                ConnectionSettings settings;

                lock (_syncRoot)
                {
                    master = _master;
                    settings = _settings.Clone();
                }

                if (master == null)
                {
                    return;
                }

                if (settings.ChannelOperationMode != Iec101ChannelOperationMode.FullActive)
                {
                    RaiseLine(CreateFlowRow("Info", "GI skipped", "-", "General interrogation is disabled on standby supervision channel."));
                    return;
                }

                try
                {
                    int manualRequestCount;
                    lock (_syncRoot)
                    {
                        _manualGiRequestCount++;
                        manualRequestCount = _manualGiRequestCount;
                    }

                    lock (_masterOperationLock)
                    {
                        master.SlaveAddress = settings.LinkAddress;
                        master.SendInterrogationCommand(CauseOfTransmission.ACTIVATION, settings.CasduAddress, 20);
                        master.Run();
                    }

                    _generalInterrogationSent = true;
                    _generalInterrogationInProgress = true;
                    _currentFlowClass = "Class 2";
                    TraceGi(string.Format("Manual GI sent. manualRequestCount={0}", manualRequestCount));
                    RaiseLine(CreateFlowRow("Info", "GI command sent", "Class 2", string.Format("LinkAddress={0}, CA={1}", settings.LinkAddress, settings.CasduAddress)));
                }
                catch (LinkLayerBusyException)
                {
                    TraceGi("Manual GI delayed due to link busy.");
                    MarkBusy(SystemUtils.currentTimeMillis(), "GI delayed because the link layer is busy.");
                }
                catch (Exception ex)
                {
                    TraceGi("Manual GI failed: " + ex.Message);
                    RaiseLine(CreateErrorRow("GI failed", ex.Message));
                }
            });
        }

        private async Task TriggerStartupGiAsync(ConnectionSettings settings, CancellationToken cancellationToken)
        {
            int busyRetryCount = 0;
            try
            {
                await Task.Delay(Math.Max(0, settings.GiStartupDelayMs), cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                CS101Master master;
                bool linkBusy;
                bool shouldStop;

                lock (_syncRoot)
                {
                    master = _master;
                    linkBusy = _linkBusy;
                    shouldStop =
                        !_isConnected
                        || _isDisconnecting
                        || !_autoGeneralInterrogationArmed
                        || _autoGeneralInterrogationAttempted
                        || _generalInterrogationSent
                        || _generalInterrogationInProgress;
                }

                if (shouldStop || master == null)
                {
                    return;
                }

                if (linkBusy)
                {
                    busyRetryCount++;
                    if (busyRetryCount <= 3)
                    {
                    try
                    {
                        await Task.Delay(Math.Max(50, settings.BusyBackoffMs), cancellationToken).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }

                    continue;
                    }
                }

                try
                {
                    lock (_masterOperationLock)
                    {
                        master.SlaveAddress = settings.LinkAddress;
                        master.SendInterrogationCommand(CauseOfTransmission.ACTIVATION, settings.CasduAddress, 20);
                        master.Run();
                    }

                    lock (_syncRoot)
                    {
                        _autoGeneralInterrogationAttempted = true;
                        _generalInterrogationSent = true;
                        _generalInterrogationInProgress = true;
                        _autoGeneralInterrogationArmed = false;
                        _currentFlowClass = "Class 2";
                        _giAttemptCounter++;
                    }

                    TraceGi(string.Format("Startup GI attempt #{0} send returned successfully.", _giAttemptCounter));
                    RaiseLine(CreateFlowRow("Info", "GI command sent", "Class 2", string.Format("LinkAddress={0}, CA={1}", settings.LinkAddress, settings.CasduAddress)));
                    return;
                }
                catch (LinkLayerBusyException)
                {
                    TraceGi("Startup GI busy; retrying.");
                    MarkBusy(SystemUtils.currentTimeMillis(), "GI delayed because the link layer is busy.");
                }
                catch (Exception ex)
                {
                    TraceGi("Startup GI failed: " + ex.Message);
                    RaiseLine(CreateErrorRow("GI failed", ex.Message));
                    return;
                }
            }
        }

        public void NotifyActiveLinkSwitchover()
        {
            CS101Master master;
            ConnectionSettings settings;

            lock (_syncRoot)
            {
                master = _master;
                settings = _settings.Clone();
                _lastPollAt = 0;
                _linkBusy = false;
                _hasAccessDemand = false;
                _commandFollowUpUntil = 0;
                _commandFollowUpObserved = false;
                _lastBusyAt = 0;
            }

            if (master == null || settings == null || settings.ChannelOperationMode != Iec101ChannelOperationMode.FullActive)
            {
                return;
            }

            try
            {
                lock (_masterOperationLock)
                {
                    UpdateFlowClass("Class 2");
                    master.PollSingleSlave(settings.LinkAddress);
                    master.Run();
                }

                long now = SystemUtils.currentTimeMillis();
                _lastPollAt = now;
                _lastRunAt = now;
            }
            catch (Exception ex)
            {
                RaiseLine(CreateErrorRow("Switchover poll kick failed", ex.Message));
            }
        }

        public Task SendClockSyncAsync()
        {
            return Task.Run(() =>
            {
                CS101Master master;
                ConnectionSettings settings;

                lock (_syncRoot)
                {
                    master = _master;
                    settings = _settings.Clone();
                }

                if (master == null)
                {
                    return;
                }

                if (settings.ChannelOperationMode != Iec101ChannelOperationMode.FullActive)
                {
                    RaiseLine(CreateFlowRow("Info", "Clock sync skipped", "-", "Clock sync is disabled on standby supervision channel."));
                    return;
                }

                try
                {
                    CP56Time2a currentTime = new CP56Time2a(DateTime.Now);

                    lock (_masterOperationLock)
                    {
                        master.SlaveAddress = settings.LinkAddress;
                        master.SendClockSyncCommand(settings.CasduAddress, currentTime);
                        master.Run();
                    }

                    _currentFlowClass = "Class 2";
                    RaiseLine(CreateFlowRow("Info", "Clock sync sent", "Class 2", string.Format("LinkAddress={0}, CA={1}", settings.LinkAddress, settings.CasduAddress)));
                }
                catch (LinkLayerBusyException)
                {
                    MarkBusy(SystemUtils.currentTimeMillis(), "Clock sync delayed because the link layer is busy.");
                }
                catch (Exception ex)
                {
                    RaiseLine(CreateErrorRow("Clock sync failed", ex.Message));
                }
            });
        }

        public Task<bool> SendLinkLayerTestFunctionAsync()
        {
            return Task.Run(() =>
            {
                CS101Master master;
                ConnectionSettings settings;

                lock (_syncRoot)
                {
                    master = _master;
                    settings = _settings.Clone();
                }

                if (master == null || settings == null || settings.ChannelOperationMode != Iec101ChannelOperationMode.StandbySupervision)
                {
                    return false;
                }

                try
                {
                    lock (_masterOperationLock)
                    {
                        master.SendLinkLayerTestFunction();
                        master.Run();
                    }

                    RaiseLine(CreateFlowRow("Info", "Link test sent", "-", "CS101 link-layer test function dispatched."));
                    return true;
                }
                catch (LinkLayerBusyException)
                {
                    MarkBusy(SystemUtils.currentTimeMillis(), "Link test delayed because the link layer is busy.");
                    return false;
                }
                catch (Exception ex)
                {
                    RaiseLine(CreateErrorRow("Link test failed", ex.Message));
                    return false;
                }
            });
        }


        public Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0)
        {
            lock (_syncRoot)
            {
                if (_master == null)
                {
                    throw new InvalidOperationException("IEC-101 master is not connected.");
                }
            }

            EnqueueCommand("Single", ioa, state, select, quality);
            return Task.CompletedTask;
        }

        public Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0)
        {
            lock (_syncRoot)
            {
                if (_master == null)
                {
                    throw new InvalidOperationException("IEC-101 master is not connected.");
                }
            }

            EnqueueCommand("Double", ioa, on, select, quality);
            return Task.CompletedTask;
        }

        public Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0)
        {
            lock (_syncRoot)
            {
                if (_master == null)
                {
                    throw new InvalidOperationException("IEC-101 master is not connected.");
                }
            }

            EnqueueCommand("Step", ioa, raise, select, quality);
            return Task.CompletedTask;
        }

        public Task SendSetpointNormalizedCommandAsync(int ioa, float normalizedValue, bool select = false, int quality = 0)
        {
            lock (_syncRoot)
            {
                if (_master == null)
                {
                    throw new InvalidOperationException("IEC-101 master is not connected.");
                }
            }

            EnqueueSetpointCommand(ioa, normalizedValue, select, quality);
            return Task.CompletedTask;
        }

        private async Task DisconnectInternalAsync(bool raiseDisconnectedState)
        {
            CS101Master master;
            SerialPort serialPort;
            CancellationTokenSource workerCancellationSource;
            Task workerTask;
            bool shouldReturn;

            lock (_syncRoot)
            {
                shouldReturn = _isDisconnecting;
                if (!shouldReturn)
                {
                    _isDisconnecting = true;
                }

                master = _master;
                serialPort = _serialPort;
                workerCancellationSource = _workerCancellationSource;
                workerTask = _workerTask;

                _master = null;
                _serialPort = null;
                _workerCancellationSource = null;
                _workerTask = null;
                _isConnected = false;
            }

            if (shouldReturn)
            {
                return;
            }

            try
            {
                if (raiseDisconnectedState)
                {
                    RaiseConnectionState(ConnectionStatusInfo.Disconnecting);
                }

                if (workerCancellationSource != null)
                {
                    workerCancellationSource.Cancel();
                }

                try { _startupGiCancellationSource?.Cancel(); } catch { }

                if (workerTask != null)
                {
                    try { await workerTask.ConfigureAwait(false); } catch { }
                }

                if (master != null)
                {
                    try { master.Stop(); } catch { }
                }

                if (serialPort != null)
                {
                    try
                    {
                        if (serialPort.IsOpen)
                        {
                            serialPort.Close();
                        }
                    }
                    catch (Exception closeEx)
                    {
                        RaiseLine(CreateErrorRow("Serial port warning", closeEx.Message));
                    }
                    finally
                    {
                        serialPort.Dispose();
                    }
                }

                workerCancellationSource?.Dispose();
                _startupGiCancellationSource?.Dispose();
                _startupGiCancellationSource = null;
                ResetSessionState();

                if (raiseDisconnectedState)
                {
                    RaiseLine(_lineMonitorFormatter.CreateSystemRow("STATE", "Disconnected", "Communication stopped cleanly."));
                    RaiseConnectionState(ConnectionStatusInfo.Disconnected);
                }
            }
            finally
            {
                lock (_syncRoot)
                {
                    _isDisconnecting = false;
                }
            }
        }

        private async Task RunWorkerAsync(CS101Master master, ConnectionSettings settings, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.WriteLine(string.Format(
                    "[POLL-TICK] mode={0} lastPoll={1} linkBusy={2} hasAccessDemand={3} cmdFollowupUntil={4}",
                    settings.ChannelOperationMode,
                    _lastPollAt,
                    _linkBusy ? 1 : 0,
                    _hasAccessDemand ? 1 : 0,
                    _commandFollowUpUntil));

                if (settings.ChannelOperationMode != Iec101ChannelOperationMode.FullActive)
                {
                    Trace.WriteLine("[POLL-SKIP reason=standby]");
                    try
                    {
                        lock (_masterOperationLock)
                        {
                            master.Run();
                        }

                        _lastRunAt = SystemUtils.currentTimeMillis();
                    }
                    catch (Exception ex)
                    {
                        RaiseLine(CreateErrorRow("Standby worker error", ex.Message));
                    }

                    try
                    {
                        await Task.Delay(StandbyWorkerSleepMs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                long now = SystemUtils.currentTimeMillis();
                bool shouldPoll = false;
                bool shouldPollClass1 = false;
                int activePollInterval = settings.PollIntervalMs;

                try
                {
                    lock (_syncRoot)
                    {
                        bool commandFollowUpActive = now < _commandFollowUpUntil;
                        bool backoffActive = _linkBusy && (now - _lastBusyAt) < settings.BusyBackoffMs;
                        if (!backoffActive)
                        {
                            _linkBusy = false;
                        }

                        shouldPoll = !backoffActive;
                        shouldPollClass1 = _hasAccessDemand || commandFollowUpActive;
                        activePollInterval = shouldPollClass1 ? settings.Class1PollIntervalMs : settings.PollIntervalMs;

                        if (backoffActive)
                        {
                            Trace.WriteLine(string.Format("[POLL-SKIP reason=busy now={0} lastBusyAt={1} backoffMs={2}]", now, _lastBusyAt, settings.BusyBackoffMs));
                        }
                        else if (commandFollowUpActive)
                        {
                            Trace.WriteLine(string.Format("[POLL-SKIP reason=followup now={0} until={1}]", now, _commandFollowUpUntil));
                        }
                    }

                    if (TryExecutePendingCommand(master, settings, now))
                    {
                        try
                        {
                            await Task.Delay(WorkerSleepMs, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        continue;
                    }

                    if (shouldPoll && (now - _lastPollAt) >= activePollInterval)
                    {
                        lock (_masterOperationLock)
                        {
                            if (shouldPollClass1)
                            {
                                Trace.WriteLine(string.Format("[POLL-C1] linkAddress={0} interval={1}", settings.LinkAddress, activePollInterval));
                                UpdateFlowClass("Class 1");
                                master.RequestClass1Data(settings.LinkAddress);
                            }
                            else
                            {
                                Trace.WriteLine(string.Format("[POLL-C2] linkAddress={0} interval={1}", settings.LinkAddress, activePollInterval));
                                UpdateFlowClass("Class 2");
                                master.PollSingleSlave(settings.LinkAddress);
                            }

                            master.Run();
                        }

                        _lastPollAt = now;
                        _lastRunAt = now;
                    }
                    else
                    {
                        Trace.WriteLine(string.Format("[POLL-SKIP reason=cooldown now={0} lastPoll={1} interval={2} shouldPoll={3} class1={4}", now, _lastPollAt, activePollInterval, shouldPoll ? 1 : 0, shouldPollClass1 ? 1 : 0));
                        lock (_masterOperationLock)
                        {
                            master.Run();
                        }

                        _lastRunAt = now;
                    }
                }
                catch (LinkLayerBusyException)
                {
                    Trace.WriteLine(string.Format("[POLL-SKIP reason=busy-exception now={0} pollInterval={1}]", now, settings.PollIntervalMs));
                    MarkBusy(now, string.Format("Poll delayed. PollInterval={0}ms", settings.PollIntervalMs));
                }
                catch (Exception ex)
                {
                    RaiseLine(CreateErrorRow("Worker error", ex.Message));
                    RegisterTransientLinkError(now, settings);
                }

                bool raiseCommandTimeout = false;
                string commandTimeoutDetail = null;

                lock (_syncRoot)
                {
                    if (_lastCommandSentAt > 0
                        && !_commandFollowUpObserved
                        && now > _commandFollowUpUntil)
                    {
                        raiseCommandTimeout = true;
                        commandTimeoutDetail = _lastCommandSummary;
                        _commandFollowUpObserved = true;
                    }
                }

                if (raiseCommandTimeout)
                {
                    RaiseLine(CreateFlowRow(
                        "Warning",
                        "Command follow-up timeout",
                        "Class 1",
                        commandTimeoutDetail ?? "No command follow-up observed."));
                }

                try
                {
                    await Task.Delay(WorkerSleepMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private SerialPort CreateSerialPort(ConnectionSettings settings)
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

        private static LinkLayerMode ParseLinkLayerMode(string value)
        {
            return string.Equals(value, "Balanced", StringComparison.OrdinalIgnoreCase)
                ? LinkLayerMode.BALANCED
                : LinkLayerMode.UNBALANCED;
        }

        private static LinkLayerParameters CreateLinkLayerParameters(ConnectionSettings settings)
        {
            return new LinkLayerParameters
            {
                AddressLength = settings.LinkAddressLength,
                TimeoutForACK = Math.Max(500, settings.ResponseTimeoutMs),
                TimeoutLinkState = Math.Max(1000, settings.LinkStatusTimeoutMs),
                TimeoutRepeat = Math.Max(500, settings.ResponseTimeoutMs),
                UseSingleCharACK = settings.UseSingleCharAck
            };
        }

        private static ApplicationLayerParameters CreateApplicationLayerParameters(ConnectionSettings settings)
        {
            return new ApplicationLayerParameters
            {
                OA = settings.OriginatorAddress,
                SizeOfCA = settings.CasduLength,
                SizeOfIOA = settings.IoaLength
            };
        }

        private bool OnAsduReceived(object parameter, int address, ASDU asdu)
        {
            _lastGoodResponseAt = SystemUtils.currentTimeMillis();
            _consecutiveLinkErrors = 0;
            _faultRaised = false;
            UpdateGeneralInterrogationState(asdu);

            RaiseLine(new LineMonitorRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = "DBG",
                FrameType = "ASDU",
                Summary = "OnAsduReceived",
                ControlFc = "-",
                ACD = "-",
                DFC = "-",
                AsduType = asdu == null ? "-" : asdu.TypeId.ToString(),
                COT = asdu == null ? "-" : asdu.Cot.ToString().Replace('_', ' '),
                CASDU = asdu == null ? "-" : asdu.Ca.ToString(),
                IOA = "-",
                RawHex = string.Empty,
                Detail = asdu == null
                    ? "ASDU null"
                    : string.Format("Type={0}, COT={1}, CA={2}, Elements={3}", asdu.TypeId, asdu.Cot, asdu.Ca, asdu.NumberOfElements),
                DataClass = GetAsduClass(asdu)
            });

            if (
                (asdu.TypeId == TypeID.C_SC_NA_1 ||
                 asdu.TypeId == TypeID.C_DC_NA_1 ||
                 asdu.TypeId == TypeID.C_RC_NA_1)
                &&
                (asdu.Cot == CauseOfTransmission.ACTIVATION_CON ||
                 asdu.Cot == CauseOfTransmission.ACTIVATION_TERMINATION)
            )
            {
                lock (_syncRoot)
                {
                    _commandFollowUpObserved = true;
                }
            }

            LineMonitorRow asduLine = _lineMonitorFormatter.FromAsdu("RX", asdu);
            asduLine.DataClass = GetAsduClass(asdu);
            RaiseLine(asduLine);

            if (!ShouldMapToValueViewer(asdu))
            {
                return true;
            }

            for (int index = 0; index < asdu.NumberOfElements; index++)
            {
                try
                {
                    InformationObject informationObject = asdu.GetElement(index);
                    ValueViewerRow row = _mapper.Map(asdu, informationObject);
                    if (row != null)
                    {
                        row.TrafficClass = GetAsduClass(asdu);
                        row.DeliveryContext = GetAsduDeliveryContext(asdu);

                        // ACD is a frame-level indication, not a point attribute.
                        // Do not stamp it onto Value Viewer rows.
                        row.Acd = "-";

                        if (row.IOA == 8388714)
                        {
                            RaiseLine(new LineMonitorRow
                            {
                                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                                Direction = "DBG",
                                FrameType = "ASDU",
                                Summary = "L1FT traced",
                                ControlFc = "-",
                                ACD = "-",
                                DFC = "-",
                                AsduType = asdu.TypeId.ToString(),
                                COT = asdu.Cot.ToString().Replace('_', ' '),
                                CASDU = asdu.Ca.ToString(),
                                IOA = row.IOA.ToString(),
                                RawHex = string.Empty,
                                Detail = string.Format("Value={0}, Timestamp={1}, RaisedValue=True", row.Value, string.IsNullOrWhiteSpace(row.Timestamp) ? "-" : row.Timestamp),
                                DataClass = row.TrafficClass
                            });
                        }

                        RaiseValue(row);
                    }
                }
                catch (Exception ex)
                {
                    RaiseLine(new LineMonitorRow
                    {
                        Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                        Direction = "ERR",
                        FrameType = "ASDU",
                        Summary = "ASDU decode error",
                        ControlFc = "-",
                        ACD = "-",
                        DFC = "-",
                        IOA = "-",
                        AsduType = asdu.TypeId.ToString(),
                        COT = asdu.Cot.ToString().Replace('_', ' '),
                        CASDU = asdu.Ca.ToString(),
                        RawHex = ex.Message,
                        Detail = "Index=" + index,
                        DataClass = GetAsduClass(asdu)
                    });
                }
            }

            return true;
        }

        private bool OnRawMessageReceived(object parameter, byte[] msg, int msgSize)
        {
            ConnectionSettings settings;
            lock (_syncRoot)
            {
                settings = _settings == null ? ConnectionSettings.CreateDefault() : _settings.Clone();
            }

            bool isVariableFrame = msg != null && msgSize >= 2 && msg[0] == 0x68;
            if (isVariableFrame)
            {
                RaiseLine(new LineMonitorRow
                {
                    Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Direction = "DBG",
                    FrameType = "Variable",
                    Summary = "Raw variable RX observed",
                    ControlFc = "-",
                    ACD = "-",
                    DFC = "-",
                    AsduType = "-",
                    COT = "-",
                    CASDU = "-",
                    IOA = "-",
                    RawHex = string.Empty,
                    Detail = string.Format("Length={0}", msgSize),
                    DataClass = "RX"
                });
            }
            ProcessSecondaryFrame(msg, msgSize);
            RaiseLine(_lineMonitorFormatter.FromRawMessage("RX", msg, msgSize, settings));
            return true;
        }

        private bool OnRawMessageSent(object parameter, byte[] msg, int msgSize)
        {
            ConnectionSettings settings;
            lock (_syncRoot)
            {
                settings = _settings == null ? ConnectionSettings.CreateDefault() : _settings.Clone();
            }

            ProcessPrimaryFrame(msg, msgSize);
            TraceTxGiFrame(msg, msgSize);
            RaiseLine(_lineMonitorFormatter.FromRawMessage("TX", msg, msgSize, settings));
            return true;
        }

        private void OnLinkLayerStateChanged(object parameter, int address, LinkLayerState state)
        {
            long now = SystemUtils.currentTimeMillis();

            switch (state)
            {
                case LinkLayerState.AVAILABLE:
                    lock (_syncRoot)
                    {
                        _linkAvailable = true;
                        _consecutiveLinkErrors = 0;
                        _faultRaised = false;
                    }
                    TraceGi("LinkLayerState AVAILABLE.");
                    RaiseConnectionState(ConnectionStatusInfo.Connected);
                    break;
                case LinkLayerState.BUSY:
                    TraceGi("LinkLayerState BUSY.");
                    MarkBusy(now, "Address=" + address + ", State=BUSY");
                    break;
                case LinkLayerState.ERROR:
                    TraceGi("LinkLayerState ERROR.");
                    RegisterTransientLinkError(now, _settings);
                    break;
            }

            if (state != LinkLayerState.BUSY)
            {
                RaiseLine(new LineMonitorRow
                {
                    Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Direction = "STATE",
                    FrameType = "Info",
                    Summary = "Link layer " + state,
                    ControlFc = "-",
                    ACD = "-",
                    DFC = "-",
                    AsduType = "-",
                    COT = "-",
                    CASDU = address.ToString(),
                    IOA = "-",
                    RawHex = string.Empty,
                    Detail = string.Format("Address={0}, State={1}", address, state),
                    DataClass = string.IsNullOrWhiteSpace(_currentFlowClass) ? "-" : _currentFlowClass
                });
            }
        }

        private void ProcessPrimaryFrame(byte[] msg, int msgSize)
        {
            if (msg == null || msgSize < 2 || msg[0] != 0x10)
            {
                return;
            }

            byte control = msg[1];
            int fc = control & 0x0F;
            if (fc == 10)
            {
                UpdateFlowClass("Class 1");
            }
            else if (fc == 11)
            {
                UpdateFlowClass("Class 2");
            }
        }

        private void ProcessSecondaryFrame(byte[] msg, int msgSize)
        {
            if (msg == null || msgSize < 2)
            {
                return;
            }

            int controlIndex;
            if (msg[0] == 0x10)
            {
                controlIndex = 1;
            }
            else if (msg[0] == 0x68 && msgSize >= 5)
            {
                controlIndex = 4;
            }
            else
            {
                return;
            }

            byte control = msg[controlIndex];
            bool prm = (control & 0x40) != 0;
            if (prm)
            {
                return;
            }

            _lastGoodResponseAt = SystemUtils.currentTimeMillis();
            _consecutiveLinkErrors = 0;
            _faultRaised = false;

            bool acd = (control & 0x20) != 0;
            bool dfc = (control & 0x10) != 0;

            if (_lastLoggedAcd == null || _lastLoggedAcd.Value != acd)
            {
                RaiseLine(CreateFlowRow("Info", acd ? "ACD asserted" : "ACD cleared", "-", acd ? "Slave reported ACD=1." : "Slave reported ACD=0."));
                _lastLoggedAcd = acd;
            }

            lock (_syncRoot)
            {
                _hasAccessDemand = acd;
                _linkBusy = dfc;
                _lastRxFrameAcd = acd ? "1" : "0";
                _lastRxFrameClass = string.IsNullOrWhiteSpace(_currentFlowClass) ? "Unknown" : _currentFlowClass;
                if (acd)
                {
                    _lastPollAt = 0;
                }
            }
            if (dfc)
            {
                _lastBusyAt = _lastGoodResponseAt;
                MarkBusy(_lastGoodResponseAt, "Slave set DFC=1.");
            }
        }

        private void EnqueueCommand(string kind, int ioa, bool state, bool select, int quality)
        {
            lock (_syncRoot)
            {
                _pendingCommand = new PendingCommandRequest
                {
                    Kind = kind ?? string.Empty,
                    Ioa = ioa,
                    State = state,
                    Select = select,
                    Quality = quality,
                    EnqueuedAt = SystemUtils.currentTimeMillis()
                };

                _lastPollAt = 0;
            }
        }

        private void EnqueueSetpointCommand(int ioa, float normalizedValue, bool select, int quality)
        {
            lock (_syncRoot)
            {
                _pendingCommand = new PendingCommandRequest
                {
                    Kind = "SetpointNormalized",
                    Ioa = ioa,
                    NormalizedValue = normalizedValue,
                    Select = select,
                    Quality = quality,
                    EnqueuedAt = SystemUtils.currentTimeMillis()
                };

                _lastPollAt = 0;
            }
        }

        private bool TryExecutePendingCommand(CS101Master master, ConnectionSettings settings, long now)
        {
            if (settings.ChannelOperationMode != Iec101ChannelOperationMode.FullActive)
            {
                return false;
            }

            PendingCommandRequest request = null;

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

                request = _pendingCommand;
            }

            try
            {
                lock (_masterOperationLock)
                {
                    master.SlaveAddress = settings.LinkAddress;

                    switch (request.Kind)
                    {
                        case "Single":
                            master.SendControlCommand(
                                CauseOfTransmission.ACTIVATION,
                                settings.CasduAddress,
                                new SingleCommand(request.Ioa, request.State, request.Select, request.Quality));
                            break;

                        case "Double":
                            master.SendControlCommand(
                                CauseOfTransmission.ACTIVATION,
                                settings.CasduAddress,
                                new DoubleCommand(
                                    request.Ioa,
                                    request.State ? DoubleCommand.ON : DoubleCommand.OFF,
                                    request.Select,
                                    request.Quality));
                            break;

                        case "Step":
                            master.SendControlCommand(
                                CauseOfTransmission.ACTIVATION,
                                settings.CasduAddress,
                                new StepCommand(
                                    request.Ioa,
                                    request.State ? StepCommandValue.HIGHER : StepCommandValue.LOWER,
                                    request.Select,
                                    request.Quality));
                            break;

                        case "SetpointNormalized":
                            master.SendControlCommand(
                                CauseOfTransmission.ACTIVATION,
                                settings.CasduAddress,
                                new SetpointCommandNormalized(
                                    request.Ioa,
                                    request.NormalizedValue,
                                    new SetpointCommandQualifier(request.Select, request.Quality)));
                            break;

                        default:
                            return false;
                    }

                    master.Run();
                }

                lock (_syncRoot)
                {
                    if (_pendingCommand == request)
                    {
                        _pendingCommand = null;
                    }

                    _lastCommandSentAt = now;
                    _commandFollowUpUntil = now + CommandFollowUpWindowMs;
                    _commandFollowUpObserved = false;
                    _lastPollAt = 0;

                    switch (request.Kind)
                    {
                        case "Single":
                            _lastCommandSummary = string.Format(
                                "Single IOA={0} State={1} Select={2}",
                                request.Ioa,
                                request.State ? "ON" : "OFF",
                                request.Select ? 1 : 0);
                            break;

                        case "Double":
                            _lastCommandSummary = string.Format(
                                "Double IOA={0} State={1} Select={2}",
                                request.Ioa,
                                request.State ? "CLOSE" : "OPEN",
                                request.Select ? 1 : 0);
                            break;

                        case "Step":
                            _lastCommandSummary = string.Format(
                                "Step IOA={0} State={1} Select={2}",
                                request.Ioa,
                                request.State ? "RAISE" : "LOWER",
                                request.Select ? 1 : 0);
                            break;
                    }
                }

                string summary;
                switch (request.Kind)
                {
                    case "Single":
                        summary = request.State ? "Single command ON sent" : "Single command OFF sent";
                        break;
                    case "Double":
                        summary = request.State ? "Double command CLOSE sent" : "Double command OPEN sent";
                        break;
                    default:
                        summary = request.State ? "Step command RAISE sent" : "Step command LOWER sent";
                        break;
                }

                RaiseLine(CreateFlowRow(
                    "Info",
                    summary,
                    "Class 1",
                    string.Format("IOA={0}, CA={1}, Select={2}", request.Ioa, settings.CasduAddress, request.Select ? 1 : 0)));

                return true;
            }
            catch (LinkLayerBusyException)
            {
                MarkBusy(now, "Queued command still waiting because message is pending.");
                return false;
            }
            catch (Exception ex)
            {
                lock (_syncRoot)
                {
                    if (_pendingCommand == request)
                    {
                        _pendingCommand = null;
                    }
                }

                RaiseLine(CreateErrorRow("Queued command failed", ex.Message));
                return false;
            }
        }

        private void UpdateGeneralInterrogationState(ASDU asdu)
        {
            if (asdu == null || asdu.TypeId != TypeID.C_IC_NA_1)
            {
                return;
            }

            if (asdu.Cot == CauseOfTransmission.ACTIVATION_CON)
            {
                lock (_syncRoot)
                {
                    _generalInterrogationInProgress = true;
                }
                TraceGi("Received GI ACT_CON.");
            }
            else if (asdu.Cot == CauseOfTransmission.ACTIVATION_TERMINATION)
            {
                lock (_syncRoot)
                {
                    _generalInterrogationInProgress = false;
                }
                TraceGi("Received GI ACT_TERM.");
            }
        }

        private string GetAsduClass(ASDU asdu)
        {
            if (asdu == null)
            {
                return string.IsNullOrWhiteSpace(_lastRxFrameClass)
                    ? (string.IsNullOrWhiteSpace(_currentFlowClass) ? "Unknown" : _currentFlowClass)
                    : _lastRxFrameClass;
            }

            switch (asdu.Cot)
            {
                case CauseOfTransmission.INTERROGATED_BY_STATION:
                case CauseOfTransmission.BACKGROUND_SCAN:
                case CauseOfTransmission.PERIODIC:
                    return "Class 2";
            }

            switch (asdu.TypeId)
            {
                case TypeID.C_IC_NA_1:
                    return "Class 2";
                case TypeID.C_CS_NA_1:
                case TypeID.C_SC_NA_1:
                case TypeID.C_DC_NA_1:
                case TypeID.C_RC_NA_1:
                case TypeID.C_SE_NA_1:
                case TypeID.C_SE_NB_1:
                case TypeID.C_SE_NC_1:
                    return "Class 1";
            }

            if (!string.IsNullOrWhiteSpace(_lastRxFrameClass))
            {
                return _lastRxFrameClass;
            }

            return string.IsNullOrWhiteSpace(_currentFlowClass) ? "Unknown" : _currentFlowClass;
        }

        private string GetAsduAcd(ASDU asdu)
        {
            if (!string.IsNullOrWhiteSpace(_lastRxFrameAcd))
            {
                return _lastRxFrameAcd;
            }

            return "-";
        }

        private string GetAsduDeliveryContext(ASDU asdu)
        {
            if (asdu == null)
            {
                return "Unknown";
            }

            switch (asdu.Cot)
            {
                case CauseOfTransmission.SPONTANEOUS:
                    return "Spontaneous";
                case CauseOfTransmission.INTERROGATED_BY_STATION:
                    return "GI Response";
                case CauseOfTransmission.BACKGROUND_SCAN:
                case CauseOfTransmission.PERIODIC:
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

        private static bool ShouldMapToValueViewer(ASDU asdu)
        {
            switch (asdu.TypeId)
            {
                case TypeID.C_IC_NA_1:
                case TypeID.C_CS_NA_1:
                case TypeID.C_RD_NA_1:
                case TypeID.C_SC_NA_1:
                case TypeID.C_DC_NA_1:
                case TypeID.C_RC_NA_1:
                case TypeID.C_SE_NA_1:
                case TypeID.C_SE_NB_1:
                case TypeID.C_SE_NC_1:
                    return false;
                default:
                    return true;
            }
        }

        private void UpdateFlowClass(string nextClass)
        {
            if (string.IsNullOrWhiteSpace(nextClass))
            {
                return;
            }

            _currentFlowClass = nextClass;
            if (string.Equals(_lastLoggedFlowClass, nextClass, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RaiseLine(CreateFlowRow("Info", "Polling switched", nextClass, string.Format("System is now serving {0} traffic.", nextClass)));
            _lastLoggedFlowClass = nextClass;
        }

        private void RegisterTransientLinkError(long now, ConnectionSettings settings)
        {
            lock (_syncRoot)
            {
                _linkAvailable = false;
                _consecutiveLinkErrors++;
            }

            long timeoutWindow = Math.Max(1500, settings.ResponseTimeoutMs * 2);
            bool silentTooLong = (_lastGoodResponseAt > 0) && ((now - _lastGoodResponseAt) >= timeoutWindow);

            if (!_faultRaised && _consecutiveLinkErrors >= ErrorToleranceCount && silentTooLong)
            {
                lock (_syncRoot)
                {
                    _faultRaised = true;
                }
                RaiseConnectionState(ConnectionStatusInfo.Faulted);
            }
        }

        private void MarkBusy(long now, string detail)
        {
            lock (_syncRoot)
            {
                _linkBusy = true;
            }
            _lastBusyAt = now;

            if ((now - _lastBusyLogAt) >= BusyLogIntervalMs)
            {
                _lastBusyLogAt = now;
                RaiseLine(CreateFlowRow("Warning", "Link layer busy", string.IsNullOrWhiteSpace(_currentFlowClass) ? "-" : _currentFlowClass, detail ?? "Link layer is busy."));
            }
        }

        private void ArmCommandFollowUp(long now)
        {
            lock (_syncRoot)
            {
                _commandFollowUpUntil = now + CommandFollowUpWindowMs;
                _lastCommandSentAt = now;
                _commandFollowUpObserved = false;
                _lastPollAt = 0;
            }
        }

        private void ResetSessionState()
        {
                _hasAccessDemand = false;
                _linkBusy = false;
                _linkAvailable = false;
                _generalInterrogationSent = false;
                _generalInterrogationInProgress = false;
            _autoGeneralInterrogationArmed = false;
            _autoGeneralInterrogationAttempted = false;
            _lastBusyAt = 0;
            _lastBusyLogAt = 0;
            _lastGoodResponseAt = 0;
            _lastRunAt = 0;
                _lastPollAt = 0;
                _commandFollowUpUntil = 0;
                _pendingCommand = null;
                _consecutiveLinkErrors = 0;
                _faultRaised = false;
                _currentFlowClass = "Class 2";
                _lastLoggedAcd = null;
            _lastLoggedFlowClass = null;
            _lastRxFrameAcd = null;
            _lastRxFrameClass = null;
            _giAttemptCounter = 0;
            _manualGiRequestCount = 0;
            _txGiAsduCount = 0;
            TraceGi("Session state reset.");
        }

        private void TraceTxGiFrame(byte[] msg, int msgSize)
        {
            if (msg == null || msgSize < 10 || msg[0] != 0x68)
            {
                return;
            }

            int controlIndex = 4;
            if (msgSize <= controlIndex)
            {
                return;
            }

            byte control = msg[controlIndex];
            bool prm = (control & 0x40) != 0;
            int fc = control & 0x0F;
            if (!prm || fc != 3)
            {
                return;
            }

            int asduIndex = controlIndex + 3;
            if (msgSize <= asduIndex)
            {
                return;
            }

            if (msg[asduIndex] != (byte)TypeID.C_IC_NA_1)
            {
                return;
            }

            int txCount;
            lock (_syncRoot)
            {
                _txGiAsduCount++;
                txCount = _txGiAsduCount;
            }

            TraceGi(string.Format("Raw TX GI ASDU observed. txGiAsduCount={0}", txCount));
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
            if (info != null)
            {
                ConnectionStateChanged?.Invoke(this, info);
            }
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

        private void TraceGi(string message)
        {
            Trace.WriteLine(string.Format(
                "[IEC101 GI] {0:HH:mm:ss.fff} T{1} sent={2} inProgress={3} armed={4} linkAvailable={5} linkBusy={6} :: {7}",
                DateTime.Now,
                Thread.CurrentThread.ManagedThreadId,
                _generalInterrogationSent ? 1 : 0,
                _generalInterrogationInProgress ? 1 : 0,
                _autoGeneralInterrogationArmed ? 1 : 0,
                _linkAvailable ? 1 : 0,
                _linkBusy ? 1 : 0,
                message ?? string.Empty));
        }
    }
}
