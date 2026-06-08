using System;
using System.Collections.Generic;
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
        private string _lastFailoverFrom;
        private string _lastFailoverTo;
        private DateTime? _lastFailoverStartedUtc;
        private DateTime? _lastFailoverCompletedUtc;
        private double _lastFailoverLatencyMs;
        private DateTime? _lastControllerRecoveryUtc;
        private int _controllerRecoveryInFlight;
        private readonly HashSet<int> _applicationImageIoas = new HashSet<int>();
        private readonly HashSet<int> _giImageIoas = new HashSet<int>();
        private int _backgroundOnlyObjectCount;
        private NucApplicationImageState _applicationImageState;
        private string _applicationImageDetail;
        private string _lastGiStatus;
        private DateTime? _lastGiDispatchUtc;
        private DateTime? _lastApplicationValueUtc;
        private int _bootstrapAttemptCount;
        private int _bootstrapInFlight;

        private static readonly TimeSpan ControllerRecoveryInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan ApplicationImageFreshWindow = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan BootstrapInitialDelay = TimeSpan.FromMilliseconds(80);
        private static readonly TimeSpan BootstrapResponseWindow = TimeSpan.FromMilliseconds(2300);
        private static readonly TimeSpan BootstrapRetryCooldown = TimeSpan.FromSeconds(5);

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
            _applicationImageState = NucApplicationImageState.Empty;
            _applicationImageDetail = "No application image has been acquired yet.";
            _lastGiStatus = "Never";
            _healthMonitorTimer = new Timer(OnHealthMonitorTick, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

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
                _lastFailoverFrom = null;
                _lastFailoverTo = null;
                _lastFailoverStartedUtc = null;
                _lastFailoverCompletedUtc = null;
                _lastFailoverLatencyMs = 0d;
                _lastControllerRecoveryUtc = null;
                _controllerRecoveryInFlight = 0;
                _applicationImageIoas.Clear();
                _giImageIoas.Clear();
                _backgroundOnlyObjectCount = 0;
                _applicationImageState = NucApplicationImageState.Empty;
                _applicationImageDetail = "Cold application image. Startup GI/bootstrap is required before the Value Viewer can be considered ready.";
                _lastGiStatus = "Never";
                _lastGiDispatchUtc = null;
                _lastApplicationValueUtc = null;
                _bootstrapAttemptCount = 0;
                _bootstrapInFlight = 0;
            }

            RaiseState("Session Starting", "Starting NUC (Norwegian Users Convention) redundancy session. Application image is empty and will be bootstrapped.");
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
                _applicationImageIoas.Clear();
                _giImageIoas.Clear();
                _backgroundOnlyObjectCount = 0;
                _applicationImageState = NucApplicationImageState.Empty;
                _applicationImageDetail = "NUC session is stopped.";
                _lastGiStatus = "Stopped";
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

        private Task SendGeneralInterrogationCoreAsync()
        {
            return DispatchTrackedGiAsync("Manual GI", "User requested general interrogation.", true);
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
                        _controllerState = NucControllerState.Bootstrapping;
                    }

                    RaiseBootstrapEvent("Startup elected", "Main elected active, Backup is standby. Cold image bootstrap will run before normal Class 2 polling is considered operational.");
                    StartApplicationBootstrap("cold start hot-standby election");
                    RaiseState("Application Bootstrapping", "NUC hot-standby links are open. Building application image with startup GI.");
                    return;
                }

                await _primaryChannel.StartAsActiveAsync().ConfigureAwait(false);
                await _backupChannel.StartAsActiveAsync().ConfigureAwait(false);
                lock (_syncRoot)
                {
                    _controllerState = NucControllerState.Bootstrapping;
                    _activeChannel = "Main";
                    _latchedActiveChannel = "Main";
                }

                RaiseBootstrapEvent("Startup elected", "Main elected as preferred active channel. Cold image bootstrap will run before normal Class 2 polling is considered operational.");
                StartApplicationBootstrap("cold start dual-link election");
                RaiseState("Application Bootstrapping", "Dual-link transport is open. Building application image with startup GI.");
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

        private void StartApplicationBootstrap(string reason)
        {
            lock (_syncRoot)
            {
                if (_lastGiDispatchUtc.HasValue
                    && DateTime.UtcNow - _lastGiDispatchUtc.Value < BootstrapRetryCooldown
                    && (_applicationImageState == NucApplicationImageState.Partial || _applicationImageState == NucApplicationImageState.Failed))
                {
                    return;
                }
            }

            if (Interlocked.Exchange(ref _bootstrapInFlight, 1) != 0)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(BootstrapInitialDelay).ConfigureAwait(false);
                    for (int attempt = 1; attempt <= 2; attempt++)
                    {
                        if (!IsSessionActive || IsApplicationImageFresh())
                        {
                            return;
                        }

                        await DispatchTrackedGiAsync("Startup GI", reason + " | attempt " + attempt.ToString(System.Globalization.CultureInfo.InvariantCulture), false).ConfigureAwait(false);
                        await Task.Delay(BootstrapResponseWindow).ConfigureAwait(false);
                    }

                    if (!IsApplicationImageFresh())
                    {
                        lock (_syncRoot)
                        {
                            _applicationImageState = _applicationImageIoas.Count > 0 ? NucApplicationImageState.Partial : NucApplicationImageState.Failed;
                            _applicationImageDetail = _applicationImageIoas.Count > 0
                                ? "Startup GI produced a partial image. Normal polling remains active; use Send GI to refresh."
                                : "Link layer is responsive, but no application value has been received after startup GI retries.";
                            _lastGiStatus = _applicationImageIoas.Count > 0 ? "Partial" : "No application data";
                            _controllerState = _applicationImageIoas.Count > 0 ? NucControllerState.Degraded : NucControllerState.Degraded;
                        }

                        RaiseBootstrapEvent(
                            _applicationImageIoas.Count > 0 ? "Bootstrap partial" : "Bootstrap failed",
                            _applicationImageDetail);
                        PublishSessionSnapshot();
                    }
                }
                catch (Exception ex)
                {
                    lock (_syncRoot)
                    {
                        _applicationImageState = NucApplicationImageState.Failed;
                        _applicationImageDetail = "Startup bootstrap failed: " + ex.Message;
                        _lastGiStatus = "Bootstrap exception";
                        _controllerState = NucControllerState.Degraded;
                    }

                    RaiseBootstrapEvent("Bootstrap exception", ex.Message);
                    PublishSessionSnapshot();
                }
                finally
                {
                    Interlocked.Exchange(ref _bootstrapInFlight, 0);
                }
            });
        }

        private async Task DispatchTrackedGiAsync(string giKind, string reason, bool manual)
        {
            INucLinkChannel active = GetActiveChannel();
            if (active == null)
            {
                RaiseState("GI skipped", "No active NUC channel is available.");
                RaiseBootstrapEvent("GI skipped", "No active NUC channel is available.");
                return;
            }

            lock (_syncRoot)
            {
                _bootstrapAttemptCount++;
                _lastGiDispatchUtc = DateTime.UtcNow;
                _lastGiStatus = giKind + " dispatched";
                if (_applicationImageState == NucApplicationImageState.Empty
                    || _applicationImageState == NucApplicationImageState.Failed
                    || _applicationImageState == NucApplicationImageState.Stale)
                {
                    _applicationImageState = NucApplicationImageState.Bootstrapping;
                }

                _applicationImageDetail = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0} sent through {1}. Waiting for application values. Reason: {2}",
                    giKind,
                    active.Name,
                    string.IsNullOrWhiteSpace(reason) ? "-" : reason);
                _controllerState = NucControllerState.Bootstrapping;
            }

            RaiseBootstrapEvent(giKind + " dispatched", _applicationImageDetail);
            await active.SendGeneralInterrogationAsync().ConfigureAwait(false);
            PublishSessionSnapshot();

            if (manual)
            {
                RaiseState("GI dispatched", "General interrogation sent through " + active.Name + " active channel.");
            }
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
                ObserveApplicationBootstrapLine(channel.Name, record);
                LineMonitorRecordReceived?.Invoke(this, new NucRedundancyLineMonitorEventArgs
                {
                    ChannelName = channel.Name,
                    Record = record
                });
            };

            channel.ValueReceived += (sender, value) =>
            {
                RegisterApplicationValue(value);
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
            bool standbyHealthy = IsOperationallyHealthy(standbySnapshot);
            if (activeHealthy)
            {
                bool imageFresh = IsApplicationImageFresh();
                bool imageEmpty = IsApplicationImageEmpty();
                if (!imageFresh)
                {
                    lock (_syncRoot)
                    {
                        _controllerState = imageEmpty || _applicationImageState == NucApplicationImageState.Bootstrapping
                            ? NucControllerState.Bootstrapping
                            : NucControllerState.Degraded;
                    }

                    if (imageEmpty
                        || _applicationImageState == NucApplicationImageState.Failed
                        || _applicationImageState == NucApplicationImageState.Stale
                        || _applicationImageState == NucApplicationImageState.Partial)
                    {
                        StartApplicationBootstrap("active link is healthy but GI application image is not ready");
                    }
                }
                else if (standbyHealthy)
                {
                    lock (_syncRoot)
                    {
                        _controllerState = NucControllerState.Healthy;
                    }
                }
                else
                {
                    lock (_syncRoot)
                    {
                        _controllerState = NucControllerState.Degraded;
                    }

                    TryScheduleRecovery(standby, "active healthy but standby is not ready");
                }

                return;
            }

            if (!standbyHealthy)
            {
                lock (_syncRoot)
                {
                    _controllerState = NucControllerState.Recovering;
                }

                TryScheduleRecoveryPair(
                    active,
                    "active link unhealthy and no standby is ready",
                    standby,
                    "standby link unhealthy and cannot take over");
                RaiseState(
                    "Recovering links",
                    string.Format(
                        "No viable NUC link. Main={0}/{1}, Backup={2}/{3}. Recovery probes remain armed.",
                        activeSnapshot.ChannelName,
                        activeSnapshot.State,
                        standbySnapshot.ChannelName,
                        standbySnapshot.State));
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

            DateTime failoverStartedUtc = DateTime.UtcNow;
            lock (_syncRoot)
            {
                _lastFailoverFrom = active.Name;
                _lastFailoverTo = standby.Name;
                _lastFailoverStartedUtc = failoverStartedUtc;
                _lastFailoverCompletedUtc = null;
                _lastFailoverLatencyMs = 0d;
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
                    _lastFailoverCompletedUtc = DateTime.UtcNow;
                    _lastFailoverLatencyMs = _lastFailoverStartedUtc.HasValue
                        ? (_lastFailoverCompletedUtc.Value - _lastFailoverStartedUtc.Value).TotalMilliseconds
                        : 0d;
                }

                Trace.WriteLine(string.Format(
                    "[SWITCH] {0}->{1} PROMOTE_COMPLETE activeNow={2}",
                    active.Name,
                    standby.Name,
                    GetActiveChannel().Name));

                RaiseState(
                    "Switchover committed",
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0} active channel failed ({1}); switched to {2} in {3:0} ms.",
                        active.Name,
                        activeSnapshot.State,
                        standby.Name,
                        _lastFailoverLatencyMs));

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
            bool imageFresh;
            lock (_syncRoot)
            {
                giPolicy = _settings == null ? null : _settings.GiPolicy;
                imageFresh = IsApplicationImageFreshLocked(DateTime.UtcNow);
            }

            if (!string.Equals(giPolicy, "Required", StringComparison.OrdinalIgnoreCase) && imageFresh)
            {
                RaiseBootstrapEvent("Post-switch GI skipped", "GI policy is optional and the application image is still fresh.");
                return;
            }

            Trace.WriteLine("[GI] post-switch dispatch requested");
            await DispatchTrackedGiAsync("Post-switch GI", imageFresh ? "GI policy required" : "application image stale/empty after switchover", false).ConfigureAwait(false);
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
            string lastFailoverFrom;
            string lastFailoverTo;
            DateTime? lastFailoverCompletedUtc;
            double lastFailoverLatencyMs;
            NucApplicationImageState applicationImageState;
            string applicationImageDetail;
            int applicationObjectCount;
            string lastGiStatus;
            DateTime? lastGiDispatchUtc;
            int bootstrapAttemptCount;

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
                lastFailoverFrom = _lastFailoverFrom;
                lastFailoverTo = _lastFailoverTo;
                lastFailoverCompletedUtc = _lastFailoverCompletedUtc;
                lastFailoverLatencyMs = _lastFailoverLatencyMs;
                applicationImageState = GetEffectiveApplicationImageStateLocked(DateTime.UtcNow);
                applicationImageDetail = _applicationImageDetail;
                applicationObjectCount = _applicationImageIoas.Count;
                lastGiStatus = _lastGiStatus;
                lastGiDispatchUtc = _lastGiDispatchUtc;
                bootstrapAttemptCount = _bootstrapAttemptCount;
            }

            bool primaryHealthy = IsOperationallyHealthy(primarySnapshot);
            bool backupHealthy = IsOperationallyHealthy(backupSnapshot);
            if (!primaryHealthy && !backupHealthy && isActive)
            {
                controllerState = controllerState == NucControllerState.Switching
                    ? NucControllerState.Switching
                    : NucControllerState.Recovering;
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
                BackupLastResponseUtcText = backupSnapshot != null && backupSnapshot.LastResponseUtc.HasValue ? backupSnapshot.LastResponseUtc.Value.ToString("o") : string.Empty,
                PrimaryLastTimeoutUtcText = primarySnapshot != null && primarySnapshot.LastTimeoutUtc.HasValue ? primarySnapshot.LastTimeoutUtc.Value.ToString("o") : string.Empty,
                BackupLastTimeoutUtcText = backupSnapshot != null && backupSnapshot.LastTimeoutUtc.HasValue ? backupSnapshot.LastTimeoutUtc.Value.ToString("o") : string.Empty,
                LastFailoverFrom = lastFailoverFrom ?? string.Empty,
                LastFailoverTo = lastFailoverTo ?? string.Empty,
                LastFailoverAtUtcText = lastFailoverCompletedUtc.HasValue ? lastFailoverCompletedUtc.Value.ToString("o") : string.Empty,
                LastFailoverLatencyMs = lastFailoverLatencyMs,
                ApplicationImageState = applicationImageState.ToString(),
                ApplicationImageDetail = applicationImageDetail ?? string.Empty,
                ApplicationObjectCount = applicationObjectCount,
                LastGiStatus = lastGiStatus ?? string.Empty,
                LastGiAtUtcText = lastGiDispatchUtc.HasValue ? lastGiDispatchUtc.Value.ToString("o") : string.Empty,
                BootstrapAttemptCount = bootstrapAttemptCount
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

        private bool IsApplicationImageEmpty()
        {
            lock (_syncRoot)
            {
                return _applicationImageIoas.Count == 0;
            }
        }

        private bool IsApplicationImageFresh()
        {
            lock (_syncRoot)
            {
                return IsApplicationImageFreshLocked(DateTime.UtcNow);
            }
        }

        private bool IsApplicationImageFreshLocked(DateTime nowUtc)
        {
            return _applicationImageState == NucApplicationImageState.Ready
                && _giImageIoas.Count > 0
                && _lastApplicationValueUtc.HasValue
                && nowUtc - _lastApplicationValueUtc.Value <= ApplicationImageFreshWindow;
        }

        private NucApplicationImageState GetEffectiveApplicationImageStateLocked(DateTime nowUtc)
        {
            if (_applicationImageState == NucApplicationImageState.Ready
                && _lastApplicationValueUtc.HasValue
                && nowUtc - _lastApplicationValueUtc.Value > ApplicationImageFreshWindow)
            {
                _applicationImageState = NucApplicationImageState.Stale;
                _applicationImageDetail = "Application image is stale; no fresh GI/application value has been received in the freshness window.";
            }

            return _applicationImageState;
        }

        private void RegisterApplicationValue(ValueViewerRow value)
        {
            if (value == null)
            {
                return;
            }

            int objectCount;
            int giObjectCount;
            bool looksLikeGi;
            bool looksLikeBackground;
            string cot = value.Cot ?? string.Empty;
            string source = value.SourceType ?? string.Empty;
            lock (_syncRoot)
            {
                _applicationImageIoas.Add(value.IOA);
                _lastApplicationValueUtc = DateTime.UtcNow;
                objectCount = _applicationImageIoas.Count;

                looksLikeGi = cot.IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0
                    || cot.IndexOf("Interrog", StringComparison.OrdinalIgnoreCase) >= 0
                    || source.IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0;
                looksLikeBackground = cot.IndexOf("BgScan", StringComparison.OrdinalIgnoreCase) >= 0
                    || cot.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0
                    || source.IndexOf("C2", StringComparison.OrdinalIgnoreCase) >= 0;

                if (looksLikeGi)
                {
                    _giImageIoas.Add(value.IOA);
                }
                else if (looksLikeBackground)
                {
                    _backgroundOnlyObjectCount++;
                }

                giObjectCount = _giImageIoas.Count;

                if (looksLikeGi && giObjectCount > 0)
                {
                    _applicationImageState = NucApplicationImageState.Ready;
                    _applicationImageDetail = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "GI application image is ready with {0} GI object(s), {1} total object(s). Last IOA {2}, COT {3}.",
                        giObjectCount,
                        objectCount,
                        value.IOA,
                        string.IsNullOrWhiteSpace(cot) ? "-" : cot);
                    _lastGiStatus = "GI data received";
                    if (_controllerState == NucControllerState.Bootstrapping || _controllerState == NucControllerState.Degraded)
                    {
                        _controllerState = NucControllerState.Healthy;
                    }
                }
                else
                {
                    if (_applicationImageState == NucApplicationImageState.Empty || _applicationImageState == NucApplicationImageState.Bootstrapping)
                    {
                        _applicationImageState = NucApplicationImageState.Partial;
                    }

                    _applicationImageDetail = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Only non-GI application traffic has been received so far ({0} total object(s), {1} background/C2 object(s)). Startup bootstrap remains incomplete until GI/interrogated data is observed.",
                        objectCount,
                        _backgroundOnlyObjectCount);
                    _lastGiStatus = looksLikeBackground ? "Background data only" : "Non-GI data received";
                }
            }

            if (objectCount == 1)
            {
                RaiseBootstrapEvent("First application value", _applicationImageDetail);
            }

            if (looksLikeGi && giObjectCount == 1)
            {
                RaiseBootstrapEvent("GI image started", _applicationImageDetail);
            }
            else if (looksLikeGi && giObjectCount == 3)
            {
                RaiseBootstrapEvent("GI application image ready", _applicationImageDetail);
            }
            else if (!looksLikeGi && objectCount == 3)
            {
                RaiseBootstrapEvent("Background-only image", _applicationImageDetail);
            }

            PublishSessionSnapshot();
        }

        private void ObserveApplicationBootstrapLine(string channelName, LineMonitorRow record)
        {
            if (record == null)
            {
                return;
            }

            string summary = record.Summary ?? string.Empty;
            string detail = record.Detail ?? string.Empty;
            bool isGi = summary.IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("C_IC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("interrogation", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isGi)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (summary.IndexOf("queued", StringComparison.OrdinalIgnoreCase) >= 0
                    || summary.IndexOf("sent", StringComparison.OrdinalIgnoreCase) >= 0
                    || summary.IndexOf("dispatch", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _lastGiStatus = "GI queued/sent on " + (channelName ?? "active link");
                    if (_giImageIoas.Count == 0)
                    {
                        _applicationImageState = NucApplicationImageState.Bootstrapping;
                    }
                }

                if ((record.COT ?? string.Empty).IndexOf("ActivationTermination", StringComparison.OrdinalIgnoreCase) >= 0
                    || summary.IndexOf("ActivationTermination", StringComparison.OrdinalIgnoreCase) >= 0
                    || detail.IndexOf("ActivationTermination", StringComparison.OrdinalIgnoreCase) >= 0
                    || (record.COT ?? string.Empty).IndexOf("ActTerm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _lastGiStatus = "GI activation terminated";
                    if (_giImageIoas.Count > 0)
                    {
                        _applicationImageState = NucApplicationImageState.Ready;
                        _applicationImageDetail = "GI activation termination observed after GI data. Application image is ready.";
                    }
                }
            }
        }

        private void RaiseBootstrapEvent(string summary, string detail)
        {
            LineMonitorRecordReceived?.Invoke(this, new NucRedundancyLineMonitorEventArgs
            {
                ChannelName = "System",
                Record = new LineMonitorRow
                {
                    Time = DateTime.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
                    Channel = "NUC",
                    Direction = "STATE",
                    FrameType = "Bootstrap",
                    Summary = summary ?? "Bootstrap",
                    ControlFc = "-",
                    ACD = "-",
                    DFC = "-",
                    AsduType = "-",
                    COT = "-",
                    CASDU = "-",
                    IOA = "-",
                    RawHex = string.Empty,
                    Detail = detail ?? string.Empty,
                    DataClass = "System"
                }
            });
        }

        private bool IsOperationallyHealthy(NucChannelSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.Connected)
            {
                return false;
            }

            if (snapshot.State == NucChannelState.Responsive)
            {
                return true;
            }

            if (snapshot.State == NucChannelState.StandbySupervision)
            {
                return snapshot.LastSupervisionResponseUtc.HasValue
                    || snapshot.LastResponseUtc.HasValue
                    || snapshot.RxCount > 0;
            }

            if (snapshot.State == NucChannelState.ConnectedNoResponse)
            {
                // Freshly opened active channels need a short grace window before being declared failed.
                DateTime nowUtc = DateTime.UtcNow;
                return !snapshot.LastActivityUtc.HasValue
                    || nowUtc - snapshot.LastActivityUtc.Value < GetActiveResponseTimeoutWindow();
            }

            return false;
        }

        private void TryScheduleRecoveryPair(INucLinkChannel first, string firstReason, INucLinkChannel second, string secondReason)
        {
            DateTime nowUtc = DateTime.UtcNow;
            lock (_syncRoot)
            {
                if (_controllerRecoveryInFlight != 0)
                {
                    return;
                }

                if (_lastControllerRecoveryUtc.HasValue && nowUtc - _lastControllerRecoveryUtc.Value < ControllerRecoveryInterval)
                {
                    return;
                }

                _lastControllerRecoveryUtc = nowUtc;
                _controllerRecoveryInFlight = 1;
            }

            Task.Run(async () =>
            {
                try
                {
                    if (first != null)
                    {
                        RaiseState("Recovery probe", first.Name + ": " + firstReason);
                        await first.RecoverAsync(firstReason).ConfigureAwait(false);
                    }

                    if (second != null && !ReferenceEquals(second, first))
                    {
                        RaiseState("Recovery probe", second.Name + ": " + secondReason);
                        await second.RecoverAsync(secondReason).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    RaiseState("Recovery probe failed", ex.Message);
                }
                finally
                {
                    lock (_syncRoot)
                    {
                        _controllerRecoveryInFlight = 0;
                    }
                }
            });
        }

        private void TryScheduleRecovery(INucLinkChannel channel, string reason)
        {
            if (channel == null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            lock (_syncRoot)
            {
                if (_controllerRecoveryInFlight != 0)
                {
                    return;
                }

                if (_lastControllerRecoveryUtc.HasValue && nowUtc - _lastControllerRecoveryUtc.Value < ControllerRecoveryInterval)
                {
                    return;
                }

                _lastControllerRecoveryUtc = nowUtc;
                _controllerRecoveryInFlight = 1;
            }

            Task.Run(async () =>
            {
                try
                {
                    RaiseState("Recovery probe", channel.Name + ": " + reason);
                    await channel.RecoverAsync(reason).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RaiseState("Recovery probe failed", channel.Name + ": " + ex.Message);
                }
                finally
                {
                    lock (_syncRoot)
                    {
                        _controllerRecoveryInFlight = 0;
                    }
                }
            });
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
                if (normalized.State != NucChannelState.Disconnected
                    && normalized.State != NucChannelState.Recovering
                    && normalized.State != NucChannelState.Reopening)
                {
                    normalized.State = NucChannelState.FaultLatched;
                }

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
            double timeoutMs = Math.Max(1500, responseTimeoutMs * 2);
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
            channelSettings.UseGeneralInterrogationOnConnect = false;
            channelSettings.ChannelOperationMode = Iec101ChannelOperationMode.FullActive;
            return channelSettings;
        }
    }
}
