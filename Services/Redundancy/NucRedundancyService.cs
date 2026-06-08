using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101;

namespace IEC101MasterTester.Services.Redundancy
{
    public sealed class NucRedundancyService : INucRedundancyService
    {
        private readonly object _syncRoot = new object();
        private readonly INucLinkChannel _primaryChannel;
        private readonly INucLinkChannel _backupChannel;
        private NucRedundancySettings _settings;
        private bool _isSessionActive;
        private bool _switchInProgress;
        private string _activeChannel;
        private string _latchedActiveChannel;
        private NucControllerState _controllerState;
        private DateTime _lastSnapshotBroadcastUtc;
        private readonly Timer _healthMonitorTimer;
        private string _lastStatusText;
        private string _lastDetailText;

        public NucRedundancyService()
            : this(
                new NucIec101LinkChannel("Main", new Iec101MasterServiceRouter()),
                new NucIec101LinkChannel("Backup", new Iec101MasterServiceRouter()))
        {
        }

        internal NucRedundancyService(INucLinkChannel primaryChannel, INucLinkChannel backupChannel)
        {
            _primaryChannel = primaryChannel ?? throw new ArgumentNullException(nameof(primaryChannel));
            _backupChannel = backupChannel ?? throw new ArgumentNullException(nameof(backupChannel));
            _activeChannel = "Main";
            _latchedActiveChannel = "Main";
            _controllerState = NucControllerState.NoAvailableLink;
            _lastStatusText = "Idle";
            _lastDetailText = string.Empty;
            _healthMonitorTimer = new Timer(OnHealthMonitorTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            SubscribeChannel(_primaryChannel);
            SubscribeChannel(_backupChannel);
        }

        public event EventHandler<NucRedundancySessionState> SessionStateChanged;
        public event EventHandler<NucRedundancyConnectionEventArgs> ConnectionStateChanged;
        public event EventHandler<NucRedundancyLineMonitorEventArgs> LineMonitorRecordReceived;
        public event EventHandler<NucRedundancyValueEventArgs> ValueReceived;

        public bool IsSessionActive
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isSessionActive;
                }
            }
        }

        public void ApplySettings(NucRedundancySettings settings)
        {
            ConnectionSettings primaryConnectionSettings;
            ConnectionSettings backupConnectionSettings;

            lock (_syncRoot)
            {
                _settings = settings == null
                    ? null
                    : new NucRedundancySettings
                    {
                        BaseConnectionSettings = settings.BaseConnectionSettings == null ? null : settings.BaseConnectionSettings.Clone(),
                        PrimarySerialPort = settings.PrimarySerialPort,
                        BackupSerialPort = settings.BackupSerialPort,
                        RedundancyMode = settings.RedundancyMode,
                        GiPolicy = settings.GiPolicy
                    };
                _activeChannel = "Main";
                _controllerState = NucControllerState.Starting;

                primaryConnectionSettings = CreateChannelSettings(_settings, _settings == null ? null : _settings.PrimarySerialPort);
                backupConnectionSettings = CreateChannelSettings(_settings, _settings == null ? null : _settings.BackupSerialPort);
            }

            _primaryChannel.ApplySettings(primaryConnectionSettings);
            _backupChannel.ApplySettings(backupConnectionSettings);
            RaiseState("Configured", "NUC (Norwegian Users Convention) redundancy settings prepared.");
        }

        public void StartSession()
        {
            lock (_syncRoot)
            {
                _isSessionActive = true;
                _switchInProgress = false;
                _activeChannel = "Main";
                _latchedActiveChannel = "Main";
                _controllerState = NucControllerState.Starting;
            }

            RaiseState("Session Starting", "Starting NUC (Norwegian Users Convention) redundancy session.");
            Task.Run(StartSessionCoreAsync);
        }

        public void StopSession()
        {
            _ = StopSessionAsync();
        }

        public async Task StopSessionAsync()
        {
            lock (_syncRoot)
            {
                _isSessionActive = false;
                _switchInProgress = false;
                _controllerState = NucControllerState.NoAvailableLink;
                _latchedActiveChannel = null;
            }

            RaiseState("Session Stopping", "Stopping NUC redundancy session.");
            await _primaryChannel.StopAsync().ConfigureAwait(false);
            await _backupChannel.StopAsync().ConfigureAwait(false);
            RaiseState("Session Stopped", "NUC redundancy session stopped.");
        }

        public Task SendGeneralInterrogationAsync()
        {
            return SendGeneralInterrogationCoreAsync();
        }

        public Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0)
        {
            return DispatchActiveCommandAsync(
                active => active.SendSingleCommandAsync(ioa, state, select, quality),
                string.Format("Single command {0} IOA {1} sent through active channel.", state ? "ON" : "OFF", ioa));
        }

        public Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0)
        {
            return DispatchActiveCommandAsync(
                active => active.SendDoubleCommandAsync(ioa, on, select, quality),
                string.Format("Double command {0} IOA {1} sent through active channel.", on ? "CLOSE" : "OPEN", ioa));
        }

        public Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0)
        {
            return DispatchActiveCommandAsync(
                active => active.SendStepCommandAsync(ioa, raise, select, quality),
                string.Format("Step command {0} IOA {1} sent through active channel.", raise ? "RAISE" : "LOWER", ioa));
        }

        public Task SendSetpointNormalizedCommandAsync(int ioa, float normalizedValue, bool select = false, int quality = 0)
        {
            return DispatchActiveCommandAsync(
                active => active.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select, quality),
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Setpoint normalized {0:0.###} IOA {1} sent through active channel.",
                    normalizedValue,
                    ioa));
        }

        private async Task SendGeneralInterrogationCoreAsync()
        {
            INucLinkChannel active = GetActiveChannel();
            if (active == null)
            {
                RaiseState("GI skipped", "No active NUC channel is available.");
                return;
            }

            await active.SendGeneralInterrogationAsync().ConfigureAwait(false);
            RaiseState("GI dispatched", "General interrogation sent through " + active.Name + " active channel.");
        }

        private async Task StartSessionCoreAsync()
        {
            try
            {
                if (IsHotStandbyMode())
                {
                    await _primaryChannel.StartAsActiveAsync().ConfigureAwait(false);
                    await _backupChannel.StartAsStandbyAsync().ConfigureAwait(false);
                    lock (_syncRoot)
                    {
                        _activeChannel = "Main";
                        _latchedActiveChannel = "Main";
                        _controllerState = NucControllerState.Healthy;
                    }

                    _primaryChannel.NotifyActiveLinkSwitchover();
                    await DispatchStartupGiAsync().ConfigureAwait(false);
                    RaiseState("Session Active", "NUC hot-standby session started. Main is active, Backup is standby supervision.");
                    return;
                }

                await _primaryChannel.StartAsActiveAsync().ConfigureAwait(false);
                await _backupChannel.StartAsActiveAsync().ConfigureAwait(false);
                lock (_syncRoot)
                {
                    _controllerState = NucControllerState.Healthy;
                    _activeChannel = "Main";
                    _latchedActiveChannel = "Main";
                }

                _primaryChannel.NotifyActiveLinkSwitchover();
                await DispatchStartupGiAsync().ConfigureAwait(false);
                RaiseState("Session Active", "Dual-link session started.");
            }
            catch (Exception ex)
            {
                lock (_syncRoot)
                {
                    _controllerState = NucControllerState.Degraded;
                }

                RaiseState("Session Faulted", ex.Message);
            }
        }

        private async Task DispatchStartupGiAsync()
        {
            ConnectionSettings baseSettings;
            INucLinkChannel active;

            lock (_syncRoot)
            {
                baseSettings = _settings == null ? null : _settings.BaseConnectionSettings;
                active = GetActiveChannel();
            }

            if (baseSettings == null || !baseSettings.UseGeneralInterrogationOnConnect)
            {
                return;
            }

            if (active == null)
            {
                RaiseState("Startup GI skipped", "No active NUC channel is available for initial GI.");
                return;
            }

            await active.SendGeneralInterrogationAsync().ConfigureAwait(false);
            RaiseState("Startup GI dispatched", "Initial general interrogation sent through " + active.Name + " active channel.");
        }

        private Task DispatchActiveCommandAsync(Func<INucLinkChannel, Task> action, string successDetail)
        {
            return DispatchActiveCommandCoreAsync(action, successDetail);
        }

        private async Task DispatchActiveCommandCoreAsync(Func<INucLinkChannel, Task> action, string successDetail)
        {
            INucLinkChannel active = GetActiveChannel();
            if (active == null)
            {
                RaiseState("Command skipped", "No active NUC channel is available.");
                return;
            }

            await action(active).ConfigureAwait(false);
            RaiseState("Command dispatched", successDetail);
        }

        private void OnHealthMonitorTick(object state)
        {
            if (!IsSessionActive)
            {
                return;
            }

            _ = EvaluateControllerAsync(null);
            PublishSessionSnapshot();
        }

        private void SubscribeChannel(INucLinkChannel channel)
        {
            channel.ConnectionStateChanged += (sender, status) =>
            {
                ConnectionStateChanged?.Invoke(this, new NucRedundancyConnectionEventArgs
                {
                    ChannelName = channel.Name,
                    Status = status ?? ConnectionStatusInfo.Faulted
                });

                RaiseState("Channel update", string.Format("{0} link is now {1}.", channel.Name, status == null ? "Unknown" : status.DisplayText));
            };

            channel.LineMonitorRecordReceived += (sender, record) =>
            {
                LineMonitorRecordReceived?.Invoke(this, new NucRedundancyLineMonitorEventArgs
                {
                    ChannelName = channel.Name,
                    Record = record
                });
            };

            channel.ValueReceived += (sender, value) =>
            {
                ValueReceived?.Invoke(this, new NucRedundancyValueEventArgs
                {
                    ChannelName = channel.Name,
                    Value = value
                });
            };

            channel.SnapshotChanged += (sender, snapshot) =>
            {
                _ = EvaluateControllerAsync(snapshot);
                MaybeBroadcastSnapshotState(snapshot);
            };
        }

        private void MaybeBroadcastSnapshotState(NucChannelSnapshot snapshot)
        {
            DateTime nowUtc = DateTime.UtcNow;
            lock (_syncRoot)
            {
                if ((nowUtc - _lastSnapshotBroadcastUtc).TotalMilliseconds < 350)
                {
                    return;
                }

                _lastSnapshotBroadcastUtc = nowUtc;
            }

            RaiseState(
                "Channel Snapshot",
                snapshot == null
                    ? "NUC channel snapshot updated."
                    : string.Format(
                        "{0} | role={1} | state={2} | tx={3} | rx={4}",
                        snapshot.ChannelName,
                        snapshot.Role,
                        snapshot.State,
                        snapshot.TxCount,
                        snapshot.RxCount));
        }

        private async Task EvaluateControllerAsync(NucChannelSnapshot changedSnapshot)
        {
            bool shouldEvaluate;
            bool hotStandby;
            lock (_syncRoot)
            {
                shouldEvaluate = _isSessionActive && !_switchInProgress;
                hotStandby = IsHotStandbyModeLocked();
            }

            if (!shouldEvaluate || !hotStandby)
            {
                return;
            }

            INucLinkChannel active = GetActiveChannel();
            INucLinkChannel standby = GetStandbyChannel();
            if (active == null || standby == null)
            {
                return;
            }

            bool activeIsPrimary = ReferenceEquals(active, _primaryChannel);
            bool standbyIsPrimary = ReferenceEquals(standby, _primaryChannel);
            NucChannelSnapshot activeSnapshot = NormalizeSnapshot(active.Snapshot, activeIsPrimary);
            NucChannelSnapshot standbySnapshot = NormalizeSnapshot(standby.Snapshot, standbyIsPrimary);

            bool activeHealthy = IsOperationallyHealthy(activeSnapshot);
            if (activeHealthy)
            {
                lock (_syncRoot)
                {
                    _controllerState = NucControllerState.Healthy;
                }

                return;
            }

            bool standbyHealthy = IsOperationallyHealthy(standbySnapshot);
            if (!standbyHealthy)
            {
                lock (_syncRoot)
                {
                    _controllerState = NucControllerState.NoAvailableLink;
                }

                return;
            }

            Trace.WriteLine(string.Format(
                "[SWITCH] {0}->{1} PROMOTE_START activeState={2} standbyState={3} activeConn={4} standbyConn={5} activeLastRx={6} standbyLastRx={7}",
                active.Name,
                standby.Name,
                activeSnapshot.State,
                standbySnapshot.State,
                activeSnapshot.Connected ? 1 : 0,
                standbySnapshot.Connected ? 1 : 0,
                activeSnapshot.LastResponseUtc.HasValue ? activeSnapshot.LastResponseUtc.Value.ToString("o") : "-",
                standbySnapshot.LastResponseUtc.HasValue ? standbySnapshot.LastResponseUtc.Value.ToString("o") : "-"));

            lock (_syncRoot)
            {
                if (_switchInProgress)
                {
                    return;
                }

                _switchInProgress = true;
                _controllerState = NucControllerState.Switching;
            }

            try
            {
                await standby.PromoteToActiveAsync().ConfigureAwait(false);
                await active.DemoteToStandbyAsync().ConfigureAwait(false);

                lock (_syncRoot)
                {
                    _activeChannel = standby.Name;
                    _latchedActiveChannel = standby.Name;
                    _controllerState = NucControllerState.Healthy;
                }

                Trace.WriteLine(string.Format(
                    "[SWITCH] {0}->{1} PROMOTE_COMPLETE activeNow={2}",
                    active.Name,
                    standby.Name,
                    GetActiveChannel().Name));

                RaiseState(
                    "Switchover committed",
                    string.Format(
                        "{0} active channel failed ({1}); switched to {2}.",
                        active.Name,
                        activeSnapshot.State,
                        standby.Name));

                standby.NotifyActiveLinkSwitchover();
                await DispatchGiAfterSwitchAsync().ConfigureAwait(false);

                Trace.WriteLine(string.Format(
                    "[SWITCH] {0}->{1} GI_AFTER_PROMOTE dispatched={2}",
                    active.Name,
                    standby.Name,
                    string.Equals(_settings?.GiPolicy, "Required", StringComparison.OrdinalIgnoreCase) ? 1 : 0));
            }
            catch (Exception ex)
            {
                lock (_syncRoot)
                {
                    _controllerState = NucControllerState.Degraded;
                }

                RaiseState("Switch fault", ex.Message);
            }
            finally
            {
                lock (_syncRoot)
                {
                    _switchInProgress = false;
                }
            }
        }

        private async Task DispatchGiAfterSwitchAsync()
        {
            string giPolicy;
            lock (_syncRoot)
            {
                giPolicy = _settings == null ? null : _settings.GiPolicy;
            }

            if (!string.Equals(giPolicy, "Required", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Trace.WriteLine("[GI] post-switch dispatch requested");
            await SendGeneralInterrogationAsync().ConfigureAwait(false);
        }

        private void RaiseState(string statusText, string detailText)
        {
            lock (_syncRoot)
            {
                _lastStatusText = statusText;
                _lastDetailText = detailText;
            }

            PublishSessionSnapshot();
        }

        private void PublishSessionSnapshot()
        {
            NucRedundancySettings snapshot;
            bool isActive;
            string activeChannel;
            NucControllerState controllerState;
            NucChannelSnapshot primarySnapshot;
            NucChannelSnapshot backupSnapshot;
            string statusText;
            string detailText;

            lock (_syncRoot)
            {
                snapshot = _settings == null
                    ? null
                    : new NucRedundancySettings
                    {
                        BaseConnectionSettings = _settings.BaseConnectionSettings == null ? null : _settings.BaseConnectionSettings.Clone(),
                        PrimarySerialPort = _settings.PrimarySerialPort,
                        BackupSerialPort = _settings.BackupSerialPort,
                        RedundancyMode = _settings.RedundancyMode,
                        GiPolicy = _settings.GiPolicy
                     };
                isActive = _isSessionActive;
                activeChannel = string.IsNullOrWhiteSpace(_latchedActiveChannel) ? _activeChannel : _latchedActiveChannel;
                controllerState = _controllerState;
                primarySnapshot = NormalizeSnapshot(_primaryChannel.Snapshot, true);
                backupSnapshot = NormalizeSnapshot(_backupChannel.Snapshot, false);
                statusText = _lastStatusText;
                detailText = _lastDetailText;
            }

            bool primaryHealthy = IsOperationallyHealthy(primarySnapshot);
            bool backupHealthy = IsOperationallyHealthy(backupSnapshot);
            if (!primaryHealthy && !backupHealthy)
            {
                activeChannel = "-";
                if (isActive)
                {
                    controllerState = NucControllerState.NoAvailableLink;
                }
            }

            SessionStateChanged?.Invoke(this, new NucRedundancySessionState
            {
                IsActive = isActive,
                StatusText = statusText,
                DetailText = detailText,
                Settings = snapshot,
                PrimaryStatusText = primarySnapshot == null ? "-" : primarySnapshot.StatusText,
                BackupStatusText = backupSnapshot == null ? "-" : backupSnapshot.StatusText,
                ActiveChannel = activeChannel,
                ControllerState = controllerState.ToString(),
                PrimaryRole = primarySnapshot == null ? "-" : primarySnapshot.Role.ToString(),
                BackupRole = backupSnapshot == null ? "-" : backupSnapshot.Role.ToString(),
                PrimaryChannelState = primarySnapshot == null ? "-" : primarySnapshot.State.ToString(),
                BackupChannelState = backupSnapshot == null ? "-" : backupSnapshot.State.ToString(),
                PrimaryRxCount = primarySnapshot == null ? 0 : primarySnapshot.RxCount,
                PrimaryTxCount = primarySnapshot == null ? 0 : primarySnapshot.TxCount,
                BackupRxCount = backupSnapshot == null ? 0 : backupSnapshot.RxCount,
                BackupTxCount = backupSnapshot == null ? 0 : backupSnapshot.TxCount,
                PrimarySupervisionTickCount = primarySnapshot == null ? 0 : primarySnapshot.SupervisionTickCount,
                PrimarySupervisionTxObservedCount = primarySnapshot == null ? 0 : primarySnapshot.SupervisionTxObservedCount,
                PrimarySupervisionResponseObservedCount = primarySnapshot == null ? 0 : primarySnapshot.SupervisionResponseObservedCount,
                BackupSupervisionTickCount = backupSnapshot == null ? 0 : backupSnapshot.SupervisionTickCount,
                BackupSupervisionTxObservedCount = backupSnapshot == null ? 0 : backupSnapshot.SupervisionTxObservedCount,
                BackupSupervisionResponseObservedCount = backupSnapshot == null ? 0 : backupSnapshot.SupervisionResponseObservedCount,
                PrimaryLastActivityUtcText = primarySnapshot != null && primarySnapshot.LastActivityUtc.HasValue ? primarySnapshot.LastActivityUtc.Value.ToString("o") : string.Empty,
                BackupLastActivityUtcText = backupSnapshot != null && backupSnapshot.LastActivityUtc.HasValue ? backupSnapshot.LastActivityUtc.Value.ToString("o") : string.Empty,
                PrimaryLastResponseUtcText = primarySnapshot != null && primarySnapshot.LastResponseUtc.HasValue ? primarySnapshot.LastResponseUtc.Value.ToString("o") : string.Empty,
                BackupLastResponseUtcText = backupSnapshot != null && backupSnapshot.LastResponseUtc.HasValue ? backupSnapshot.LastResponseUtc.Value.ToString("o") : string.Empty
            });
        }

        private INucLinkChannel GetActiveChannel()
        {
            lock (_syncRoot)
            {
                return string.Equals(_activeChannel, "Backup", StringComparison.OrdinalIgnoreCase)
                    ? _backupChannel
                    : _primaryChannel;
            }
        }

        private INucLinkChannel GetStandbyChannel()
        {
            lock (_syncRoot)
            {
                return string.Equals(_activeChannel, "Backup", StringComparison.OrdinalIgnoreCase)
                    ? _primaryChannel
                    : _backupChannel;
            }
        }

        private bool IsOperationallyHealthy(NucChannelSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            return snapshot.State == NucChannelState.Responsive
                || snapshot.State == NucChannelState.StandbySupervision
                || snapshot.State == NucChannelState.ConnectedNoResponse;
        }

        private NucChannelSnapshot NormalizeSnapshot(NucChannelSnapshot snapshot, bool isPrimary)
        {
            if (snapshot == null)
            {
                return null;
            }

            NucChannelSnapshot normalized = CloneSnapshot(snapshot);
            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan activeResponseWindow = GetActiveResponseTimeoutWindow();

            if (!normalized.Connected)
            {
                normalized.State = NucChannelState.FaultLatched;
                return normalized;
            }

            if (normalized.Role == NucChannelRole.Active)
            {
                bool responseStale = normalized.LastResponseUtc.HasValue
                    && nowUtc - normalized.LastResponseUtc.Value > activeResponseWindow;
                bool startupNoResponse = !normalized.LastResponseUtc.HasValue
                    && normalized.LastActivityUtc.HasValue
                    && normalized.TxCount > 0
                    && nowUtc - normalized.LastActivityUtc.Value > activeResponseWindow;

                if (responseStale || startupNoResponse)
                {
                    normalized.State = NucChannelState.Timeout;
                    normalized.LastTimeoutUtc = nowUtc;
                }
            }
            else if (normalized.Role == NucChannelRole.Standby)
            {
                TimeSpan standbyResponseWindow = TimeSpan.FromSeconds(8);
                bool responseStale = normalized.LastSupervisionTxObservedUtc.HasValue
                    && (!normalized.LastSupervisionResponseUtc.HasValue
                        || nowUtc - normalized.LastSupervisionResponseUtc.Value > standbyResponseWindow);

                if (responseStale && normalized.SupervisionTxObservedCount >= 2)
                {
                    normalized.State = NucChannelState.Timeout;
                    normalized.LastTimeoutUtc = nowUtc;
                }
            }

            return normalized;
        }

        private TimeSpan GetActiveResponseTimeoutWindow()
        {
            ConnectionSettings settings = _settings == null ? null : _settings.BaseConnectionSettings;
            int responseTimeoutMs = settings != null ? settings.ResponseTimeoutMs : 1000;
            double timeoutMs = Math.Max(3000, responseTimeoutMs * 4);
            return TimeSpan.FromMilliseconds(timeoutMs);
        }

        private static NucChannelSnapshot CloneSnapshot(NucChannelSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

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

        private bool IsHotStandbyMode()
        {
            lock (_syncRoot)
            {
                return IsHotStandbyModeLocked();
            }
        }

        private bool IsHotStandbyModeLocked()
        {
            return _settings == null
                || string.IsNullOrWhiteSpace(_settings.RedundancyMode)
                || _settings.RedundancyMode.IndexOf("Hot", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ConnectionSettings CreateChannelSettings(NucRedundancySettings settings, string serialPort)
        {
            if (settings == null || settings.BaseConnectionSettings == null || string.IsNullOrWhiteSpace(serialPort))
            {
                return null;
            }

            ConnectionSettings channelSettings = settings.BaseConnectionSettings.Clone();
            channelSettings.SerialPort = serialPort;
            channelSettings.UseGeneralInterrogationOnConnect = settings.BaseConnectionSettings.UseGeneralInterrogationOnConnect;
            channelSettings.ChannelOperationMode = Iec101ChannelOperationMode.FullActive;
            return channelSettings;
        }
    }
}
