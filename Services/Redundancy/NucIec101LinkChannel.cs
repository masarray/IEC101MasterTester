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
        private DateTime? _standbyStartedAtUtc;
        private DateTime? _firstSuccessfulStandbySupervisionUtc;
        private static readonly TimeSpan StandbySupervisionInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan StandbyInitialResponseWindow = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan StandbySupervisionResponseWindow = TimeSpan.FromSeconds(8);

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
            }

            if (settings == null)
            {
                return;
            }

            settings.ChannelOperationMode = role == NucChannelRole.Active
                ? Iec101ChannelOperationMode.FullActive
                : Iec101ChannelOperationMode.StandbySupervision;
            settings.UseGeneralInterrogationOnConnect = role == NucChannelRole.Active
                && settings.UseGeneralInterrogationOnConnect;

            StopStandbySupervisionTimer();
            await _service.DisconnectAsync().ConfigureAwait(false);
            _service.ApplySettings(settings);
            await _service.ConnectAsync().ConfigureAwait(false);

            UpdateSnapshot(snapshot =>
            {
                snapshot.Role = role;
                snapshot.State = role == NucChannelRole.Active ? NucChannelState.ConnectedNoResponse : NucChannelState.StandbySupervision;
            });

            if (role == NucChannelRole.Standby)
            {
                StartStandbySupervisionTimer();
            }
        }

        private void Service_ConnectionStateChanged(object sender, ConnectionStatusInfo e)
        {
            ConnectionStateChanged?.Invoke(this, e);

            UpdateSnapshot(snapshot =>
            {
                snapshot.StatusText = e == null ? "Unknown" : e.DisplayText;
                snapshot.DetailText = e == null ? string.Empty : e.Detail;
                bool isConnected = e != null
                    && string.Equals(e.DisplayText, ConnectionStatusInfo.Connected.DisplayText, StringComparison.OrdinalIgnoreCase);
                snapshot.Connected = isConnected;
                if (!isConnected)
                {
                    snapshot.State = NucChannelState.FaultLatched;
                    _faultLatched = true;
                }
                else if (snapshot.Role == NucChannelRole.Standby)
                {
                    snapshot.LastTimeoutUtc = null;
                    if (!snapshot.LastActivityUtc.HasValue)
                    {
                        snapshot.LastActivityUtc = DateTime.UtcNow;
                    }
                    snapshot.State = NucChannelState.StandbySupervision;
                }
                else if (!_faultLatched)
                {
                    snapshot.LastTimeoutUtc = null;
                    snapshot.State = NucChannelState.ConnectedNoResponse;
                }
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
                    snapshot.Connected = false;
                    if (snapshot.Role == NucChannelRole.Active)
                    {
                        _faultLatched = true;
                    }
                    snapshot.State = NucChannelState.Timeout;
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

            bool explicitTransportFailure =
                summary.IndexOf("standby supervision timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("standby supervision timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("no response", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("no response", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("serial port", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("serial port", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("port closed", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("port closed", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("worker error", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("worker error", StringComparison.OrdinalIgnoreCase) >= 0
                || (string.Equals(frameType, "Error", StringComparison.OrdinalIgnoreCase)
                    && (summary.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                        || summary.IndexOf("read", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("read", StringComparison.OrdinalIgnoreCase) >= 0
                        || summary.IndexOf("connect", StringComparison.OrdinalIgnoreCase) >= 0
                        || detail.IndexOf("connect", StringComparison.OrdinalIgnoreCase) >= 0));

            return explicitTransportFailure;
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
                    if (!snapshot.Connected)
                    {
                        snapshot.State = NucChannelState.FaultLatched;
                        snapshot.LastTimeoutUtc = nowUtc;
                        snapshot.DetailText = "Standby supervision cannot run because link is not connected.";
                        return;
                    }

                    if (sent)
                    {
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
                        if (_firstSuccessfulStandbySupervisionUtc.HasValue
                            && nowUtc - _firstSuccessfulStandbySupervisionUtc.Value > StandbyInitialResponseWindow)
                        {
                            snapshot.LastTimeoutUtc = nowUtc;
                            snapshot.Connected = false;
                            snapshot.State = NucChannelState.Timeout;
                            snapshot.DetailText = string.Format(
                                "Standby supervision timeout: ticks={0}, txObs={1}, rxObs={2}.",
                                snapshot.SupervisionTickCount,
                                snapshot.SupervisionTxObservedCount,
                                snapshot.SupervisionResponseObservedCount);
                        }
                        else
                        {
                            snapshot.DetailText = string.Format(
                                "Standby supervision tick ran, but link-layer test dispatch did not complete. ticks={0}, txObs={1}, rxObs={2}.",
                                snapshot.SupervisionTickCount,
                                snapshot.SupervisionTxObservedCount,
                                snapshot.SupervisionResponseObservedCount);
                            snapshot.State = NucChannelState.ConnectedNoResponse;
                        }
                    }
                    else if ((!hasValidResponse && initialWindowExpired && snapshot.SupervisionTxObservedCount >= 2) || responseStale)
                    {
                        snapshot.LastTimeoutUtc = nowUtc;
                        snapshot.Connected = false;
                        snapshot.State = NucChannelState.Timeout;
                        snapshot.DetailText = string.Format(
                            "Standby supervision timeout: ticks={0}, txObs={1}, rxObs={2}.",
                            snapshot.SupervisionTickCount,
                            snapshot.SupervisionTxObservedCount,
                            snapshot.SupervisionResponseObservedCount);
                    }
                    else
                    {
                        snapshot.State = NucChannelState.StandbySupervision;
                        snapshot.DetailText = string.Format(
                            "Standby supervision active: ticks={0}, txObs={1}, rxObs={2}.",
                            snapshot.SupervisionTickCount,
                            snapshot.SupervisionTxObservedCount,
                            snapshot.SupervisionResponseObservedCount);
                    }
                });

                if (!sent && _firstSuccessfulStandbySupervisionUtc.HasValue
                    && nowUtc - _firstSuccessfulStandbySupervisionUtc.Value > StandbyInitialResponseWindow)
                {
                    StopStandbySupervisionTimer();
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
