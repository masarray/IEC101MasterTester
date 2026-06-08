using System;
using System.Threading;
using System.Threading.Tasks;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101;

namespace IEC101MasterTester.Services.Redundancy
{
    public sealed class NucIec101LinkChannel : INucLinkChannel
    {
        private readonly object _syncRoot = new object();
        private readonly IIec101MasterService _service;
        private ConnectionSettings _baseSettings;
        private NucChannelRole _role;
        private NucChannelSnapshot _snapshot;
        private bool _faultLatched;
        private Timer _standbySupervisionTimer;
        private int _standbyTickInFlight;
        private int _recoveryInFlight;
        private DateTime? _standbyStartedAtUtc;
        private DateTime? _firstSuccessfulStandbySupervisionUtc;
        private DateTime? _lastRecoveryProbeUtc;
        private DateTime? _lastReconnectAttemptUtc;

        private static readonly TimeSpan StandbySupervisionInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan StandbyInitialResponseWindow = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan StandbySupervisionResponseWindow = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RecoveryProbeInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RecoveryReconnectInterval = TimeSpan.FromSeconds(5);

        public NucIec101LinkChannel(string name, IIec101MasterService service)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Channel name is required.", nameof(name));
            }

            _service = service ?? throw new ArgumentNullException(nameof(service));
            Name = name;
            _role = NucChannelRole.Standby;
            _snapshot = CreateSnapshot();

            _service.ConnectionStateChanged += Service_ConnectionStateChanged;
            _service.LineMonitorRecordReceived += Service_LineMonitorRecordReceived;
            _service.ValueReceived += Service_ValueReceived;
        }

        public event EventHandler<ConnectionStatusInfo> ConnectionStateChanged;
        public event EventHandler<LineMonitorRow> LineMonitorRecordReceived;
        public event EventHandler<ValueViewerRow> ValueReceived;
        public event EventHandler<NucChannelSnapshot> SnapshotChanged;

        public string Name { get; }

        public NucChannelRole Role
        {
            get
            {
                lock (_syncRoot)
                {
                    return _role;
                }
            }
        }

        public NucChannelSnapshot Snapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return CloneSnapshot(_snapshot);
                }
            }
        }

        public void ApplySettings(ConnectionSettings baseSettings)
        {
            lock (_syncRoot)
            {
                _baseSettings = baseSettings == null ? null : baseSettings.Clone();
            }
        }

        public Task StartAsActiveAsync()
        {
            return StartWithRoleAsync(NucChannelRole.Active);
        }

        public Task StartAsStandbyAsync()
        {
            return StartWithRoleAsync(NucChannelRole.Standby);
        }

        public Task PromoteToActiveAsync()
        {
            return StartWithRoleAsync(NucChannelRole.Active);
        }

        public Task DemoteToStandbyAsync()
        {
            return StartWithRoleAsync(NucChannelRole.Standby);
        }

        public Task StopAsync()
        {
            StopStandbySupervisionTimer();
            return _service.DisconnectAsync();
        }

        public Task RecoverAsync(string reason = null)
        {
            return RecoverCoreAsync(string.IsNullOrWhiteSpace(reason) ? "controller requested recovery" : reason);
        }

        public Task SendGeneralInterrogationAsync()
        {
            if (Role != NucChannelRole.Active)
            {
                return Task.CompletedTask;
            }

            return _service.SendGeneralInterrogationAsync();
        }

        public void NotifyActiveLinkSwitchover()
        {
            _service.NotifyActiveLinkSwitchover();
        }

        public Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0)
        {
            return Role != NucChannelRole.Active
                ? Task.CompletedTask
                : _service.SendSingleCommandAsync(ioa, state, select, quality);
        }

        public Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0)
        {
            return Role != NucChannelRole.Active
                ? Task.CompletedTask
                : _service.SendDoubleCommandAsync(ioa, on, select, quality);
        }

        public Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0)
        {
            return Role != NucChannelRole.Active
                ? Task.CompletedTask
                : _service.SendStepCommandAsync(ioa, raise, select, quality);
        }

        public Task SendSetpointNormalizedCommandAsync(int ioa, float normalizedValue, bool select = false, int quality = 0)
        {
            return Role != NucChannelRole.Active
                ? Task.CompletedTask
                : _service.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select, quality);
        }

        private async Task StartWithRoleAsync(NucChannelRole role)
        {
            ConnectionSettings settings;
            lock (_syncRoot)
            {
                _role = role;
                settings = _baseSettings == null ? null : _baseSettings.Clone();
                _faultLatched = false;
                _standbyStartedAtUtc = role == NucChannelRole.Standby ? (DateTime?)DateTime.UtcNow : null;
                _firstSuccessfulStandbySupervisionUtc = null;
                _lastRecoveryProbeUtc = null;
                _lastReconnectAttemptUtc = null;
            }

            if (settings == null)
            {
                return;
            }

            PrepareSettingsForRole(settings, role);

            StopStandbySupervisionTimer();
            UpdateSnapshot(snapshot =>
            {
                snapshot.Role = role;
                snapshot.State = NucChannelState.Reopening;
                snapshot.DetailText = string.Format("{0} channel is opening as {1}.", Name, role);
            });

            await _service.DisconnectAsync().ConfigureAwait(false);
            _service.ApplySettings(settings);
            await _service.ConnectAsync().ConfigureAwait(false);

            UpdateSnapshot(snapshot =>
            {
                bool connected = _service.IsConnected;
                snapshot.Role = role;
                snapshot.Connected = connected;
                snapshot.LastTimeoutUtc = connected ? null : (DateTime?)DateTime.UtcNow;
                snapshot.State = connected
                    ? (role == NucChannelRole.Active ? NucChannelState.ConnectedNoResponse : NucChannelState.StandbySupervision)
                    : NucChannelState.Reopening;
                snapshot.DetailText = connected
                    ? (role == NucChannelRole.Active
                        ? "Active channel opened. Waiting for first application or link-layer response."
                        : "Standby channel opened. Link-layer supervision remains armed.")
                    : "Channel open failed or is still unavailable. Recovery will retry.";
            });

            if (role == NucChannelRole.Standby)
            {
                StartStandbySupervisionTimer();
            }
        }

        private async Task RecoverCoreAsync(string reason)
        {
            if (Interlocked.Exchange(ref _recoveryInFlight, 1) != 0)
            {
                return;
            }

            try
            {
                ConnectionSettings settings;
                NucChannelRole role;
                DateTime nowUtc = DateTime.UtcNow;
                bool serviceConnected = _service.IsConnected;
                bool shouldReconnect;
                bool shouldProbe;

                lock (_syncRoot)
                {
                    settings = _baseSettings == null ? null : _baseSettings.Clone();
                    role = _role;
                    shouldReconnect = !serviceConnected
                        && (!_lastReconnectAttemptUtc.HasValue || nowUtc - _lastReconnectAttemptUtc.Value >= RecoveryReconnectInterval);
                    shouldProbe = serviceConnected
                        && (!_lastRecoveryProbeUtc.HasValue || nowUtc - _lastRecoveryProbeUtc.Value >= RecoveryProbeInterval);
                    if (shouldReconnect)
                    {
                        _lastReconnectAttemptUtc = nowUtc;
                    }
                    if (shouldProbe)
                    {
                        _lastRecoveryProbeUtc = nowUtc;
                    }
                }

                if (settings == null)
                {
                    return;
                }

                PrepareSettingsForRole(settings, role);

                if (shouldReconnect)
                {
                    UpdateSnapshot(snapshot =>
                    {
                        snapshot.Role = role;
                        snapshot.State = NucChannelState.Reopening;
                        snapshot.Connected = false;
                        snapshot.DetailText = string.Format("Recovery is reopening {0} after {1}.", Name, reason ?? "timeout");
                    });

                    await _service.DisconnectAsync().ConfigureAwait(false);
                    _service.ApplySettings(settings);
                    await _service.ConnectAsync().ConfigureAwait(false);

                    if (role == NucChannelRole.Standby)
                    {
                        StartStandbySupervisionTimer();
                    }

                    UpdateSnapshot(snapshot =>
                    {
                        bool connected = _service.IsConnected;
                        snapshot.Role = role;
                        snapshot.Connected = connected;
                        snapshot.LastTimeoutUtc = connected ? null : (DateTime?)DateTime.UtcNow;
                        snapshot.State = connected
                            ? (role == NucChannelRole.Active ? NucChannelState.ConnectedNoResponse : NucChannelState.Recovering)
                            : NucChannelState.Reopening;
                        snapshot.DetailText = connected
                            ? string.Format("Recovery reopened {0}. Waiting for first valid response.", Name)
                            : string.Format("Recovery could not reopen {0} yet. Retry remains armed.", Name);
                    });

                    return;
                }

                if (!shouldProbe)
                {
                    return;
                }

                UpdateSnapshot(snapshot =>
                {
                    snapshot.Role = role;
                    snapshot.State = NucChannelState.Recovering;
                    snapshot.Connected = true;
                    snapshot.DetailText = string.Format("Recovery probe on {0}: {1}.", Name, reason ?? "timeout/no-response");
                });

                if (role == NucChannelRole.Standby)
                {
                    await _service.SendLinkLayerTestFunctionAsync().ConfigureAwait(false);
                }
                else
                {
                    _service.NotifyActiveLinkSwitchover();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _recoveryInFlight, 0);
            }
        }

        private static void PrepareSettingsForRole(ConnectionSettings settings, NucChannelRole role)
        {
            settings.ChannelOperationMode = role == NucChannelRole.Active
                ? Iec101ChannelOperationMode.FullActive
                : Iec101ChannelOperationMode.StandbySupervision;
            settings.UseGeneralInterrogationOnConnect = role == NucChannelRole.Active
                && settings.UseGeneralInterrogationOnConnect;
        }

        private void Service_ConnectionStateChanged(object sender, ConnectionStatusInfo e)
        {
            ConnectionStateChanged?.Invoke(this, e);

            UpdateSnapshot(snapshot =>
            {
                snapshot.StatusText = e == null ? "Unknown" : e.DisplayText;
                snapshot.DetailText = e == null ? string.Empty : e.Detail;
                string displayText = e == null ? string.Empty : e.DisplayText;
                bool isConnected = string.Equals(displayText, ConnectionStatusInfo.Connected.DisplayText, StringComparison.OrdinalIgnoreCase);
                bool isDisconnected = string.Equals(displayText, ConnectionStatusInfo.Disconnected.DisplayText, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(displayText, ConnectionStatusInfo.Disconnecting.DisplayText, StringComparison.OrdinalIgnoreCase);
                bool isFaulted = string.Equals(displayText, ConnectionStatusInfo.Faulted.DisplayText, StringComparison.OrdinalIgnoreCase);

                if (isConnected)
                {
                    snapshot.Connected = true;
                    snapshot.LastTimeoutUtc = null;
                    if (snapshot.Role == NucChannelRole.Standby)
                    {
                        if (!snapshot.LastActivityUtc.HasValue)
                        {
                            snapshot.LastActivityUtc = DateTime.UtcNow;
                        }
                        snapshot.State = NucChannelState.StandbySupervision;
                    }
                    else if (!_faultLatched)
                    {
                        snapshot.State = NucChannelState.ConnectedNoResponse;
                    }
                    return;
                }

                if (isDisconnected)
                {
                    snapshot.Connected = false;
                    snapshot.State = NucChannelState.Disconnected;
                    return;
                }

                if (isFaulted)
                {
                    bool serviceStillOpen = _service.IsConnected;
                    snapshot.Connected = serviceStillOpen;
                    snapshot.LastTimeoutUtc = DateTime.UtcNow;
                    snapshot.State = serviceStillOpen ? NucChannelState.Timeout : NucChannelState.FaultLatched;
                    if (!serviceStillOpen)
                    {
                        _faultLatched = true;
                    }
                    return;
                }

                snapshot.Connected = _service.IsConnected;
            });
        }

        private void Service_LineMonitorRecordReceived(object sender, LineMonitorRow e)
        {
            LineMonitorRecordReceived?.Invoke(this, e);

            if (e == null)
            {
                return;
            }

            bool isRx = string.Equals(e.Direction, "RX", StringComparison.OrdinalIgnoreCase);
            bool isTx = string.Equals(e.Direction, "TX", StringComparison.OrdinalIgnoreCase);
            string summary = e.Summary ?? string.Empty;
            string detail = e.Detail ?? string.Empty;
            bool isLinkTestTxObserved = summary.IndexOf("link test sent", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("link test sent", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isTransportHealthFailure = IsTransportHealthFailureEvidence(e);
            bool isPhysicalTransportFailure = IsPhysicalTransportFailureEvidence(e);

            UpdateSnapshot(snapshot =>
            {
                snapshot.LastActivityUtc = DateTime.UtcNow;
                if (isTx)
                {
                    snapshot.TxCount++;
                }

                if (snapshot.Role == NucChannelRole.Standby && isLinkTestTxObserved)
                {
                    snapshot.SupervisionTxObservedCount++;
                    snapshot.LastSupervisionTxObservedUtc = DateTime.UtcNow;
                }

                if (isRx)
                {
                    snapshot.RxCount++;
                    snapshot.LastResponseUtc = DateTime.UtcNow;
                    snapshot.LastTimeoutUtc = null;
                    snapshot.Connected = true;
                    _faultLatched = false;
                    if (snapshot.Role == NucChannelRole.Standby)
                    {
                        snapshot.SupervisionResponseObservedCount++;
                        snapshot.LastSupervisionResponseUtc = DateTime.UtcNow;
                    }
                    snapshot.State = snapshot.Role == NucChannelRole.Active
                        ? NucChannelState.Responsive
                        : NucChannelState.StandbySupervision;
                }

                if (isTransportHealthFailure)
                {
                    snapshot.LastTimeoutUtc = DateTime.UtcNow;
                    snapshot.Connected = !isPhysicalTransportFailure && _service.IsConnected;
                    snapshot.State = isPhysicalTransportFailure ? NucChannelState.FaultLatched : NucChannelState.Timeout;
                    snapshot.DetailText = isPhysicalTransportFailure
                        ? "Physical transport fault was observed. Recovery will reopen the port."
                        : "Protocol timeout/no-response was observed. Recovery will continue probing without closing the port.";
                    if (snapshot.Role == NucChannelRole.Active && isPhysicalTransportFailure)
                    {
                        _faultLatched = true;
                    }
                }
            });
        }

        private static bool IsTransportHealthFailureEvidence(LineMonitorRow row)
        {
            if (row == null)
            {
                return false;
            }

            string summary = row.Summary ?? string.Empty;
            string detail = row.Detail ?? string.Empty;
            string frameType = row.FrameType ?? string.Empty;

            if (IsCommandLifecycleEvidence(summary, detail))
            {
                return false;
            }

            return summary.IndexOf("standby supervision timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("standby supervision timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("no response", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("no response", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || IsPhysicalTransportFailureEvidence(row)
                || (string.Equals(frameType, "Error", StringComparison.OrdinalIgnoreCase)
                    && (summary.IndexOf("read", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("read", StringComparison.OrdinalIgnoreCase) >= 0
                        || summary.IndexOf("connect", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("connect", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static bool IsPhysicalTransportFailureEvidence(LineMonitorRow row)
        {
            if (row == null)
            {
                return false;
            }

            string summary = row.Summary ?? string.Empty;
            string detail = row.Detail ?? string.Empty;
            return summary.IndexOf("serial port", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("serial port", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("port closed", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("port closed", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("worker error", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("worker error", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCommandLifecycleEvidence(string summary, string detail)
        {
            return ContainsAny(summary, detail,
                "command",
                "sbo",
                "select rejected",
                "execute rejected",
                "rejected",
                "follow-up timeout");
        }

        private static bool ContainsAny(string summary, string detail, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                string needle = needles[i];
                if (summary.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                    || detail.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Service_ValueReceived(object sender, ValueViewerRow e)
        {
            ValueReceived?.Invoke(this, e);

            UpdateSnapshot(snapshot =>
            {
                snapshot.LastActivityUtc = DateTime.UtcNow;
                snapshot.LastResponseUtc = DateTime.UtcNow;
                snapshot.LastTimeoutUtc = null;
                snapshot.Connected = true;
                _faultLatched = false;
                if (snapshot.Role == NucChannelRole.Active)
                {
                    snapshot.State = NucChannelState.Responsive;
                }
                else
                {
                    snapshot.SupervisionResponseObservedCount++;
                    snapshot.LastSupervisionResponseUtc = DateTime.UtcNow;
                    snapshot.State = NucChannelState.StandbySupervision;
                }
            });
        }

        private void UpdateSnapshot(Action<NucChannelSnapshot> updater)
        {
            NucChannelSnapshot cloned;
            lock (_syncRoot)
            {
                updater(_snapshot);
                cloned = CloneSnapshot(_snapshot);
            }

            SnapshotChanged?.Invoke(this, cloned);
        }

        private void StartStandbySupervisionTimer()
        {
            lock (_syncRoot)
            {
                if (_standbySupervisionTimer != null)
                {
                    return;
                }

                _standbySupervisionTimer = new Timer(
                    async _ => await RunStandbySupervisionTickAsync().ConfigureAwait(false),
                    null,
                    StandbySupervisionInterval,
                    StandbySupervisionInterval);
            }
        }

        private void StopStandbySupervisionTimer()
        {
            Timer timer;
            lock (_syncRoot)
            {
                timer = _standbySupervisionTimer;
                _standbySupervisionTimer = null;
                _standbyStartedAtUtc = null;
                _firstSuccessfulStandbySupervisionUtc = null;
            }

            if (timer != null)
            {
                timer.Dispose();
            }
        }

        private async Task RunStandbySupervisionTickAsync()
        {
            if (Interlocked.Exchange(ref _standbyTickInFlight, 1) != 0)
            {
                return;
            }

            try
            {
                if (Role != NucChannelRole.Standby)
                {
                    return;
                }

                DateTime nowUtc = DateTime.UtcNow;
                if (!_service.IsConnected)
                {
                    await RecoverCoreAsync("standby port is closed").ConfigureAwait(false);
                    return;
                }

                UpdateSnapshot(snapshot =>
                {
                    snapshot.SupervisionTickCount++;
                    snapshot.LastSupervisionTickUtc = nowUtc;
                    snapshot.DetailText = string.Format(
                        "Standby supervision instrumentation: ticks={0}, txObs={1}, rxObs={2}.",
                        snapshot.SupervisionTickCount,
                        snapshot.SupervisionTxObservedCount,
                        snapshot.SupervisionResponseObservedCount);
                });

                bool sent = await _service.SendLinkLayerTestFunctionAsync().ConfigureAwait(false);

                UpdateSnapshot(snapshot =>
                {
                    if (sent)
                    {
                        snapshot.Connected = true;
                        snapshot.TxCount++;
                        snapshot.SupervisionTxObservedCount++;
                        snapshot.LastSupervisionTxObservedUtc = nowUtc;
                        snapshot.LastActivityUtc = nowUtc;
                        if (!_firstSuccessfulStandbySupervisionUtc.HasValue)
                        {
                            _firstSuccessfulStandbySupervisionUtc = nowUtc;
                        }
                    }

                    bool hasValidResponse = snapshot.LastSupervisionResponseUtc.HasValue;
                    bool initialWindowExpired = _firstSuccessfulStandbySupervisionUtc.HasValue
                        && nowUtc - _firstSuccessfulStandbySupervisionUtc.Value > StandbyInitialResponseWindow;
                    bool responseStale = hasValidResponse
                        && nowUtc - snapshot.LastSupervisionResponseUtc.Value > StandbySupervisionResponseWindow;

                    if (!sent)
                    {
                        snapshot.LastTimeoutUtc = nowUtc;
                        snapshot.Connected = _service.IsConnected;
                        snapshot.State = _service.IsConnected ? NucChannelState.Recovering : NucChannelState.Reopening;
                        snapshot.DetailText = string.Format(
                            "Standby recovery probe pending: ticks={0}, txObs={1}, rxObs={2}. Timer remains armed.",
                            snapshot.SupervisionTickCount,
                            snapshot.SupervisionTxObservedCount,
                            snapshot.SupervisionResponseObservedCount);
                    }
                    else if ((!hasValidResponse && initialWindowExpired && snapshot.SupervisionTxObservedCount >= 2) || responseStale)
                    {
                        snapshot.LastTimeoutUtc = nowUtc;
                        snapshot.Connected = true;
                        snapshot.State = NucChannelState.Timeout;
                        snapshot.DetailText = string.Format(
                            "Standby supervision timeout: ticks={0}, txObs={1}, rxObs={2}. Recovery probes remain armed.",
                            snapshot.SupervisionTickCount,
                            snapshot.SupervisionTxObservedCount,
                            snapshot.SupervisionResponseObservedCount);
                    }
                    else
                    {
                        snapshot.Connected = true;
                        snapshot.State = NucChannelState.StandbySupervision;
                        snapshot.DetailText = string.Format(
                            "Standby supervision active: ticks={0}, txObs={1}, rxObs={2}.",
                            snapshot.SupervisionTickCount,
                            snapshot.SupervisionTxObservedCount,
                            snapshot.SupervisionResponseObservedCount);
                    }
                });

                if (!sent)
                {
                    await RecoverCoreAsync("standby supervision dispatch failed").ConfigureAwait(false);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _standbyTickInFlight, 0);
            }
        }

        private NucChannelSnapshot CreateSnapshot()
        {
            return new NucChannelSnapshot
            {
                ChannelName = Name,
                Role = _role,
                State = NucChannelState.Disconnected,
                StatusText = ConnectionStatusInfo.Disconnected.DisplayText,
                DetailText = string.Empty
            };
        }

        private static NucChannelSnapshot CloneSnapshot(NucChannelSnapshot source)
        {
            return new NucChannelSnapshot
            {
                ChannelName = source.ChannelName,
                Role = source.Role,
                State = source.State,
                Connected = source.Connected,
                RxCount = source.RxCount,
                TxCount = source.TxCount,
                LastResponseUtc = source.LastResponseUtc,
                LastActivityUtc = source.LastActivityUtc,
                LastTimeoutUtc = source.LastTimeoutUtc,
                SupervisionTickCount = source.SupervisionTickCount,
                SupervisionTxObservedCount = source.SupervisionTxObservedCount,
                SupervisionResponseObservedCount = source.SupervisionResponseObservedCount,
                LastSupervisionTickUtc = source.LastSupervisionTickUtc,
                LastSupervisionTxObservedUtc = source.LastSupervisionTxObservedUtc,
                LastSupervisionResponseUtc = source.LastSupervisionResponseUtc,
                StatusText = source.StatusText,
                DetailText = source.DetailText
            };
        }
    }
}
