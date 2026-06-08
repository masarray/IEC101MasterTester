using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Diagnostics;
using IEC101MasterTester.Services.Iec101;
using IEC101MasterTester.Services.Profiles;
using IEC101MasterTester.Services.Redundancy;
using IEC101MasterTester.Services.Settings;
using IEC101MasterTester.Services.Soe;
using IEC101MasterTester.Models.Soe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using IEC101MasterTester.Services.Iec101.Native.Asdu;

namespace IEC101MasterTester.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private const int MaxLineMonitorRows = 500;
        private const int MaxEventLogRows = 1000;
        private const int MaxStatusHistoryRows = 200;
        private const int MaxFindingRows = 200;
        private const int MaxCommandLifeMonitorRows = 12;
        private const int MaxBufferReplaySessions = 24;
        private const int MaxRedundancyTimelineRows = 24;
        private const int MaxRedundancyJournalRows = 24;
        private const int MaxNucEventLogRows = 1000;
        private const int MaxNucSoeAuditRows = 1000;
        private const int NucSoeForensicCapacity = 8000;
        private const int MaxNucLineMonitorRows = 5;
        private const int MaxNucTraceRows = 5000;
        private const int MaxNucValueRows = 400;
        private const int MaxAvailabilityTimelineRows = 120;
        private const int MaxLineRawHexChars = 640;
        private const int MaxLineDetailChars = 900;
        private const int MaxNucTraceRawHexChars = 384;
        private const int MaxNucTraceDetailChars = 640;

        private readonly IIec101MasterService _masterService;
        private readonly ISettingsStore _settingsStore;
        private readonly INucRedundancySettingsStore _nucRedundancySettingsStore;
        private readonly INucRedundancyService _nucRedundancyService;
        private readonly NucSoeForensicJournal _nucSoeForensicJournal;
        private readonly Dictionary<int, ValueViewerRow> _valueIndex;
        private readonly Dictionary<int, ValueViewerRow> _nucValueIndex;
        private readonly ConcurrentDictionary<string, CommandTransaction> _nucFastCommandCache;
        private readonly Dictionary<int, string> _nucLastDiscreteStates;
        private readonly Dictionary<int, string> _lastDiscreteStates;
        private readonly HashSet<string> _activeFindingKeys;
        private readonly Dictionary<string, int> _findingEvidenceCounts;
        private readonly Dictionary<int, int> _binaryClass2ChangeCounts;
        private readonly Dictionary<int, int> _analogSpontCounts;
        private readonly HashSet<string> _activeBufferReplaySignatures;
        private readonly Dictionary<string, DateTime> _nucLineMonitorThrottleMap;
        private readonly SemaphoreSlim _connectLock;
        private readonly DispatcherTimer _availabilityMonitorTimer;
        private readonly Dictionary<string, string> _commandLifecycle = new Dictionary<string, string>();
        private readonly CommandLifeTrackerEngine _commandTracker;

        private string _connectionStatus;
        private string _connectionDetail;
        private string _activeModeInfo;
        private string _currentProfileSummary;
        private ConnectionSettings _currentSettings;
        private bool _isBusy;
        private bool _isTxActive;
        private bool _isRxActive;
        private string _lastConnectionEvent;
        private bool? _lastSlaveClass1Available;
        private string _lastEventLogKey;
        private string _lastNucEventLogKey;
        private string _lastNucSoeAuditKey;
        private string _lastNucLineMonitorKey;
        private string _lastPollEventClass;
        private bool _hasUnreadFindings;
        private BufferReplaySession _activeBufferReplaySession;
        private DateTime? _bufferReplayDisconnectedAtUtc;
        private DateTime? _bufferReplayReconnectedAtUtc;
        private DateTime? _lastBufferReplayEventTimestampUtc;
        private int _bufferReplayFinalizeToken;
        private int _redundancyGiCheckToken;
        private string _bufferReplayStatusText;
        private string _bufferReplaySummaryText;
        private string _redundancyPrimaryPort;
        private string _redundancyBackupPort;
        private string _redundancySelectedMode;
        private string _redundancySelectedGiPolicy;
        private string _redundancyConfigSummaryText;
        private string _redundancyValidationText;
        private string _redundancyControllerStatusText;
        private string _redundancyControllerDetailText;
        private string _redundancyActiveLinkText;
        private string _redundancyMainLinkText;
        private string _redundancyBackupLinkText;
        private string _redundancyIedFaultText;
        private string _redundancySwitchSummaryText;
        private string _redundancyGiObservationText;
        private string _redundancyContinuityText;
        private string _lastRedundancySwitchText;
        private string _redundancyFindingSummaryText;
        private string _redundancyFindingDetailsText;
        private string _availabilitySessionStartedText;
        private string _availabilitySummaryText;
        private string _availabilityUptimeText;
        private string _availabilityReconnectCountText;
        private string _availabilitySlaveRecoveryCountText;
        private string _availabilityRtuRestartCountText;
        private string _availabilityDowntimeText;
        private string _availabilityLongestDowntimeText;
        private string _availabilitySlaveDowntimeText;
        private string _availabilitySlaveLongestDowntimeText;
        private string _availabilityEventThroughputText;
        private string _availabilityProtocolErrorCountText;
        private string _availabilityAcdAssertCountText;
        private string _availabilityFindingsTrendText;
        private string _availabilityLinkSwitchoverCountText;
        private string _availabilityPercentText;
        private string _availabilityStateText;
        private double _availabilityPercentValue;
        private string _reliabilityScoreText;
        private string _reliabilityStateText;
        private double _reliabilityScoreValue;
        private string _availabilityHealthBreakdownText;
        private string _availabilityDowntimeImpactText;
        private string _availabilityRedundancyImpactText;
        private string _availabilityAnomalyPressureText;
        private string _slaveAvailabilityStateText;
        private string _slaveAvailabilityDetailText;
        private SlaveAvailabilityState _slaveAvailabilityState;
        private DateTime? _lastSlaveRxUtc;
        private DateTime? _lastSlaveValidFrameUtc;
        private DateTime? _lastSlaveValidAsduUtc;
        private DateTime? _slaveTransportConnectedAtUtc;
        private readonly Queue<DateTime> _slaveRecentErrorUtc = new Queue<DateTime>();
        private bool? _mainLinkFaultActive;
        private bool? _backupLinkFaultActive;
        private bool? _iedFaultActive;
        private bool _nucMainConnected;
        private bool _nucBackupConnected;
        private bool _nucSessionActive;
        private bool _nucMainFlowHealthy;
        private bool _nucBackupFlowHealthy;
        private bool _nucMainFaultLatched;
        private bool _nucBackupFaultLatched;
        private string _nucMainAcdState;
        private string _nucBackupAcdState;
        private NucLinkHealthState _nucMainLinkState;
        private NucLinkHealthState _nucBackupLinkState;
        private NucChannelRole _nucMainRole;
        private NucChannelRole _nucBackupRole;
        private NucChannelState _nucMainControllerState;
        private NucChannelState _nucBackupControllerState;
        private int _nucMainRxCount;
        private int _nucMainTxCount;
        private int _nucBackupRxCount;
        private int _nucBackupTxCount;
        private DateTime? _nucMainConnectedAtUtc;
        private DateTime? _nucBackupConnectedAtUtc;
        private DateTime? _nucMainLastActivityUtc;
        private DateTime? _nucBackupLastActivityUtc;
        private DateTime? _nucMainLastTxUtc;
        private DateTime? _nucMainLastRxUtc;
        private DateTime? _nucBackupLastTxUtc;
        private DateTime? _nucBackupLastRxUtc;
        private bool _isCompactMode = true;
        private DateTime? _nucMainLastResponseUtc;
        private DateTime? _nucBackupLastResponseUtc;
        private DateTime? _nucMainLastTimeoutUtc;
        private DateTime? _nucBackupLastTimeoutUtc;
        private DateTime? _nucMainLastClass1Utc;
        private DateTime? _nucMainLastClass2Utc;
        private DateTime? _nucMainLastGiUtc;
        private DateTime? _nucMainLastSupervisionUtc;
        private DateTime? _nucBackupLastClass1Utc;
        private DateTime? _nucBackupLastClass2Utc;
        private DateTime? _nucBackupLastGiUtc;
        private DateTime? _nucBackupLastSupervisionUtc;
        private DateTime? _nucMainLastFlowJournalUtc;
        private DateTime? _nucBackupLastFlowJournalUtc;
        private string _redundancyActiveLink;
        private int _redundancySwitchoverCount;
        private DateTime? _lastRedundancySwitchUtc;
        private DateTime? _lastRedundancyDisconnectUtc;
        private DateTime? _lastRedundancyReconnectUtc;
        private bool _giObservedAfterRedundancySwitch;
        private DateTime _availabilitySessionStartedUtc;
        private DateTime? _availabilityDisconnectedAtUtc;
        private int _availabilityReconnectCount;
        private int _availabilitySlaveRecoveryCount;
        private int _availabilityRtuRestartConfirmedCount;
        private int _availabilityRtuRestartSuspectedCount;
        private DateTime? _availabilityRestartEvidencePendingUntilUtc;
        private double _availabilityTotalDowntimeMs;
        private double _availabilityLongestDowntimeMs;
        private DateTime? _availabilitySlaveUnavailableAtUtc;
        private double _availabilitySlaveDowntimeMs;
        private double _availabilitySlaveLongestDowntimeMs;
        private int _availabilityObservedEventCount;
        private int _availabilityProtocolErrorCount;
        private int _availabilityAcdAssertCount;
        private DateTime? _nucAvailabilityDisconnectedAtUtc;
        private int _nucAvailabilityReconnectCount;
        private int _nucAvailabilitySlaveRecoveryCount;
        private double _nucAvailabilityTotalDowntimeMs;
        private double _nucAvailabilityLongestDowntimeMs;
        private DateTime? _nucAvailabilitySlaveUnavailableAtUtc;
        private double _nucAvailabilitySlaveDowntimeMs;
        private double _nucAvailabilitySlaveLongestDowntimeMs;
        private int _nucAvailabilityObservedEventCount;
        private int _nucAvailabilityProtocolErrorCount;
        private int _nucAvailabilityAcdAssertCount;
        private int _nucAvailabilityFlapCount;
        private int _nucAvailabilityDualUnhealthyEpisodeCount;
        private bool _nucDualUnhealthyLatched;
        private readonly Queue<DateTime> _nucRecentSwitchoverUtc = new Queue<DateTime>();
        private static readonly TimeSpan NucSwitchoverFlapWindow = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan SlaveNoRxWindow = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SlaveNoAsduWindow = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan SlaveRecentErrorWindow = TimeSpan.FromSeconds(20);
        private const int SlaveRecentErrorDegradedThreshold = 3;

        // Class 1 / ACD burst analyzer
        private bool _class1BurstActive;
        private DateTime _class1BurstStartUtc;
        private int _class1BurstTotalCount;
        private int _class1BurstMeasurementCount;
        private int _class1BurstMeteringCount;
        private int _class1BurstDiscreteCount;
        private int _class1BurstCommandCount;
        private int _class1BurstOtherCount;
        private int _class1BurstToggleCount;
        private int _class1BurstFinalizeToken;
        private readonly HashSet<int> _class1BurstBinaryIoas = new HashSet<int>();
        private readonly HashSet<int> _class1BurstAnalogIoas = new HashSet<int>();
        private readonly HashSet<int> _class1BurstCommandIoas = new HashSet<int>();

        private int _singleCommandIoa = 2102;
        private int _doubleCommandIoa = 2001;
        private int _stepCommandIoa = 2201;
        private ValueViewerRow _selectedValue;
        private ValueViewerRow _selectedNucValue;
        // GI analysis state
        private bool _giInProgress;
        private DateTime _giLastCompletedUtc;

        private readonly HashSet<int> _giReceivedIoas = new HashSet<int>();
        private readonly HashSet<int> _giDiscreteIoas = new HashSet<int>();
        private readonly HashSet<int> _giAnalogIoas = new HashSet<int>();
        private readonly HashSet<int> _giCommandIoas = new HashSet<int>();
        private DateTime _giStartTime;
        private static readonly TimeSpan EmptyClass1BurstGiSuppressWindow = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan Class1BurstFinalizeGraceWindow = TimeSpan.FromMilliseconds(150);
        private static readonly TimeSpan BufferReplayObserveWindow = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan RedundancyGiObserveWindow = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan NucLinkFlowWindow = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan NucLinkTimeoutBadgeWindow = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan NucLinkInitialResponseWindow = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan NucLinkSwitchingWindow = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan NucFlowJournalCooldown = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan NucRecentDataBadgeWindow = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan NucRecentGiBadgeWindow = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan NucPulseWindow = TimeSpan.FromSeconds(2);
        private static readonly Brush NucActiveBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
        private static readonly Brush NucStandbyBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
        private static readonly Brush NucFaultBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        private static readonly Brush NucSwitchingBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        private static readonly Brush NucClass1Brush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        private static readonly Brush NucClass2Brush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        private static readonly Brush NucGiBrush = new SolidColorBrush(Color.FromRgb(168, 85, 247));
        private static readonly Brush NucTextBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));
        private static readonly Brush NucPanelBrush = new SolidColorBrush(Color.FromRgb(17, 24, 39));
        private static readonly Brush NucActivePanelBrush = new SolidColorBrush(Color.FromRgb(15, 30, 44));

        public MainViewModel(IIec101MasterService masterService, ISettingsStore settingsStore)
        {
            _masterService = masterService;
            _settingsStore = settingsStore;
            _nucRedundancySettingsStore = new JsonNucRedundancySettingsStore();
            _nucRedundancyService = new NucRedundancyService();
            _nucSoeForensicJournal = new NucSoeForensicJournal(NucSoeForensicCapacity);
            _valueIndex = new Dictionary<int, ValueViewerRow>();
            _nucValueIndex = new Dictionary<int, ValueViewerRow>();
            _nucFastCommandCache = new ConcurrentDictionary<string, CommandTransaction>(StringComparer.OrdinalIgnoreCase);
            _lastDiscreteStates = new Dictionary<int, string>();
            _nucLastDiscreteStates = new Dictionary<int, string>();
            _activeFindingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _findingEvidenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _binaryClass2ChangeCounts = new Dictionary<int, int>();
            _analogSpontCounts = new Dictionary<int, int>();
            _activeBufferReplaySignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _nucLineMonitorThrottleMap = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            _connectLock = new SemaphoreSlim(1, 1);
            _availabilityMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _availabilityMonitorTimer.Tick += AvailabilityMonitorTimer_Tick;
            _commandTracker = new CommandLifeTrackerEngine();
            _doubleCommandIoa = OfficialPointProfiles.TryGetDefaultCommandIoa("Double") ?? _doubleCommandIoa;
            _stepCommandIoa = OfficialPointProfiles.TryGetDefaultCommandIoa("Regulating") ?? _stepCommandIoa;

            Values = new ObservableCollection<ValueViewerRow>();
            NucValues = new ObservableCollection<ValueViewerRow>();
            CommandSignals = new ObservableCollection<ValueViewerRow>();
            LineMonitor = new ObservableCollection<LineMonitorRow>();
            NucLineMonitor = new ObservableCollection<LineMonitorRow>();
            NucTraceLinkA = new ObservableCollection<LineMonitorRow>();
            NucTraceLinkB = new ObservableCollection<LineMonitorRow>();
            EventLog = new ObservableCollection<EventLogRow>();
            NucEventLog = new ObservableCollection<EventLogRow>();
            NucSoeAuditLog = new ObservableCollection<EventLogRow>();
            StatusHistory = new ObservableCollection<StatusHistoryRow>();
            Findings = new ObservableCollection<FindingRow>();
            CommandLifeMonitor = new ObservableCollection<CommandLifeMonitorRow>();
            BufferReplaySessions = new ObservableCollection<BufferReplaySession>();
            RedundancyTimeline = new ObservableCollection<RedundancyTimelineRow>();
            RedundancyEventJournal = new ObservableCollection<RedundancyEventJournalRow>();
            AvailabilityTimeline = new ObservableCollection<AvailabilityTimelineRow>();
            RedundancySerialPortOptions = new ObservableCollection<string>();
            RedundancyModeOptions = new ObservableCollection<string>(new[] { "Hot-Standby" });
            RedundancyGiPolicyOptions = new ObservableCollection<string>(new[] { "Required", "Optional", "Not Expected" });
            NucMasterPanel = new NucEndpointPanelViewModel { Title = "MASTER" };
            NucSlavePanel = new NucEndpointPanelViewModel { Title = "SLAVE" };
            NucLinkAVisual = new NucLinkVisualViewModel { LinkName = "LINK A" };
            NucLinkBVisual = new NucLinkVisualViewModel { LinkName = "LINK B" };

            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), CanConnect);
            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync(), CanDisconnect);
            SendGeneralInterrogationCommand = new RelayCommand(async () => await SendGeneralInterrogationAsync(), () => CanSendCommands);
            SendClockSyncCommand = new RelayCommand(async () => await SendClockSyncAsync(), () => CanSendCommands);
            SendSingleOnCommand = new RelayCommand(async () => await SendSingleCommandAsync(true), () => CanSendCommands);
            SendSingleOffCommand = new RelayCommand(async () => await SendSingleCommandAsync(false), () => CanSendCommands);
            SendSingleSelectOnCommand = new RelayCommand(async () => await SendSingleCommandAsync(true, true), () => CanSendCommands);
            SendSingleSelectOffCommand = new RelayCommand(async () => await SendSingleCommandAsync(false, true), () => CanSendCommands);
            SendDoubleOpenCommand = new RelayCommand(async () => await SendDoubleCommandAsync(false), () => CanSendCommands);
            SendDoubleCloseCommand = new RelayCommand(async () => await SendDoubleCommandAsync(true), () => CanSendCommands);
            SendDoubleSelectOpenCommand = new RelayCommand(async () => await SendDoubleCommandAsync(false, true), () => CanSendCommands);
            SendDoubleSelectCloseCommand = new RelayCommand(async () => await SendDoubleCommandAsync(true, true), () => CanSendCommands);
            SendRaiseCommand = new RelayCommand(async () => await SendStepCommandAsync(true), () => CanSendCommands);
            SendLowerCommand = new RelayCommand(async () => await SendStepCommandAsync(false), () => CanSendCommands);
            SendSelectRaiseCommand = new RelayCommand(async () => await SendStepCommandAsync(true, true), () => CanSendCommands);
            SendSelectLowerCommand = new RelayCommand(async () => await SendStepCommandAsync(false, true), () => CanSendCommands);
            ClearEventLogCommand = new RelayCommand(ClearEventLog);
            ClearValuesCommand = new RelayCommand(ClearValues);

            ConnectionStatus = ConnectionStatusInfo.Disconnected.DisplayText;
            ConnectionDetail = ConnectionStatusInfo.Disconnected.Detail;
            ActiveModeInfo = "IEC-101 Master / Unbalanced";
            CurrentSettings = ConnectionSettings.CreateDefault();
            BufferReplayStatusText = "Idle";
            BufferReplaySummaryText = "No buffer replay session yet.";
            RedundancyActiveLinkText = "Active link: Unknown";
            RedundancyMainLinkText = "L1FT: Unknown";
            RedundancyBackupLinkText = "L2FT: Unknown";
            RedundancyIedFaultText = "IEDF: Unknown";
            RedundancySwitchSummaryText = "Switchover count: 0";
            RedundancyGiObservationText = "GI after switchover: Not observed";
            RedundancyContinuityText = "Continuity gap: -";
            LastRedundancySwitchText = "Last switchover: -";
            RedundancyFindingSummaryText = "Redundancy findings: pending observation.";
            RedundancyFindingDetailsText = "No redundancy finding recorded yet.";
            ResetAvailabilityState();
            InitializeNucRedundancyVisualModels();
            RefreshRedundancySerialPorts();

            _masterService.ConnectionStateChanged += MasterService_ConnectionStateChanged;
            _masterService.LineMonitorRecordReceived += MasterService_LineMonitorRecordReceived;
            _masterService.ValueReceived += MasterService_ValueReceived;
            _nucRedundancyService.SessionStateChanged += NucRedundancyService_SessionStateChanged;
            _nucRedundancyService.ConnectionStateChanged += NucRedundancyService_ConnectionStateChanged;
            _nucRedundancyService.LineMonitorRecordReceived += NucRedundancyService_LineMonitorRecordReceived;
            _nucRedundancyService.ValueReceived += NucRedundancyService_ValueReceived;
            _availabilityMonitorTimer.Start();
        }

        public ObservableCollection<ValueViewerRow> Values { get; }
        public ObservableCollection<ValueViewerRow> NucValues { get; }
        public ObservableCollection<ValueViewerRow> CommandSignals { get; }
        public ObservableCollection<LineMonitorRow> LineMonitor { get; }
        public ObservableCollection<LineMonitorRow> NucLineMonitor { get; }
        public ObservableCollection<LineMonitorRow> NucTraceLinkA { get; }
        public ObservableCollection<LineMonitorRow> NucTraceLinkB { get; }
        public ObservableCollection<EventLogRow> EventLog { get; }
        public ObservableCollection<EventLogRow> NucEventLog { get; }
        public ObservableCollection<EventLogRow> NucSoeAuditLog { get; }
        public NucSoeForensicJournal NucSoeForensicJournal => _nucSoeForensicJournal;
        public ObservableCollection<StatusHistoryRow> StatusHistory { get; }
        public ObservableCollection<FindingRow> Findings { get; }
        public bool HasUnreadFindings { get => _hasUnreadFindings; private set => SetProperty(ref _hasUnreadFindings, value); }
        public ObservableCollection<CommandLifeMonitorRow> CommandLifeMonitor { get; }
        public ObservableCollection<BufferReplaySession> BufferReplaySessions { get; }
        public ObservableCollection<RedundancyTimelineRow> RedundancyTimeline { get; }
        public ObservableCollection<RedundancyEventJournalRow> RedundancyEventJournal { get; }
        public ObservableCollection<AvailabilityTimelineRow> AvailabilityTimeline { get; }
        public string BufferReplayStatusText { get => _bufferReplayStatusText; private set => SetProperty(ref _bufferReplayStatusText, value); }
        public string BufferReplaySummaryText { get => _bufferReplaySummaryText; private set => SetProperty(ref _bufferReplaySummaryText, value); }
        public ObservableCollection<string> RedundancySerialPortOptions { get; }
        public ObservableCollection<string> RedundancyModeOptions { get; }
        public ObservableCollection<string> RedundancyGiPolicyOptions { get; }
        public NucEndpointPanelViewModel NucMasterPanel { get; }
        public NucEndpointPanelViewModel NucSlavePanel { get; }
        public NucLinkVisualViewModel NucLinkAVisual { get; }
        public NucLinkVisualViewModel NucLinkBVisual { get; }
        public bool IsCompactMode
        {
            get => _isCompactMode;
            set
            {
                if (SetProperty(ref _isCompactMode, value))
                {
                    OnPropertyChanged(nameof(IsExpandedMode));
                    OnPropertyChanged(nameof(CompactModeButtonText));
                    OnPropertyChanged(nameof(IsNucCompactPanels));
                    OnPropertyChanged(nameof(IsNucExpandedPanels));
                    OnPropertyChanged(nameof(NucCompactButtonText));
                }
            }
        }
        public bool IsExpandedMode => !IsCompactMode;
        public string CompactModeButtonText => IsCompactMode ? "Expanded View" : "Compact View";
        public bool IsNucCompactPanels => IsCompactMode;
        public bool IsNucExpandedPanels => IsExpandedMode;
        public string NucCompactButtonText => CompactModeButtonText;
        public bool IsNucMainTxRecent => IsRecentNucPulse(_nucMainLastTxUtc);
        public bool IsNucMainRxRecent => IsRecentNucPulse(_nucMainLastRxUtc);
        public bool IsNucBackupTxRecent => IsRecentNucPulse(_nucBackupLastTxUtc);
        public bool IsNucBackupRxRecent => IsRecentNucPulse(_nucBackupLastRxUtc);
        public bool IsNucMainClass1Recent => IsRecentNucPulse(_nucMainLastClass1Utc);
        public bool IsNucMainClass2Recent => IsRecentNucPulse(_nucMainLastClass2Utc);
        public bool IsNucMainGiRecent => IsRecentNucPulse(_nucMainLastGiUtc);
        public bool IsNucMainLinkCheckRecent => IsRecentNucPulse(_nucMainLastSupervisionUtc);
        public bool IsNucMainTimeoutActive => _nucMainLinkState == NucLinkHealthState.Timeout || _nucMainLinkState == NucLinkHealthState.Fault;
        public bool IsNucMainConnectedIndicator => _nucMainConnected;
        public bool IsNucMainPortOpen => _nucMainConnected;
        public string NucMainPortStateText => _nucMainConnected ? "OPEN" : "CLOSED";
        public string NucMainCommStateText => GetNucCommStateText("Main");
        public bool IsNucBackupClass1Recent => IsRecentNucPulse(_nucBackupLastClass1Utc);
        public bool IsNucBackupClass2Recent => IsRecentNucPulse(_nucBackupLastClass2Utc);
        public bool IsNucBackupGiRecent => IsRecentNucPulse(_nucBackupLastGiUtc);
        public bool IsNucBackupLinkCheckRecent => IsRecentNucPulse(_nucBackupLastSupervisionUtc);
        public bool IsNucBackupTimeoutActive => _nucBackupLinkState == NucLinkHealthState.Timeout || _nucBackupLinkState == NucLinkHealthState.Fault;
        public bool IsNucBackupConnectedIndicator => _nucBackupConnected;
        public bool IsNucBackupPortOpen => _nucBackupConnected;
        public string NucBackupPortStateText => _nucBackupConnected ? "OPEN" : "CLOSED";
        public string NucBackupCommStateText => GetNucCommStateText("Backup");
        public string RedundancyPrimaryPort
        {
            get => _redundancyPrimaryPort;
            set
            {
                if (SetProperty(ref _redundancyPrimaryPort, value))
                {
                    RefreshRedundancyConfigurationSummary();
                }
            }
        }
        public string RedundancyBackupPort
        {
            get => _redundancyBackupPort;
            set
            {
                if (SetProperty(ref _redundancyBackupPort, value))
                {
                    RefreshRedundancyConfigurationSummary();
                }
            }
        }
        public string RedundancySelectedMode
        {
            get => _redundancySelectedMode;
            set
            {
                if (SetProperty(ref _redundancySelectedMode, value))
                {
                    RefreshRedundancyConfigurationSummary();
                }
            }
        }
        public string RedundancySelectedGiPolicy
        {
            get => _redundancySelectedGiPolicy;
            set
            {
                if (SetProperty(ref _redundancySelectedGiPolicy, value))
                {
                    RefreshRedundancyConfigurationSummary();
                }
            }
        }
        public string RedundancyConfigSummaryText { get => _redundancyConfigSummaryText; private set => SetProperty(ref _redundancyConfigSummaryText, value); }
        public string RedundancyValidationText { get => _redundancyValidationText; private set => SetProperty(ref _redundancyValidationText, value); }
        public string RedundancyControllerStatusText { get => _redundancyControllerStatusText; private set => SetProperty(ref _redundancyControllerStatusText, value); }
        public string RedundancyControllerDetailText { get => _redundancyControllerDetailText; private set => SetProperty(ref _redundancyControllerDetailText, value); }
        public string RedundancyActiveLinkText { get => _redundancyActiveLinkText; private set => SetProperty(ref _redundancyActiveLinkText, value); }
        public string RedundancyMainLinkText { get => _redundancyMainLinkText; private set => SetProperty(ref _redundancyMainLinkText, value); }
        public string RedundancyBackupLinkText { get => _redundancyBackupLinkText; private set => SetProperty(ref _redundancyBackupLinkText, value); }
        public string RedundancyIedFaultText { get => _redundancyIedFaultText; private set => SetProperty(ref _redundancyIedFaultText, value); }
        public string RedundancySwitchSummaryText { get => _redundancySwitchSummaryText; private set => SetProperty(ref _redundancySwitchSummaryText, value); }
        public string RedundancyGiObservationText { get => _redundancyGiObservationText; private set => SetProperty(ref _redundancyGiObservationText, value); }
        public string RedundancyContinuityText { get => _redundancyContinuityText; private set => SetProperty(ref _redundancyContinuityText, value); }
        public string LastRedundancySwitchText { get => _lastRedundancySwitchText; private set => SetProperty(ref _lastRedundancySwitchText, value); }
        public string RedundancyFindingSummaryText { get => _redundancyFindingSummaryText; private set => SetProperty(ref _redundancyFindingSummaryText, value); }
        public string RedundancyFindingDetailsText { get => _redundancyFindingDetailsText; private set => SetProperty(ref _redundancyFindingDetailsText, value); }
        public int RedundancySwitchoverCountValue => _redundancySwitchoverCount;
        public bool IsGiObservedAfterRedundancySwitch => _giObservedAfterRedundancySwitch;
        public int NucAvailabilityAcdAssertCountValue => _nucAvailabilityAcdAssertCount;
        public string AvailabilitySessionStartedText { get => _availabilitySessionStartedText; private set => SetProperty(ref _availabilitySessionStartedText, value); }
        public string AvailabilitySummaryText { get => _availabilitySummaryText; private set => SetProperty(ref _availabilitySummaryText, value); }
        public string AvailabilityUptimeText { get => _availabilityUptimeText; private set => SetProperty(ref _availabilityUptimeText, value); }
        public string AvailabilityReconnectCountText { get => _availabilityReconnectCountText; private set => SetProperty(ref _availabilityReconnectCountText, value); }
        public string AvailabilitySlaveRecoveryCountText { get => _availabilitySlaveRecoveryCountText; private set => SetProperty(ref _availabilitySlaveRecoveryCountText, value); }
        public string AvailabilityRtuRestartCountText { get => _availabilityRtuRestartCountText; private set => SetProperty(ref _availabilityRtuRestartCountText, value); }
        public string AvailabilityDowntimeText { get => _availabilityDowntimeText; private set => SetProperty(ref _availabilityDowntimeText, value); }
        public string AvailabilityLongestDowntimeText { get => _availabilityLongestDowntimeText; private set => SetProperty(ref _availabilityLongestDowntimeText, value); }
        public string AvailabilitySlaveDowntimeText { get => _availabilitySlaveDowntimeText; private set => SetProperty(ref _availabilitySlaveDowntimeText, value); }
        public string AvailabilitySlaveLongestDowntimeText { get => _availabilitySlaveLongestDowntimeText; private set => SetProperty(ref _availabilitySlaveLongestDowntimeText, value); }
        public string AvailabilityEventThroughputText { get => _availabilityEventThroughputText; private set => SetProperty(ref _availabilityEventThroughputText, value); }
        public string AvailabilityProtocolErrorCountText { get => _availabilityProtocolErrorCountText; private set => SetProperty(ref _availabilityProtocolErrorCountText, value); }
        public string AvailabilityAcdAssertCountText { get => _availabilityAcdAssertCountText; private set => SetProperty(ref _availabilityAcdAssertCountText, value); }
        public string AvailabilityFindingsTrendText { get => _availabilityFindingsTrendText; private set => SetProperty(ref _availabilityFindingsTrendText, value); }
        public string AvailabilityLinkSwitchoverCountText { get => _availabilityLinkSwitchoverCountText; private set => SetProperty(ref _availabilityLinkSwitchoverCountText, value); }
        public string AvailabilityPercentText { get => _availabilityPercentText; private set => SetProperty(ref _availabilityPercentText, value); }
        public string AvailabilityStateText { get => _availabilityStateText; private set => SetProperty(ref _availabilityStateText, value); }
        public double AvailabilityPercentValue { get => _availabilityPercentValue; private set => SetProperty(ref _availabilityPercentValue, value); }
        public string ReliabilityScoreText { get => _reliabilityScoreText; private set => SetProperty(ref _reliabilityScoreText, value); }
        public string ReliabilityStateText { get => _reliabilityStateText; private set => SetProperty(ref _reliabilityStateText, value); }
        public double ReliabilityScoreValue { get => _reliabilityScoreValue; private set => SetProperty(ref _reliabilityScoreValue, value); }
        public string AvailabilityHealthBreakdownText { get => _availabilityHealthBreakdownText; private set => SetProperty(ref _availabilityHealthBreakdownText, value); }
        public string AvailabilityDowntimeImpactText { get => _availabilityDowntimeImpactText; private set => SetProperty(ref _availabilityDowntimeImpactText, value); }
        public string AvailabilityRedundancyImpactText { get => _availabilityRedundancyImpactText; private set => SetProperty(ref _availabilityRedundancyImpactText, value); }
        public string AvailabilityAnomalyPressureText { get => _availabilityAnomalyPressureText; private set => SetProperty(ref _availabilityAnomalyPressureText, value); }
        public string SlaveAvailabilityStateText { get => _slaveAvailabilityStateText; private set => SetProperty(ref _slaveAvailabilityStateText, value); }
        public string SlaveAvailabilityDetailText { get => _slaveAvailabilityDetailText; private set => SetProperty(ref _slaveAvailabilityDetailText, value); }
        public RelayCommand ConnectCommand { get; }
        public RelayCommand DisconnectCommand { get; }
        public RelayCommand SendGeneralInterrogationCommand { get; }
        public RelayCommand SendClockSyncCommand { get; }
        public RelayCommand SendSingleOnCommand { get; }
        public RelayCommand SendSingleOffCommand { get; }
        public RelayCommand SendSingleSelectOnCommand { get; }
        public RelayCommand SendSingleSelectOffCommand { get; }
        public RelayCommand SendDoubleOpenCommand { get; }
        public RelayCommand SendDoubleCloseCommand { get; }
        public RelayCommand SendDoubleSelectOpenCommand { get; }
        public RelayCommand SendDoubleSelectCloseCommand { get; }
        public RelayCommand SendRaiseCommand { get; }
        public RelayCommand SendLowerCommand { get; }
        public RelayCommand SendSelectRaiseCommand { get; }
        public RelayCommand SendSelectLowerCommand { get; }
        public RelayCommand ClearEventLogCommand { get; }
        public RelayCommand ClearValuesCommand { get; }
        public bool CanEditSettings => !_isBusy && !_masterService.IsConnected;
        public bool CanSendCommands => !_isBusy && _masterService.IsConnected;
        public int SingleCommandIoa { get => _singleCommandIoa; set => SetProperty(ref _singleCommandIoa, value); }
        public int DoubleCommandIoa { get => _doubleCommandIoa; set => SetProperty(ref _doubleCommandIoa, value); }
        public int StepCommandIoa { get => _stepCommandIoa; set => SetProperty(ref _stepCommandIoa, value); }
        public bool CanOpenSelectedValueCommand => GetSignalCommandFamily(SelectedValue) != null;
        public bool IsTxActive { get => _isTxActive; private set => SetProperty(ref _isTxActive, value); }
        public bool IsRxActive { get => _isRxActive; private set => SetProperty(ref _isRxActive, value); }
        public ValueViewerRow SelectedValue
        {
            get => _selectedValue;
            set
            {
                if (SetProperty(ref _selectedValue, value))
                {
                    OnPropertyChanged(nameof(CanOpenSelectedValueCommand));
                }
            }
        }

        public ConnectionSettings CurrentSettings
        {
            get => _currentSettings;
            private set
            {
                if (SetProperty(ref _currentSettings, value))
                {
                    ActiveModeInfo = "IEC-101 Master / " + value.LinkLayerMode;
                    CurrentProfileSummary = BuildProfileSummary(value);
                }
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        public string ConnectionDetail
        {
            get => _connectionDetail;
            private set => SetProperty(ref _connectionDetail, value);
        }

        public string ActiveModeInfo
        {
            get => _activeModeInfo;
            private set => SetProperty(ref _activeModeInfo, value);
        }

        public string CurrentProfileSummary
        {
            get => _currentProfileSummary;
            private set => SetProperty(ref _currentProfileSummary, value);
        }

        public async Task InitializeAsync()
        {
            ConnectionSettings settings = await _settingsStore.LoadAsync();
            NucRedundancySettings nucSettings = await _nucRedundancySettingsStore.LoadAsync();
            CurrentSettings = settings;
            _masterService.ApplySettings(settings);
            ApplyLoadedNucRedundancySettings(nucSettings);
            RefreshRedundancySerialPorts();
            AddSystemLine("READY", "Configuration loaded", "Application initialized.");
            RefreshCommands();
        }

        public async Task UpdateSettingsAsync(ConnectionSettings settings)
        {
            CurrentSettings = settings.Clone();
            _masterService.ApplySettings(CurrentSettings);
            await _settingsStore.SaveAsync(CurrentSettings);
            RefreshRedundancySerialPorts();
            AddSystemLine("CFG", "Settings saved", CurrentProfileSummary);
            RefreshCommands();
        }

        public async Task ShutdownAsync()
        {
            await DisconnectAsync();
        }

        public void RefreshAvailabilityDashboardSnapshot()
        {
            RefreshAvailabilityTelemetry();
        }

        private void AvailabilityMonitorTimer_Tick(object sender, EventArgs e)
        {
            RefreshAvailabilityTelemetry();
            UpdateNucRedundancyVisuals();
        }

        public Task DisconnectForExclusiveWindowAsync()
        {
            return DisconnectAsync();
        }

        public bool CanStartNucRedundancySession()
        {
            return !_nucRedundancyService.IsSessionActive;
        }

        public bool CanStopNucRedundancySession()
        {
            return _nucRedundancyService.IsSessionActive;
        }

        public bool IsNucRedundancySessionActive => _nucSessionActive || _nucRedundancyService.IsSessionActive;
        public bool CanStartNucRedundancySessionButton => !IsNucRedundancySessionActive;
        public bool CanStopNucRedundancySessionButton => IsNucRedundancySessionActive;

        public bool TryStartNucRedundancySession(out string validationMessage)
        {
            validationMessage = string.Empty;

            NucRedundancySettings settings;
            if (!TryBuildNucRedundancySettings(out settings, out validationMessage))
            {
                RedundancyValidationText = validationMessage;
                return false;
            }

            _nucRedundancyService.ApplySettings(settings);
            _nucSessionActive = false;
            _redundancyActiveLink = null;
            ClearNucRecentTrafficBadges();
            ClearNucValues();
            ResetAvailabilityState();
            _nucRedundancyService.StartSession();
            OnPropertyChanged(nameof(IsNucRedundancySessionActive));
            OnPropertyChanged(nameof(CanStartNucRedundancySessionButton));
            OnPropertyChanged(nameof(CanStopNucRedundancySessionButton));
            return true;
        }

        public void StopNucRedundancySession()
        {
            _nucRedundancyService.StopSession();
            ClearNucRecentTrafficBadges();
            OnPropertyChanged(nameof(IsNucRedundancySessionActive));
            OnPropertyChanged(nameof(CanStartNucRedundancySessionButton));
            OnPropertyChanged(nameof(CanStopNucRedundancySessionButton));
        }

        public async Task StopNucRedundancySessionAsync()
        {
            await _nucRedundancyService.StopSessionAsync();
            ClearNucRecentTrafficBadges();
            OnPropertyChanged(nameof(IsNucRedundancySessionActive));
            OnPropertyChanged(nameof(CanStartNucRedundancySessionButton));
            OnPropertyChanged(nameof(CanStopNucRedundancySessionButton));
        }

        public async Task SendNucRedundancyGiAsync()
        {
            await _nucRedundancyService.SendGeneralInterrogationAsync();
        }

        private bool CanConnect()
        {
            return !_isBusy && !_masterService.IsConnected;
        }

        private bool CanDisconnect()
        {
            return !_isBusy && _masterService.IsConnected;
        }

        private async Task ConnectAsync()
        {
            await _connectLock.WaitAsync();
            try
            {
                if (_isBusy || _masterService.IsConnected)
                {
                    return;
                }

                _isBusy = true;
                ConnectionDetail = "Opening serial port and starting IEC-101 master.";
                RefreshCommands();

                _masterService.ApplySettings(CurrentSettings);
                await _masterService.ConnectAsync();
            }
            catch (Exception ex)
            {
                ConnectionDetail = ex.Message;
                AddSystemLine("ERR", "Connect failed", ex.Message);
            }
            finally
            {
                _isBusy = false;
                RefreshCommands();
                _connectLock.Release();
            }
        }

        private async Task DisconnectAsync()
        {
            await _connectLock.WaitAsync();
            try
            {
                if (_isBusy)
                {
                    return;
                }

                _isBusy = true;
                ConnectionDetail = "Stopping worker loop and releasing COM port.";
                RefreshCommands();

                await _masterService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                ConnectionDetail = ex.Message;
                AddSystemLine("ERR", "Disconnect failed", ex.Message);
            }
            finally
            {
                _isBusy = false;
                RefreshCommands();
                _connectLock.Release();
            }
        }

        private async Task SendGeneralInterrogationAsync()
        {
            if (!CanSendCommands)
            {
                return;
            }

            _isBusy = true;
            RefreshCommands();
            try
            {
                await _masterService.SendGeneralInterrogationAsync();
            }
            finally
            {
                _isBusy = false;
                RefreshCommands();
            }
        }

        private async Task SendClockSyncAsync()
        {
            if (!CanSendCommands)
            {
                return;
            }

            _isBusy = true;
            RefreshCommands();
            try
            {
                await _masterService.SendClockSyncAsync();
            }
            finally
            {
                _isBusy = false;
                RefreshCommands();
            }
        }


        private async Task SendSingleCommandAsync(bool state, bool select = false)
        {
            await SendSignalCommandAsync("Single", SingleCommandIoa, state ? "ON" : "OFF", select);
        }

        private async Task SendDoubleCommandAsync(bool on, bool select = false)
        {
            await SendSignalCommandAsync("Double", DoubleCommandIoa, on ? "CLOSE" : "OPEN", select);
        }

        private async Task SendStepCommandAsync(bool raise, bool select = false)
        {
            await SendSignalCommandAsync("Regulating", StepCommandIoa, raise ? "RAISE" : "LOWER", select);
        }
        private void ClearEventLog()
        {
            EventLog.Clear();
            StatusHistory.Clear();
            Findings.Clear();
            CommandLifeMonitor.Clear();
            BufferReplaySessions.Clear();
            RedundancyTimeline.Clear();
            RedundancyEventJournal.Clear();
            AvailabilityTimeline.Clear();
            _activeFindingKeys.Clear();
            _findingEvidenceCounts.Clear();
            _binaryClass2ChangeCounts.Clear();
            _analogSpontCounts.Clear();
            ResetBufferReplayState();
            ResetRedundancyState();
            ResetAvailabilityState();
            HasUnreadFindings = false;
            _commandTracker.Clear();
            _commandLifecycle.Clear();
            ResetClass1BurstAnalysis();
            ClearNucValues();
        }

        private void ClearValues()
        {
            Values.Clear();
            CommandSignals.Clear();
            _valueIndex.Clear();
            _lastDiscreteStates.Clear();
            SelectedValue = null;
        }

        private void ClearNucValues()
        {
            NucValues.Clear();
            NucEventLog.Clear();
            NucSoeAuditLog.Clear();
            _nucSoeForensicJournal.Clear();
            NucLineMonitor.Clear();
            NucTraceLinkA.Clear();
            NucTraceLinkB.Clear();
            _nucValueIndex.Clear();
            _nucLastDiscreteStates.Clear();
            _lastNucEventLogKey = null;
            _lastNucSoeAuditKey = null;
            _lastNucLineMonitorKey = null;
            SelectedNucValue = null;
        }

        public bool TryClearNucRuntimeObservability(out string validationMessage)
        {
            validationMessage = string.Empty;

            if (_nucSessionActive || _nucRedundancyService.IsSessionActive)
            {
                validationMessage = "Stop Session terlebih dahulu agar reset statistik/runtime aman dan tidak mengganggu komunikasi aktif.";
                return false;
            }

            ClearNucRuntimeObservability();
            return true;
        }

        private void ClearNucRuntimeObservability()
        {
            ClearNucValues();
            Findings.Clear();
            BufferReplaySessions.Clear();
            RedundancyTimeline.Clear();
            RedundancyEventJournal.Clear();
            AvailabilityTimeline.Clear();
            CommandLifeMonitor.Clear();

            _activeFindingKeys.Clear();
            _findingEvidenceCounts.Clear();
            _binaryClass2ChangeCounts.Clear();
            _analogSpontCounts.Clear();
            _commandTracker.Clear();
            _commandLifecycle.Clear();
            _nucFastCommandCache.Clear();

            ResetClass1BurstAnalysis();
            ResetBufferReplayState();
            ResetNucObservabilityState();
            ResetAvailabilityState();
            RefreshRedundancyFindingsDashboard();
            HasUnreadFindings = false;
        }

        private void ResetNucObservabilityState()
        {
            _mainLinkFaultActive = null;
            _backupLinkFaultActive = null;
            _iedFaultActive = null;
            _nucMainConnected = false;
            _nucBackupConnected = false;
            _nucMainFlowHealthy = false;
            _nucBackupFlowHealthy = false;
            _nucMainFaultLatched = false;
            _nucBackupFaultLatched = false;
            _nucMainAcdState = null;
            _nucBackupAcdState = null;
            _nucMainLinkState = NucLinkHealthState.Fault;
            _nucBackupLinkState = NucLinkHealthState.Fault;
            _nucMainRole = NucChannelRole.Active;
            _nucBackupRole = NucChannelRole.Standby;
            _nucMainControllerState = NucChannelState.Disconnected;
            _nucBackupControllerState = NucChannelState.Disconnected;
            _nucMainRxCount = 0;
            _nucMainTxCount = 0;
            _nucBackupRxCount = 0;
            _nucBackupTxCount = 0;
            _nucMainConnectedAtUtc = null;
            _nucBackupConnectedAtUtc = null;
            _nucMainLastActivityUtc = null;
            _nucBackupLastActivityUtc = null;
            _nucMainLastTxUtc = null;
            _nucMainLastRxUtc = null;
            _nucBackupLastTxUtc = null;
            _nucBackupLastRxUtc = null;
            _nucMainLastResponseUtc = null;
            _nucBackupLastResponseUtc = null;
            _nucMainLastTimeoutUtc = null;
            _nucBackupLastTimeoutUtc = null;
            _nucMainLastFlowJournalUtc = null;
            _nucBackupLastFlowJournalUtc = null;
            _redundancyActiveLink = null;
            _redundancySwitchoverCount = 0;
            _lastRedundancySwitchUtc = null;
            _lastRedundancyDisconnectUtc = null;
            _lastRedundancyReconnectUtc = null;
            _giObservedAfterRedundancySwitch = false;
            RedundancyActiveLinkText = "Active link: Unknown";
            RedundancyMainLinkText = "L1FT: Unknown";
            RedundancyBackupLinkText = "L2FT: Unknown";
            RedundancyIedFaultText = "IEDF: Unknown";
            RedundancySwitchSummaryText = "Switchover count: 0";
            RedundancyGiObservationText = "GI after switchover: Not observed";
            RedundancyContinuityText = "Continuity gap: -";
            LastRedundancySwitchText = "Last switchover: -";
            RedundancyFindingSummaryText = "Redundancy findings: pending observation.";
            RedundancyFindingDetailsText = "No redundancy finding recorded yet.";
            ClearNucRecentTrafficBadges();
            UpdateNucRedundancyVisuals();
        }

        public string GetSelectedValueCommandFamily()
        {
            return GetSignalCommandFamily(SelectedValue);
        }

        public string GetSelectedNucValueCommandFamily()
        {
            return GetSignalCommandFamily(SelectedNucValue);
        }

        public int GetSelectedValueSuggestedCommandIoa()
        {
            if (SelectedValue != null)
            {
                int? relatedCommandIoa = OfficialPointProfiles.TryGetRelatedCommandIoa(SelectedValue.IOA);
                if (relatedCommandIoa.HasValue)
                {
                    return relatedCommandIoa.Value;
                }
            }

            string family = GetSelectedValueCommandFamily();
            switch (family)
            {
                case "Single": return SingleCommandIoa;
                case "Double": return DoubleCommandIoa;
                case "Regulating": return StepCommandIoa;
                default: return SelectedValue != null ? SelectedValue.IOA : 0;
            }
        }

        public int GetSelectedNucValueSuggestedCommandIoa()
        {
            if (SelectedNucValue != null)
            {
                int? relatedCommandIoa = OfficialPointProfiles.TryGetRelatedCommandIoa(SelectedNucValue.IOA);
                if (relatedCommandIoa.HasValue)
                {
                    return relatedCommandIoa.Value;
                }
            }

            string family = GetSelectedNucValueCommandFamily();
            switch (family)
            {
                case "Single": return SingleCommandIoa;
                case "Double": return DoubleCommandIoa;
                case "Regulating": return StepCommandIoa;
                default: return SelectedNucValue != null ? SelectedNucValue.IOA : 0;
            }
        }

        public async Task SendSelectedValueCommandAsync(int ioa, string operation, bool select)
        {
            if (SelectedValue == null || !CanSendCommands)
                return;

            string family = GetSelectedValueCommandFamily();
            await SendSignalCommandAsync(family, ioa, operation, select);
        }

        public async Task SendSelectedNucValueCommandAsync(int ioa, string operation, bool select)
        {
            if (SelectedNucValue == null || !_nucRedundancyService.IsSessionActive)
                return;

            string family = GetSelectedNucValueCommandFamily();
            if (family == null)
                return;

            string normalizedOperation = NormalizeCommandOperation(family, operation);
            string commandType = GetCommandTypeForFamily(family);
            CommandTransaction transaction = RegisterPendingCommand(ioa, commandType, normalizedOperation, select);
            LogCommandTransmission(transaction);
            ScheduleCommandTimeoutCheck();

            if (family == "Single")
            {
                await _nucRedundancyService.SendSingleCommandAsync(
                    ioa,
                    string.Equals(normalizedOperation, "ON", StringComparison.OrdinalIgnoreCase),
                    select);
            }
            else if (family == "Double")
            {
                await _nucRedundancyService.SendDoubleCommandAsync(
                    ioa,
                    string.Equals(normalizedOperation, "CLOSE", StringComparison.OrdinalIgnoreCase),
                    select);
            }
            else if (family == "Regulating")
            {
                await _nucRedundancyService.SendStepCommandAsync(
                    ioa,
                    string.Equals(normalizedOperation, "RAISE", StringComparison.OrdinalIgnoreCase),
                    select);
            }
            else if (family == "Setpoint")
            {
                float normalizedValue;
                if (float.TryParse(normalizedOperation, NumberStyles.Float, CultureInfo.InvariantCulture, out normalizedValue))
                {
                    await _nucRedundancyService.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select);
                }
            }
        }

        public async Task SendNucSignalCommandAsync(string family, int ioa, string operation, bool select)
        {
            if (!_nucRedundancyService.IsSessionActive)
                return;

            if (family == null)
                return;

            string normalizedOperation = NormalizeCommandOperation(family, operation);
            string commandType = GetCommandTypeForFamily(family);
            CommandTransaction transaction = RegisterPendingCommand(ioa, commandType, normalizedOperation, select);
            LogCommandTransmission(transaction);
            ScheduleCommandTimeoutCheck();

            if (family == "Single")
            {
                await _nucRedundancyService.SendSingleCommandAsync(
                    ioa,
                    string.Equals(normalizedOperation, "ON", StringComparison.OrdinalIgnoreCase),
                    select);
            }
            else if (family == "Double")
            {
                await _nucRedundancyService.SendDoubleCommandAsync(
                    ioa,
                    string.Equals(normalizedOperation, "CLOSE", StringComparison.OrdinalIgnoreCase),
                    select);
            }
            else if (family == "Regulating")
            {
                await _nucRedundancyService.SendStepCommandAsync(
                    ioa,
                    string.Equals(normalizedOperation, "RAISE", StringComparison.OrdinalIgnoreCase),
                    select);
            }
            else if (family == "Setpoint")
            {
                float normalizedValue;
                if (float.TryParse(normalizedOperation, NumberStyles.Float, CultureInfo.InvariantCulture, out normalizedValue))
                {
                    await _nucRedundancyService.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select);
                }
            }
        }

        public async Task SendSignalCommandAsync(string family, int ioa, string operation, bool select)
        {
            if (!CanSendCommands)
                return;

            if (family == null)
                return;

            _isBusy = true;
            RefreshCommands();
            try
            {
                string commandType = GetCommandTypeForFamily(family);
                string normalizedOperation = NormalizeCommandOperation(family, operation);

                switch (family)
                {
                    case "Single":
                        await _masterService.SendSingleCommandAsync(
                            ioa,
                            string.Equals(normalizedOperation, "ON", StringComparison.OrdinalIgnoreCase),
                            select);
                        break;

                    case "Double":
                        await _masterService.SendDoubleCommandAsync(
                            ioa,
                            string.Equals(normalizedOperation, "CLOSE", StringComparison.OrdinalIgnoreCase),
                            select);
                        break;

                    case "Regulating":
                        await _masterService.SendStepCommandAsync(
                            ioa,
                            string.Equals(normalizedOperation, "RAISE", StringComparison.OrdinalIgnoreCase),
                            select);
                        break;

                    case "Setpoint":
                        float normalizedValue;
                        if (float.TryParse(normalizedOperation, NumberStyles.Float, CultureInfo.InvariantCulture, out normalizedValue))
                        {
                            await _masterService.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select);
                        }
                        break;
                }

                CommandTransaction transaction = RegisterPendingCommand(ioa, commandType, normalizedOperation, select);
                LogCommandTransmission(transaction);
                ScheduleCommandTimeoutCheck();
            }
            catch (Exception ex)
            {
                AddSystemLine("ERR", "Command send failed", ex.Message);
            }
            finally
            {
                _isBusy = false;
                RefreshCommands();
            }
        }

        public async Task SendSetpointCommandAsync(int ioa, float normalizedValue, bool select, bool useNucSession)
        {
            string operation = normalizedValue.ToString("0.###", CultureInfo.InvariantCulture);
            string commandType = GetCommandTypeForFamily("Setpoint");

            if (useNucSession)
            {
                if (!_nucRedundancyService.IsSessionActive)
                {
                    return;
                }

                CommandTransaction nucTransaction = RegisterPendingCommand(ioa, commandType, operation, select);
                LogCommandTransmission(nucTransaction);
                ScheduleCommandTimeoutCheck();
                await _nucRedundancyService.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select);
                return;
            }

            if (!CanSendCommands)
            {
                return;
            }

            _isBusy = true;
            RefreshCommands();
            try
            {
                await _masterService.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select);
                CommandTransaction transaction = RegisterPendingCommand(ioa, commandType, operation, select);
                LogCommandTransmission(transaction);
                ScheduleCommandTimeoutCheck();
            }
            catch (Exception ex)
            {
                AddSystemLine("ERR", "Setpoint send failed", ex.Message);
            }
            finally
            {
                _isBusy = false;
                RefreshCommands();
            }
        }
        private void MasterService_ConnectionStateChanged(object sender, ConnectionStatusInfo e)
        {
            RunOnUi(() =>
            {
                string newStatus = e != null ? e.DisplayText : ConnectionStatusInfo.Faulted.DisplayText;
                ConnectionStatus = newStatus;
                ConnectionDetail = e != null ? e.Detail : "Unknown communication state.";
                if (string.Equals(newStatus, "Connected", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(newStatus, "Disconnected", StringComparison.OrdinalIgnoreCase))
                {
                    _lastSlaveClass1Available = null;
                    _lastEventLogKey = null;
                    _lastPollEventClass = null;
                    _activeFindingKeys.Clear();
                    _findingEvidenceCounts.Clear();
                    _binaryClass2ChangeCounts.Clear();
                    _analogSpontCounts.Clear();
                    ResetBufferReplayState();
                    HasUnreadFindings = false;
                    _commandTracker.Clear();
                    _commandLifecycle.Clear();
                    ResetClass1BurstAnalysis();
                }

                ObserveBufferReplayConnectionState(newStatus);
                ObserveRedundancyConnectionState(newStatus);
                ObserveAvailabilityConnectionState(newStatus);
                AddSystemLine("STATE", ConnectionStatus, ConnectionDetail);

                if (ShouldCreateConnectionEvent(newStatus))
                {
                    AddStatusHistory(new StatusHistoryRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Status = newStatus,
                        Detail = ConnectionDetail,
                        Level = GetLevelForConnectionStatus(newStatus)
                    });

                    AddEventLog(new EventLogRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Name = "System",
                        IOA = "-",
                        Type = "Connection",
                        Event = newStatus,
                        Value = string.Empty,
                        Quality = string.Empty,
                        Acd = "-",
                        Cot = "-",
                        DataClass = "-"
                    });

                    _lastConnectionEvent = newStatus;
                }

                RefreshCommands();
            });
        }

        private void MasterService_LineMonitorRecordReceived(object sender, LineMonitorRow e)
        {
            if (e == null)
            {
                return;
            }

            RunOnUi(() =>
            {
                NormalizeLine(e);

                LineMonitorRow snapshot = BoundedUiBuffer.CreateLineSnapshot(e, e.Channel, MaxLineRawHexChars, MaxLineDetailChars);
                BoundedUiBuffer.InsertNewest(LineMonitor, snapshot, MaxLineMonitorRows);
                if (string.Equals(e.Direction, "TX", StringComparison.OrdinalIgnoreCase) || string.Equals(e.Direction, "RX", StringComparison.OrdinalIgnoreCase))
                {
                    PulseTraffic(e.Direction);
                }

                ProcessTimedOutCommandTransactions();
                ObserveAvailabilityLine(e);
                TryCreateStatusHistory(e);
                TryCreateCommandEvent(e);
            });
        }

        private void MasterService_ValueReceived(object sender, ValueViewerRow e)
        {
            if (e == null)
            {
                return;
            }

            if (e.IOA == 8388714)
            {
                AddStatusHistory(new StatusHistoryRow
                {
                    Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Status = "DBG",
                    Detail = string.Format("MasterService_ValueReceived IOA 8388714 Value={0} Cot={1} Ts={2}", e.Value ?? "-", e.Cot ?? "-", e.Timestamp ?? "-")
                });
            }

            RunOnUi(() =>
            {
                string oldValue = null;
                if (_valueIndex.TryGetValue(e.IOA, out ValueViewerRow existing))
                {
                    oldValue = existing.Value;
                    existing.Name = e.Name;
                    existing.Type = e.Type;
                    string previousValue = existing.Value;
                    existing.Value = e.Value;
                    existing.Quality = e.Quality;
                    if (e.HasProtocolTimestamp)
                    {
                        existing.EventTimestampUtc = e.EventTimestampUtc;
                        existing.HasProtocolTimestamp = true;
                        existing.SourceType = e.SourceType;
                        existing.Timestamp = e.EventTimestampUtc.HasValue
                            ? e.EventTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff")
                            : "-";
                    }
                    else
                    {
                        existing.SnapshotTimestampUtc = e.SnapshotTimestampUtc;
                        if (string.IsNullOrWhiteSpace(existing.Timestamp) || existing.Timestamp == "-")
                        {
                            existing.Timestamp = e.SnapshotTimestampUtc.HasValue
                                ? e.SnapshotTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff")
                                : "-";
                        }
                    }

                    if (ShouldOverwriteMetadata(existing.Cot, e.Cot))
                    {
                        existing.Acd = e.Acd;
                        existing.Cot = e.Cot;
                        existing.TrafficClass = e.TrafficClass;
                        existing.DeliveryContext = e.DeliveryContext;
                    }
                }
                else
                {
                    e.No = Values.Count + 1;
                    if (e.HasProtocolTimestamp)
                    {
                        e.Timestamp = e.EventTimestampUtc.HasValue
                            ? e.EventTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff")
                            : "-";
                    }
                    else if (e.SnapshotTimestampUtc.HasValue)
                    {
                        e.Timestamp = e.SnapshotTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    }
                    Values.Add(e);
                    _valueIndex[e.IOA] = e;
                }

                if (_giInProgress)
                {
                    _giReceivedIoas.Add(e.IOA);

                    string family = GetSignalCommandFamily(e);

                    if (IsDiscreteType(e.Type))
                    {
                        _giDiscreteIoas.Add(e.IOA);
                    }
                    else if (IsMeteringType(e.Type))
                    {
                        _giAnalogIoas.Add(e.IOA);
                    }
                    else if (family != null)
                    {
                        _giCommandIoas.Add(e.IOA);
                    }
                }

                RefreshCommandSignals();

                string effectiveTimestamp = _valueIndex[e.IOA].HasProtocolTimestamp
                    ? (_valueIndex[e.IOA].EventTimestampUtc.HasValue ? _valueIndex[e.IOA].EventTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff") : "-")
                    : (_valueIndex[e.IOA].SnapshotTimestampUtc.HasValue ? _valueIndex[e.IOA].SnapshotTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff") : "-");

                if (string.Equals(e.TrafficClass, "Class 1", StringComparison.OrdinalIgnoreCase))
                {
                    NoteClass1BurstValue(e.IOA, e.Type);
                }

                TryCreateDiscreteEvent(
                    e.IOA,
                    e.Name,
                    e.Type,
                    oldValue,
                    e.Value,
                    e.Quality,
                    e.Acd,
                    e.Cot,
                    e.TrafficClass,
                    effectiveTimestamp,
                    e.HasProtocolTimestamp || string.Equals(e.SourceType, "SPONT", StringComparison.OrdinalIgnoreCase));
                TryCreateClassMismatchFinding(e.IOA, e.Type, e.TrafficClass, e.Acd, e.Cot);
                TryCreateAcdExpectationFinding(e.IOA, e.Type, e.Acd, e.Cot, e.TrafficClass, e.DeliveryContext);
            });
        }

        private void RefreshCommandSignals()
        {
            CommandSignals.Clear();
            foreach (ValueViewerRow row in Values)
            {
                if (GetSignalCommandFamily(row) != null)
                {
                    CommandSignals.Add(row);
                }
            }

            if (SelectedValue != null && GetSignalCommandFamily(SelectedValue) == null)
            {
                SelectedValue = null;
            }

            if (SelectedValue == null && CommandSignals.Count > 0)
            {
                SelectedValue = CommandSignals[0];
            }
        }

        private void TryCreateDiscreteEvent(int ioa, string name, string type, string oldValue, string newValue, string quality, string acd, string cot, string dataClass, string timestamp, bool isProtocolEvent)
        {
            if (!IsDiscreteType(type))
            {
                return;
            }

            string previous;
            bool hadPrevious = _lastDiscreteStates.TryGetValue(ioa, out previous);
            if (hadPrevious && string.Equals(previous, newValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastDiscreteStates[ioa] = newValue ?? string.Empty;

            if (!isProtocolEvent && string.Equals(cot, "GI", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string eventClass = dataClass;
            if (string.Equals(cot, "Spont", StringComparison.OrdinalIgnoreCase))
            {
                eventClass = "-";
            }
            else if (string.Equals(cot, "GI", StringComparison.OrdinalIgnoreCase))
            {
                eventClass = "Class 2";
            }

            TryAddEventLog(new EventLogRow
            {
                Time = NormalizeTimestamp(timestamp),
                Name = name,
                IOA = ioa.ToString(),
                Type = type,
                Event = hadPrevious ? "Value changed" : "Initial value",
                Value = hadPrevious ? previous + " -> " + newValue : newValue,
                Quality = quality,
                Acd = acd,
                Cot = cot,
                DataClass = eventClass
            });

            ObserveBufferReplayEvent(ioa, type, previous, newValue, timestamp, cot);
            ObserveRedundancyDiscreteEvent(ioa, name, type, previous, newValue, cot, timestamp);
        }

        private void TryCreateCommandEvent(LineMonitorRow row)
        {
            if (row == null)
            {
                return;
            }

            if (row.Direction == "STATE")
            {
                if (row.Summary.IndexOf("GI command sent", StringComparison.OrdinalIgnoreCase) >= 0
                    || row.Summary.IndexOf("Clock sync sent", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _giInProgress = true;
                    _giStartTime = DateTime.Now;
                    _giLastCompletedUtc = DateTime.MinValue;

                    _giReceivedIoas.Clear();
                    _giDiscreteIoas.Clear();
                    _giAnalogIoas.Clear();
                    _giCommandIoas.Clear();
                    TryAddEventLog(new EventLogRow
                    {
                        Time = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                        Name = "Command",
                        IOA = "-",
                        Type = row.Summary.IndexOf("Clock sync", StringComparison.OrdinalIgnoreCase) >= 0 ? "Clock Sync" : "GI",
                        Event = "🢂 " + row.Summary,
                        Value = string.Empty,
                        Quality = string.Empty,
                        Acd = "-",
                        Cot = "-",
                        Source = "Master",
                        DataClass = string.IsNullOrWhiteSpace(row.DataClass) ? "-" : row.DataClass
                    });

                    if (row.Summary.IndexOf("GI command sent", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ObserveRedundancyGiEvent("GI sent", DateTime.Today.ToString("yyyy-MM-dd ") + row.Time);
                    }
                }

                return;
            }

            bool isRx = string.Equals(row.Direction, "RX", StringComparison.OrdinalIgnoreCase);

            if (isRx)
            {
                NoteClass1BurstAsdu(row);
                string eventTimeText = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time;

                if (string.Equals(row.ACD, "1", StringComparison.OrdinalIgnoreCase))
                {
                    if (_lastSlaveClass1Available != true)
                    {
                        _lastSlaveClass1Available = true;
                        StartClass1Burst(DateTime.UtcNow);

                    TryAddEventLog(new EventLogRow
                    {
                        Time = eventTimeText,
                            Name = "System",
                            IOA = "-",
                            Type = "Class 1",
                            Event = "⚡ Slave has Class 1 data pending",
                            Value = string.Empty,
                            Quality = string.Empty,
                            Acd = "-",
                            Cot = "-",
                            Source = "System",
                            DataClass = "Class 1"
                        });
                    }
                }
                else if (string.Equals(row.ACD, "0", StringComparison.OrdinalIgnoreCase))
                {
                    if (_lastSlaveClass1Available != false)
                    {
                        _lastSlaveClass1Available = false;
                        TryAddEventLog(new EventLogRow
                        {
                            Time = eventTimeText,
                            Name = "System",
                            IOA = "-",
                            Type = "Class 1",
                            Event = "⚡ Class 1 queue cleared",
                            Value = string.Empty,
                            Quality = string.Empty,
                            Acd = "-",
                            Cot = "-",
                            Source = "System",
                            DataClass = "Class 2"
                        });

                        ScheduleCompleteClass1Burst(eventTimeText);
                    }
                }

                if (row.AsduType.IndexOf("C_IC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                    && row.COT.IndexOf("ACTIVATION TERMINATION", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (_giInProgress)
                    {
                        TimeSpan giDuration = DateTime.Now - _giStartTime;

                        TryAddEventLog(new EventLogRow
                        {
                            Time = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                            Name = "System",
                            IOA = "-",
                            Type = "GI",
                            Event = string.Format(CultureInfo.InvariantCulture, "⚡ GI duration: {0:F0} ms", giDuration.TotalMilliseconds),
                            Value = string.Empty,
                            Quality = string.Empty,
                            Acd = "-",
                            Cot = "GI",
                            Source = "System",
                            DataClass = "Class 2"
                        });

                        TryAddEventLog(new EventLogRow
                        {
                            Time = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                            Name = "System",
                            IOA = "-",
                            Type = "GI",
                            Event = string.Format(CultureInfo.InvariantCulture, "⚡ GI objects: {0} ({1} binary, {2} analog)", _giReceivedIoas.Count, _giDiscreteIoas.Count, _giAnalogIoas.Count),
                            Value = string.Empty,
                            Quality = string.Empty,
                            Acd = "-",
                            Cot = "GI",
                            Source = "System",
                            DataClass = "Class 2"
                        });

                        _giInProgress = false;
                        _giLastCompletedUtc = DateTime.UtcNow;
                    }

                    TryAddEventLog(new EventLogRow
                    {
                        Time = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                        Name = "System",
                        IOA = "-",
                        Type = "GI",
                        Event = "🢀 GI completed",
                        Value = string.Empty,
                        Quality = string.Empty,
                        Acd = "-",
                        Cot = "ActTerm",
                        Source = "System",
                        DataClass = "Class 2"
                    });

                    ObserveRedundancyGiEvent("GI completed", eventTimeText);
                }
            }

            if (isRx
                && HasDecodedAsdu(row)
                && (row.AsduType.IndexOf("C_SC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                    || row.AsduType.IndexOf("C_DC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                    || row.AsduType.IndexOf("C_RC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                bool isNegative = IsNegativeConfirmation(row);
                string ioa = string.IsNullOrWhiteSpace(row.IOA) ? ExtractIoaFromDetail(row.Detail) : row.IOA;
                string commandType = GetCommandFamilyLabel(row.AsduType);
                string operation = NormalizeCommandOperation(commandType, GetCommandOperationLabel(row));
                string rxMode = TryGetRxModeLabel(row);
                CommandTransaction transaction = ResolvePendingCommand(ioa, commandType, operation, rxMode, row, isNegative);
                if (transaction == null && (string.IsNullOrWhiteSpace(ioa) || ioa == "-"))
                {
                    return;
                }

                if (transaction != null)
                {
                    ioa = transaction.CommandIoa;
                }

                if (string.Equals(row.DataClass, "Class 1", StringComparison.OrdinalIgnoreCase))
                {
                    NoteClass1BurstCommand(ioa);
                }

                string commandMode = transaction != null ? transaction.Mode : (rxMode ?? "DO");
                double? confirmMs = transaction != null ? transaction.ConfirmLatencyMs : (double?)null;
                operation = transaction != null ? transaction.Operation : operation;

                string rxStage;
                if (string.Equals(commandMode, "SBO Select", StringComparison.OrdinalIgnoreCase))
                {
                    rxStage = "SelectRx";
                }
                else if (string.Equals(commandMode, "SBO Execute", StringComparison.OrdinalIgnoreCase))
                {
                    rxStage = "ExecuteRx";
                }
                else
                {
                    rxStage = "DoRx";
                }

                TrackCommandLifecycle(ioa, rxStage, operation, isNegative);
                AddCommandLifeMonitorRow(transaction, isNegative ? "REJ" : "OK");

                TryAddEventLog(new EventLogRow
                {
                    Time = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                    Name = "Command",
                    IOA = ioa,
                    Type = commandType,
                    Event = isNegative
                        ? "🢀 " + commandMode + " rejected"
                        : "🢀 " + commandMode + " confirmed",
                    Value = operation,
                    Quality = string.Empty,
                    Acd = "-",
                    Cot = string.IsNullOrWhiteSpace(row.COT) ? "-" : row.COT,
                    Source = "RTU",
                    DataClass = string.IsNullOrWhiteSpace(row.DataClass) ? "-" : row.DataClass
                });

                if (confirmMs.HasValue)
                {
                    TryAddEventLog(new EventLogRow
                    {
                        Time = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                        Name = "System",
                        IOA = ioa,
                        Type = commandType,
                        Event = string.Format(CultureInfo.InvariantCulture, "⚡ Command confirm time: {0:F0} ms", confirmMs.Value),
                        Value = operation,
                        Quality = string.Empty,
                        Acd = "-",
                        Cot = "Latency",
                        Source = "System",
                        DataClass = string.IsNullOrWhiteSpace(row.DataClass) ? "-" : row.DataClass
                    });
                }

                return;
            }

            if (!string.Equals(row.Direction, "TX", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (row.ControlFc.IndexOf("FC=10", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!string.Equals(_lastPollEventClass, "Class 1", StringComparison.OrdinalIgnoreCase))
                {
                    _lastPollEventClass = "Class 1";
                    TryAddEventLog(new EventLogRow
                    {
                        Time = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                        Name = "System",
                        IOA = "-",
                        Type = "Poll",
                        Event = "🢂 Master polling Class 1 data",
                        Value = string.Empty,
                        Quality = string.Empty,
                        Acd = "-",
                        Cot = "-",
                        Source = "Master",
                        DataClass = "Class 1"
                    });
                }

                return;
            }

            if (row.ControlFc.IndexOf("FC=11", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _lastPollEventClass = "Class 2";
            }
        }
        private void TryCreateStatusHistory(LineMonitorRow row)
        {
            if (row.Direction != "STATE")
            {
                return;
            }

            if (row.Summary.IndexOf("ACD", StringComparison.OrdinalIgnoreCase) >= 0
                || row.Summary.IndexOf("Polling switched", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddStatusHistory(new StatusHistoryRow
                {
                    Time = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                    Status = row.Summary,
                    Detail = string.IsNullOrWhiteSpace(row.DataClass) || row.DataClass == "-" ? row.Detail : row.DataClass + " | " + row.Detail,
                    Level = ToStatusLevel(row.FrameType)
                });
            }
        }

        private void TryCreateClassMismatchFinding(int ioa, string type, string dataClass, string acd, string cot)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(dataClass))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(cot))
            {
                return;
            }

            // Jangan diagnosa dari GI / startup / activation flow
            if (string.Equals(cot, "GI", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "InterrogatedByStation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "ACTIVATION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "ACTIVATION CON", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "ACTIVATION TERMINATION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "ActCon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "ActTerm", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string key = null;
            FindingRow finding = null;
            PointDefinition pointDefinition;
            OfficialPointProfiles.TryGetPointByIoa(ioa, out pointDefinition);
            string pointLabel = pointDefinition != null ? pointDefinition.DisplayName : type;
            string expectedClass = pointDefinition != null && !string.IsNullOrWhiteSpace(pointDefinition.IecClass)
                ? pointDefinition.IecClass
                : null;

            if (IsDiscreteType(type)
                && string.Equals(cot, "Spont", StringComparison.OrdinalIgnoreCase))
            {
                _binaryClass2ChangeCounts[ioa] = 0;
                return;
            }

            if (IsDiscreteType(type)
                && string.Equals(dataClass, "Class 2", StringComparison.OrdinalIgnoreCase)
                && string.Equals(cot, "BgScan", StringComparison.OrdinalIgnoreCase))
            {
                int evidenceCount = 0;
                _binaryClass2ChangeCounts.TryGetValue(ioa, out evidenceCount);
                evidenceCount++;
                _binaryClass2ChangeCounts[ioa] = evidenceCount;

                if (evidenceCount >= 3)
                {
                    key = "DISC:CLASS2ONLY:" + ioa;
                    finding = new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Major",
                        Category = "ClassBehavior",
                        RuleCode = "CLASS2_MISCLASSIFIED",
                        Title = "Binary signal changing only in Class 2 scan",
                        Detail = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} IOA {1} changed {2} times through background scan (Class 2) without any spontaneous/Class 1 indication in the same observation window. Expected class behavior: {3}. Check RTU event class assignment.",
                            pointLabel,
                            ioa,
                            evidenceCount,
                            string.IsNullOrWhiteSpace(expectedClass) ? "Class 1" : expectedClass),
                        IOA = ioa.ToString(),
                        Type = type,
                        ExpectedClass = string.IsNullOrWhiteSpace(expectedClass) ? "Class 1" : expectedClass,
                        ActualClass = "Class 2"
                    };
                }

                if (finding == null)
                {
                    return;
                }
            }
            else if (IsMeteringType(type)
                && string.Equals(dataClass, "Class 1", StringComparison.OrdinalIgnoreCase)
                && string.Equals(cot, "Spont", StringComparison.OrdinalIgnoreCase))
            {
                int evidenceCount = 0;
                _analogSpontCounts.TryGetValue(ioa, out evidenceCount);
                evidenceCount++;
                _analogSpontCounts[ioa] = evidenceCount;

                if (evidenceCount >= 2)
                {
                    key = "ANLG:SPONT:" + ioa;
                    finding = new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Info",
                        Category = "ClassBehavior",
                        RuleCode = "ANALOG_SPONTANEOUS_PROFILE",
                        Title = "Analog signal arriving as spontaneous/Class 1",
                        Detail = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} IOA {1} arrived spontaneously as Class 1 {2} times. Expected class behavior: {3}. Verify whether this analog signal is intentionally configured as spontaneous.",
                            pointLabel,
                            ioa,
                            evidenceCount,
                            string.IsNullOrWhiteSpace(expectedClass) ? "Class 2" : expectedClass),
                        IOA = ioa.ToString(),
                        Type = type,
                        ExpectedClass = string.IsNullOrWhiteSpace(expectedClass) ? "Class 2" : expectedClass,
                        ActualClass = "Class 1"
                    };
                }
            }
            else
            {
                if (IsDiscreteType(type))
                {
                    _binaryClass2ChangeCounts[ioa] = 0;
                }

                if (IsMeteringType(type))
                {
                    _analogSpontCounts[ioa] = 0;
                }

                return;
            }

            if (finding == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (_activeFindingKeys.Add(key))
            {
                BoundedUiBuffer.InsertNewest(Findings, finding, MaxFindingRows);
                HasUnreadFindings = true;
                return;
            }

            HasUnreadFindings = true;
            for (int index = 0; index < Findings.Count; index++)
            {
                FindingRow existing = Findings[index];
                if (existing == null)
                {
                    continue;
                }

                if (string.Equals(existing.IOA, finding.IOA, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Title, finding.Title, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    existing.Detail = finding.Detail;
                    break;
                }
            }
        }

        public ValueViewerRow SelectedNucValue
        {
            get => _selectedNucValue;
            set
            {
                if (SetProperty(ref _selectedNucValue, value))
                {
                    OnPropertyChanged(nameof(CanOpenSelectedNucValueCommand));
                }
            }
        }

        public bool CanOpenSelectedNucValueCommand => GetSignalCommandFamily(SelectedNucValue) != null;
        public bool IsRedundancyMainFaultActive => _mainLinkFaultActive == true;
        public bool IsRedundancyBackupFaultActive => _backupLinkFaultActive == true;
        public bool IsRedundancyIedFaultActive => _iedFaultActive == true;

        private void TryCreateAcdExpectationFinding(int ioa, string type, string acd, string cot, string trafficClass, string deliveryContext)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(cot))
            {
                return;
            }

            if (!string.Equals(cot, "Spont", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PointDefinition pointDefinition;
            if (!OfficialPointProfiles.TryGetPointByIoa(ioa, out pointDefinition) || pointDefinition == null)
            {
                return;
            }

            if (!string.Equals(pointDefinition.IecClass, "Class 1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Command feedback/status points may legitimately arrive as spontaneous follow-up
            // to a user command without giving us a reliable ACD observation on the same event.
            if (!string.IsNullOrWhiteSpace(pointDefinition.RelatedCommandPointKey))
            {
                return;
            }

            if (string.Equals(acd, "1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // ACD is a secondary link-layer access-demand hint, not proof that every
            // current ASDU must carry ACD=1. When a spontaneous Class 1 object is
            // already delivered through an FC10/Class 1 response, ACD may be cleared
            // because no additional Class 1 data remains pending. Treat that as a
            // healthy delivery path instead of a finding.
            if (string.Equals(trafficClass, "Class 1", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(deliveryContext)
                    && deliveryContext.IndexOf("FC10", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return;
            }

            AddFindingOnce("ACD:MISSING:" + ioa, new FindingRow
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Severity = "Warning",
                Category = "ACD",
                RuleCode = "ACD_EXPECTED_NOT_OBSERVED",
                Title = "Class 1 profile arrived without access-demand evidence",
                Detail = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} IOA {1} is profiled as {2} and arrived with spontaneous behavior, but ACD=1 was not observed.",
                    pointDefinition.DisplayName,
                    ioa,
                    pointDefinition.IecClass),
                IOA = ioa.ToString(),
                Type = type,
                ExpectedClass = pointDefinition.IecClass,
                ActualClass = string.IsNullOrWhiteSpace(acd) || acd == "-" ? "ACD unknown" : "ACD=" + acd
            });
        }

        private void PulseTraffic(string direction)
        {
            if (string.Equals(direction, "TX", StringComparison.OrdinalIgnoreCase))
            {
                IsTxActive = true;
                Task.Delay(220).ContinueWith(_ => RunOnUi(() => IsTxActive = false));
            }
            else if (string.Equals(direction, "RX", StringComparison.OrdinalIgnoreCase))
            {
                IsRxActive = true;
                Task.Delay(220).ContinueWith(_ => RunOnUi(() => IsRxActive = false));
            }
        }
        private void AddSystemLine(string direction, string summary, string detail)
        {
            MasterService_LineMonitorRecordReceived(this, new LineMonitorRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = direction,
                FrameType = "Info",
                Summary = summary ?? string.Empty,
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
            });
        }

        private void AddEventLog(EventLogRow row)
        {
            if (row == null)
            {
                return;
            }

            BoundedUiBuffer.InsertNewest(EventLog, row, MaxEventLogRows);
            ReindexEventLogRows(EventLog);
            _availabilityObservedEventCount++;
            RefreshAvailabilityTelemetry();
            ReindexEventLogRows(EventLog);
        }

        private static void ReindexEventLogRows(IList<EventLogRow> rows)
        {
            if (rows == null)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].No = i + 1;
            }
        }

        private void TryAddEventLog(EventLogRow row)
        {
            if (row == null)
            {
                return;
            }

            string signature = string.Format(
                "{0}|{1}|{2}|{3}|{4}",
                row.Name ?? string.Empty,
                row.IOA ?? string.Empty,
                row.Type ?? string.Empty,
                row.Event ?? string.Empty,
                row.Value ?? string.Empty);

            if (string.Equals(_lastEventLogKey, signature, StringComparison.Ordinal))
            {
                return;
            }

            _lastEventLogKey = signature;
            AddEventLog(row);
        }

        private void AddNucEventLog(EventLogRow row)
        {
            if (row == null)
            {
                return;
            }

            if (string.Equals(row.Name, "System", StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.IOA, "-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.Type, "State", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string signature = string.Format(
                "{0}|{1}|{2}|{3}|{4}",
                row.Source ?? string.Empty,
                row.Name ?? string.Empty,
                row.IOA ?? string.Empty,
                row.Event ?? string.Empty,
                row.Value ?? string.Empty);

            if (string.Equals(_lastNucEventLogKey, signature, StringComparison.Ordinal))
            {
                return;
            }

            _lastNucEventLogKey = signature;

            BoundedUiBuffer.InsertNewest(NucEventLog, row, MaxNucEventLogRows);
        }

        private void AddNucSoeAuditRow(EventLogRow row)
        {
            if (row == null)
            {
                return;
            }

            if (string.Equals(row.Name, "System", StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.IOA, "-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.Type, "State", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string signature = string.Format(
                "{0}|{1}|{2}|{3}|{4}|{5}",
                row.Time ?? string.Empty,
                row.Source ?? string.Empty,
                row.Name ?? string.Empty,
                row.IOA ?? string.Empty,
                row.Event ?? string.Empty,
                row.Value ?? string.Empty);

            if (string.Equals(_lastNucSoeAuditKey, signature, StringComparison.Ordinal))
            {
                return;
            }

            _lastNucSoeAuditKey = signature;

            BoundedUiBuffer.InsertNewest(NucSoeAuditLog, row, MaxNucSoeAuditRows);
        }

        private void AppendNucSoeForensicRow(SoeForensicRow row)
        {
            _nucSoeForensicJournal.Append(row);
        }

        private void AppendDecodedNucSoeForensicRow(ValueViewerRow value, string channelName)
        {
            if (value == null)
            {
                return;
            }

            AppendNucSoeForensicRow(new SoeForensicRow
            {
                RecvTimeUtc = value.ReceiveTimestampUtc == default(DateTime) ? DateTime.UtcNow : value.ReceiveTimestampUtc,
                SourceTimeUtc = value.EventTimestampUtc,
                DeltaMs = value.EventTimestampUtc.HasValue
                    ? (int?)Math.Round((value.ReceiveTimestampUtc - value.EventTimestampUtc.Value).TotalMilliseconds, MidpointRounding.AwayFromZero)
                    : null,
                Channel = string.IsNullOrWhiteSpace(channelName) ? "Main" : channelName,
                CA = ParseIntOrDefault(value.Casdu),
                IOA = value.IOA,
                TypeId = value.TypeIdRaw,
                TypeIdText = value.TypeId,
                CotText = value.Cot,
                CotRaw = value.CotRaw,
                SignalName = value.Name,
                ValueText = value.Value,
                QualityText = value.Quality,
                Origin = "Value",
                DeliveryContext = string.IsNullOrWhiteSpace(value.DeliveryContext) ? "Unknown" : value.DeliveryContext,
                ClassContext = string.IsNullOrWhiteSpace(value.TrafficClass) ? "Unknown" : value.TrafficClass
            });
        }

        private void AppendCommandNucSoeForensicRow(string eventTimeText, string channelName, string ioa, string commandType, string asduType, string casdu, string cot, string operation, string eventText)
        {
            DateTime recvUtc;
            if (!TryParseEventTimestampUtc(eventTimeText, out recvUtc))
            {
                recvUtc = DateTime.UtcNow;
            }

            int parsedIoa;
            int.TryParse(ioa ?? string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedIoa);
            AppendNucSoeForensicRow(new SoeForensicRow
            {
                RecvTimeUtc = recvUtc,
                SourceTimeUtc = null,
                DeltaMs = null,
                Channel = string.IsNullOrWhiteSpace(channelName) ? "Main" : channelName,
                CA = ParseIntOrDefault(casdu),
                IOA = parsedIoa,
                TypeId = ParseTypeIdOrDefault(asduType),
                TypeIdText = string.IsNullOrWhiteSpace(asduType) ? commandType : asduType,
                CotText = string.IsNullOrWhiteSpace(cot) ? "-" : cot,
                CotRaw = ParseCotOrDefault(cot),
                SignalName = "Command",
                ValueText = operation ?? "-",
                QualityText = "-",
                Origin = "Command",
                DeliveryContext = "Command lifecycle",
                ClassContext = "Class 1"
            });
        }

        private void AppendRedundancyNucSoeForensicRow(string timestampText, string channelName, int ioa, string signalName, string type, string cot, string valueText)
        {
            DateTime recvUtc;
            if (!TryParseEventTimestampUtc(timestampText, out recvUtc))
            {
                recvUtc = DateTime.UtcNow;
            }

            AppendNucSoeForensicRow(new SoeForensicRow
            {
                RecvTimeUtc = recvUtc,
                SourceTimeUtc = recvUtc,
                DeltaMs = 0,
                Channel = channelName,
                CA = 0,
                IOA = ioa,
                TypeId = ParseTypeIdOrDefault(type),
                TypeIdText = type ?? "-",
                CotText = string.IsNullOrWhiteSpace(cot) ? "-" : cot,
                CotRaw = ParseCotOrDefault(cot),
                SignalName = signalName,
                ValueText = valueText,
                QualityText = "-",
                Origin = "Redundancy",
                DeliveryContext = string.Equals(cot, "GI", StringComparison.OrdinalIgnoreCase) ? "GI Response" : "Unknown",
                ClassContext = string.Equals(cot, "GI", StringComparison.OrdinalIgnoreCase) ? "Class 2" : "Unknown"
            });
        }

        private static int ParseIntOrDefault(string text)
        {
            int parsed;
            return int.TryParse(text ?? string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static int ParseTypeIdOrDefault(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            string normalized = text.Trim();
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                int raw;
                return int.TryParse(normalized.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out raw) ? raw : 0;
            }

            Iec101TypeId parsed;
            return Enum.TryParse(normalized, true, out parsed) ? (int)parsed : 0;
        }

        private static int ParseCotOrDefault(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            string normalized = text.Trim().Replace(" ", string.Empty).Replace("_", string.Empty);
            int raw;
            if (normalized.StartsWith("COT", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(normalized.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out raw))
            {
                return raw;
            }

            switch (normalized.ToUpperInvariant())
            {
                case "PERIODIC": return (int)Iec101CauseOfTransmission.Periodic;
                case "BGSCAN":
                case "BACKGROUNDSCAN": return (int)Iec101CauseOfTransmission.BackgroundScan;
                case "SPONT":
                case "SPONTANEOUS": return (int)Iec101CauseOfTransmission.Spontaneous;
                case "GI":
                case "INTERROGATEDBYSTATION": return (int)Iec101CauseOfTransmission.InterrogatedByStation;
                case "ACT":
                case "ACTIVATION": return (int)Iec101CauseOfTransmission.Activation;
                case "ACTCON":
                case "ACTIVATIONCON": return (int)Iec101CauseOfTransmission.ActivationCon;
                case "ACTTERM":
                case "ACTIVATIONTERMINATION": return (int)Iec101CauseOfTransmission.ActivationTermination;
                case "REQ":
                case "REQUEST": return (int)Iec101CauseOfTransmission.Request;
                case "INIT":
                case "INITIALIZED": return (int)Iec101CauseOfTransmission.Initialized;
                default:
                    Iec101CauseOfTransmission parsed;
                    return Enum.TryParse(text.Trim().Replace(" ", string.Empty), true, out parsed) ? (int)parsed : 0;
            }
        }

        private void AddNucLineMonitorRow(LineMonitorRow row, string channelName)
        {
            if (row == null)
            {
                return;
            }

            string signature = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}",
                channelName ?? string.Empty,
                row.Time ?? string.Empty,
                row.Direction ?? string.Empty,
                row.FrameType ?? string.Empty,
                row.Summary ?? string.Empty,
                row.Detail ?? string.Empty);

            if (string.Equals(_lastNucLineMonitorKey, signature, StringComparison.Ordinal))
            {
                return;
            }

            _lastNucLineMonitorKey = signature;

            LineMonitorRow snapshot = BoundedUiBuffer.CreateLineSnapshot(row, channelName, MaxNucTraceRawHexChars, MaxNucTraceDetailChars);
            if (snapshot != null && string.IsNullOrWhiteSpace(snapshot.Summary))
            {
                snapshot.Summary = channelName;
            }

            BoundedUiBuffer.InsertNewest(NucLineMonitor, snapshot, MaxNucLineMonitorRows);
        }

        private void AddNucTraceRow(LineMonitorRow row, string channelName)
        {
            if (row == null)
            {
                return;
            }

            ObservableCollection<LineMonitorRow> target = string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase)
                ? NucTraceLinkB
                : NucTraceLinkA;

            BoundedUiBuffer.InsertNewest(
                target,
                BoundedUiBuffer.CreateLineSnapshot(row, channelName, MaxNucTraceRawHexChars, MaxNucTraceDetailChars),
                MaxNucTraceRows);
        }

        private bool ShouldCoalesceNucStandbyLineMonitorRow(LineMonitorRow row, string channelName)
        {
            if (row == null)
            {
                return false;
            }

            string summary = row.Summary ?? string.Empty;
            string detail = row.Detail ?? string.Empty;
            bool isStandbyChurn = summary.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("standby supervision", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("standby supervision", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isStandbyChurn)
            {
                return false;
            }

            string throttleKey = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                channelName ?? string.Empty,
                row.Direction ?? string.Empty,
                row.FrameType ?? string.Empty,
                summary,
                detail,
                row.ACD ?? string.Empty,
                row.DFC ?? string.Empty);

            DateTime nowUtc = DateTime.UtcNow;
            DateTime lastSeenUtc;
            if (_nucLineMonitorThrottleMap.TryGetValue(throttleKey, out lastSeenUtc)
                && nowUtc - lastSeenUtc < TimeSpan.FromSeconds(3))
            {
                _nucLineMonitorThrottleMap[throttleKey] = nowUtc;
                return true;
            }

            _nucLineMonitorThrottleMap[throttleKey] = nowUtc;
            return false;
        }

        private static bool IsGatewayFaultPoint(int ioa)
        {
            PointDefinition point;
            return OfficialPointProfiles.TryGetPointByIoa(ioa, out point)
                && point != null
                && (string.Equals(point.PointKey, "GatewayMainLinkFault", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(point.PointKey, "GatewayBackupLinkFault", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(point.PointKey, "GatewayIedFaulty", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsNucScadaEventCandidate(ValueViewerRow value)
        {
            if (value == null)
            {
                return false;
            }

            string type = value.Type ?? string.Empty;
            if (type.IndexOf("Measured", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Normalized", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Float", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Bitstring", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Step", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Setpoint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            bool isBinary = type.IndexOf("Single", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Double", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isBinary)
            {
                return false;
            }

            string cot = value.Cot ?? string.Empty;
            return cot.IndexOf("Spont", StringComparison.OrdinalIgnoreCase) >= 0
                || cot.IndexOf("Cmd", StringComparison.OrdinalIgnoreCase) >= 0
                || cot.IndexOf("Act", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNucCommandMonitorEvent(LineMonitorRow row)
        {
            if (row == null)
            {
                return false;
            }

            string summary = row.Summary ?? string.Empty;
            string detail = row.Detail ?? string.Empty;
            return summary.IndexOf("command", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("execute", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("select", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("SBO", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("command", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNucOperatorStateEvent(LineMonitorRow row)
        {
            if (row == null)
            {
                return false;
            }

            string summary = row.Summary ?? string.Empty;
            string detail = row.Detail ?? string.Empty;

            if (summary.IndexOf("Polling switched", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("ACD asserted", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("ACD cleared", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("Polling switched", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("ACD asserted", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("ACD cleared", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (summary.IndexOf("GI command sent", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("GI completed", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("fault", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("switchover", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("switched to", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("fault", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("switchover", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("switched to", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private void UpsertNucValue(ValueViewerRow source, string channel)
        {
            if (source == null)
            {
                return;
            }

            string incomingTimestamp = GetEffectiveValueTimestampText(source);
            bool isDiscrete = IsDiscreteType(source.Type);
            bool isGi = string.Equals(source.Cot, "GI", StringComparison.OrdinalIgnoreCase);

            if (_nucValueIndex.TryGetValue(source.IOA, out ValueViewerRow existing))
            {
                bool valueChanged = !string.Equals(existing.Value, source.Value, StringComparison.OrdinalIgnoreCase);
                existing.Name = OfficialPointProfiles.GetDisplayNameOrDefault(source.IOA, source.Name);
                existing.Type = source.Type;
                existing.Value = source.Value;
                existing.Quality = source.Quality;
                existing.ReceiveTimestampUtc = source.ReceiveTimestampUtc;
                existing.ReceiveTimestampText = source.ReceiveTimestampText;
                existing.EventTimestampUtc = source.EventTimestampUtc;
                existing.SnapshotTimestampUtc = source.SnapshotTimestampUtc;
                existing.HasProtocolTimestamp = source.HasProtocolTimestamp;
                existing.SourceType = source.SourceType;
                if (!(isDiscrete && !valueChanged && !string.IsNullOrWhiteSpace(existing.Timestamp) && existing.Timestamp != "-")
                    && !(isDiscrete && isGi && !string.IsNullOrWhiteSpace(existing.Timestamp) && existing.Timestamp != "-"))
                {
                    existing.Timestamp = incomingTimestamp;
                }
                existing.UpdateSource = channel;
                if (ShouldOverwriteMetadata(existing.Cot, source.Cot))
                {
                    existing.Acd = source.Acd;
                    existing.Cot = source.Cot;
                    existing.TrafficClass = string.IsNullOrWhiteSpace(source.TrafficClass) ? "-" : source.TrafficClass;
                    existing.DeliveryContext = string.IsNullOrWhiteSpace(source.DeliveryContext) ? "Unknown" : source.DeliveryContext;
                }
                return;
            }

            ValueViewerRow row = new ValueViewerRow
            {
                No = NucValues.Count + 1,
                IOA = source.IOA,
                Name = OfficialPointProfiles.GetDisplayNameOrDefault(source.IOA, source.Name),
                Type = source.Type,
                Value = source.Value,
                Quality = source.Quality,
                Timestamp = incomingTimestamp,
                ReceiveTimestampUtc = source.ReceiveTimestampUtc,
                ReceiveTimestampText = source.ReceiveTimestampText,
                EventTimestampUtc = source.EventTimestampUtc,
                SnapshotTimestampUtc = source.SnapshotTimestampUtc,
                HasProtocolTimestamp = source.HasProtocolTimestamp,
                SourceType = source.SourceType,
                UpdateSource = channel,
                Acd = source.Acd,
                Cot = source.Cot,
                TrafficClass = string.IsNullOrWhiteSpace(source.TrafficClass) ? "-" : source.TrafficClass,
                DeliveryContext = string.IsNullOrWhiteSpace(source.DeliveryContext) ? "Unknown" : source.DeliveryContext
            };

            int orderedInsertIndex = GetNucValueInsertIndex(row);
            NucValues.Insert(orderedInsertIndex, row);
            RenumberNucValues(Math.Max(0, orderedInsertIndex));

            _nucValueIndex[source.IOA] = row;

            while (NucValues.Count > MaxNucValueRows)
            {
                ValueViewerRow trimmed = NucValues[NucValues.Count - 1];
                _nucValueIndex.Remove(trimmed.IOA);
                NucValues.RemoveAt(NucValues.Count - 1);
            }
        }

        private static string GetEffectiveValueTimestampText(ValueViewerRow source)
        {
            if (source == null)
            {
                return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            }

            if (source.HasProtocolTimestamp && source.EventTimestampUtc.HasValue)
            {
                return source.EventTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            }

            if (source.SnapshotTimestampUtc.HasValue)
            {
                return source.SnapshotTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(source.ReceiveTimestampText) && source.ReceiveTimestampText != "-")
            {
                return source.ReceiveTimestampText;
            }

            if (!string.IsNullOrWhiteSpace(source.Timestamp) && source.Timestamp != "-")
            {
                return source.Timestamp;
            }

            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private void ReorderNucValuesNewestFirst()
        {
            if (NucValues.Count <= 1)
            {
                RenumberNucValues();
                return;
            }

            List<ValueViewerRow> ordered = NucValues
                .OrderBy(row => row.IOA)
                .ToList();

            for (int index = 0; index < ordered.Count; index++)
            {
                ValueViewerRow row = ordered[index];
                int oldIndex = NucValues.IndexOf(row);
                if (oldIndex >= 0 && oldIndex != index)
                {
                    NucValues.Move(oldIndex, index);
                }

                row.No = index + 1;
            }

        }

        private int GetNucValueInsertIndex(ValueViewerRow candidate)
        {
            for (int index = 0; index < NucValues.Count; index++)
            {
                ValueViewerRow current = NucValues[index];
                if (candidate.IOA < current.IOA)
                {
                    return index;
                }
            }

            return NucValues.Count;
        }

        private void RenumberNucValues(int startIndex = 0)
        {
            int safeStart = Math.Max(0, startIndex);
            for (int index = safeStart; index < NucValues.Count; index++)
            {
                ValueViewerRow row = NucValues[index];
                if (row != null)
                {
                    row.No = index + 1;
                }
            }
        }

        private static DateTime GetNucValueSortTimestampUtc(ValueViewerRow row)
        {
            if (row == null)
            {
                return DateTime.MinValue;
            }

            if (row.EventTimestampUtc.HasValue)
            {
                return row.EventTimestampUtc.Value;
            }

            if (row.SnapshotTimestampUtc.HasValue)
            {
                return row.SnapshotTimestampUtc.Value;
            }

            if (TryParseEventTimestampUtc(row.Timestamp, out DateTime parsedUtc))
            {
                return parsedUtc;
            }

            return DateTime.MinValue;
        }

        private void AddStatusHistory(StatusHistoryRow row)
        {
            if (row == null)
            {
                return;
            }

            BoundedUiBuffer.InsertNewest(StatusHistory, row, MaxStatusHistoryRows);
        }

        private bool ShouldSuppressEmptyClass1BurstSummaryDuringGi(DateTime nowUtc)
        {
            if (_giInProgress)
            {
                return true;
            }

            if (_giLastCompletedUtc == DateTime.MinValue)
            {
                return false;
            }

            return (nowUtc - _giLastCompletedUtc) <= EmptyClass1BurstGiSuppressWindow;
        }

        private void ObserveBufferReplayConnectionState(string newStatus)
        {
            if (string.Equals(newStatus, "Disconnected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(newStatus, "Faulted", StringComparison.OrdinalIgnoreCase))
            {
                _bufferReplayDisconnectedAtUtc = DateTime.UtcNow;
                _bufferReplayReconnectedAtUtc = null;
                _lastBufferReplayEventTimestampUtc = null;
                _activeBufferReplaySignatures.Clear();
                _activeBufferReplaySession = new BufferReplaySession
                {
                    SessionId = Guid.NewGuid().ToString("N"),
                    DisconnectedAtText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    FinalVerdict = "Waiting reconnect"
                };
                BufferReplayStatusText = "Offline - buffering window opened";
                BufferReplaySummaryText = "Waiting reconnect to evaluate buffered replay.";
                return;
            }

            if (!string.Equals(newStatus, "Connected", StringComparison.OrdinalIgnoreCase) || _activeBufferReplaySession == null)
            {
                return;
            }

            _bufferReplayReconnectedAtUtc = DateTime.UtcNow;
            _activeBufferReplaySession.ReconnectedAtText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _activeBufferReplaySession.FinalVerdict = "Observing replay";
            BufferReplayStatusText = "Connected - observing replay";
            BufferReplaySummaryText = "Replay observation started.";

            unchecked
            {
                _bufferReplayFinalizeToken++;
            }

            int finalizeToken = _bufferReplayFinalizeToken;
            Task.Delay(BufferReplayObserveWindow).ContinueWith(_ => RunOnUi(() => FinalizeBufferReplaySession(finalizeToken)));
        }

        private void ObserveBufferReplayEvent(int ioa, string type, string previous, string newValue, string timestamp, string cot)
        {
            if (_activeBufferReplaySession == null || !_bufferReplayReconnectedAtUtc.HasValue)
            {
                return;
            }

            if (!string.Equals(cot, "Spont", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DateTime eventTimestampUtc;
            if (!TryParseEventTimestampUtc(timestamp, out eventTimestampUtc))
            {
                return;
            }

            if (eventTimestampUtc >= _bufferReplayReconnectedAtUtc.Value)
            {
                return;
            }

            _activeBufferReplaySession.BufferedEventCount++;
            _activeBufferReplaySession.ReplayEventCount++;

            string signature = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}",
                ioa,
                previous ?? string.Empty,
                newValue ?? string.Empty,
                eventTimestampUtc.ToString("O", CultureInfo.InvariantCulture));

            if (!_activeBufferReplaySignatures.Add(signature))
            {
                _activeBufferReplaySession.DuplicateEventCount++;
            }

            if (_lastBufferReplayEventTimestampUtc.HasValue && eventTimestampUtc < _lastBufferReplayEventTimestampUtc.Value)
            {
                _activeBufferReplaySession.FifoViolationCount++;
            }

            _lastBufferReplayEventTimestampUtc = eventTimestampUtc;
            _activeBufferReplaySession.SampleCheckCount = (int)Math.Ceiling(_activeBufferReplaySession.ReplayEventCount * 0.07d);
            _activeBufferReplaySession.MeetsMinimum600Events = _activeBufferReplaySession.ReplayEventCount >= 600;
            BufferReplaySummaryText = string.Format(
                CultureInfo.InvariantCulture,
                "Replay observed: {0} event(s), duplicate {1}, FIFO violations {2}.",
                _activeBufferReplaySession.ReplayEventCount,
                _activeBufferReplaySession.DuplicateEventCount,
                _activeBufferReplaySession.FifoViolationCount);
        }

        private void FinalizeBufferReplaySession(int finalizeToken)
        {
            if (finalizeToken != _bufferReplayFinalizeToken || _activeBufferReplaySession == null)
            {
                return;
            }

            if (_activeBufferReplaySession.ReplayEventCount <= 0)
            {
                _activeBufferReplaySession.FinalVerdict = "No replay observed";
            }
            else if (_activeBufferReplaySession.FifoViolationCount > 0 || _activeBufferReplaySession.DuplicateEventCount > 0)
            {
                _activeBufferReplaySession.FinalVerdict = "Replay anomalies observed";
            }
            else if (_activeBufferReplaySession.MeetsMinimum600Events)
            {
                _activeBufferReplaySession.FinalVerdict = "PASS - minimum 600 replay events met";
            }
            else
            {
                _activeBufferReplaySession.FinalVerdict = "Replay observed - below 600 validated events";
            }

            if (_activeBufferReplaySession.FifoViolationCount > 0)
            {
                AddFindingOnce("SOE:FIFO", new FindingRow
                {
                    Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Severity = "Critical",
                    Category = "SOE",
                    RuleCode = "SOE_FIFO_VIOLATION",
                    Title = "SOE FIFO violation observed",
                    Detail = string.Format(
                        CultureInfo.InvariantCulture,
                        "Buffered replay session observed {0} FIFO violation(s).",
                        _activeBufferReplaySession.FifoViolationCount),
                    IOA = "-",
                    Type = "SOE",
                    ExpectedClass = "FIFO preserved",
                    ActualClass = "FIFO violated"
                });
            }

            if (_activeBufferReplaySession.DuplicateEventCount > 0)
            {
                AddFindingOnce("SOE:DUP", new FindingRow
                {
                    Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Severity = "Major",
                    Category = "Buffer",
                    RuleCode = "SOE_REPLAY_DUPLICATE_EVENTS",
                    Title = "SOE duplicate replay observed",
                    Detail = string.Format(
                        CultureInfo.InvariantCulture,
                        "Buffered replay session observed {0} duplicate event(s).",
                        _activeBufferReplaySession.DuplicateEventCount),
                    IOA = "-",
                    Type = "SOE",
                    ExpectedClass = "No duplicates",
                    ActualClass = "Duplicate replay"
                });
            }

            if (_activeBufferReplaySession.ReplayEventCount > 0 && !_activeBufferReplaySession.MeetsMinimum600Events)
            {
                AddFindingOnce("SOE:MIN600", new FindingRow
                {
                    Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Severity = "Major",
                    Category = "SOE",
                    RuleCode = "SOE_MIN_CAPACITY_NOT_MET",
                    Title = "SOE minimum capacity not met",
                    Detail = string.Format(
                        CultureInfo.InvariantCulture,
                        "Buffered replay session validated {0} event(s), below the formal minimum of 600 events.",
                        _activeBufferReplaySession.ReplayEventCount),
                    IOA = "-",
                    Type = "SOE",
                    ExpectedClass = ">= 600 events",
                    ActualClass = _activeBufferReplaySession.ReplayEventCount.ToString(CultureInfo.InvariantCulture)
                });
            }

            BoundedUiBuffer.InsertNewest(BufferReplaySessions, _activeBufferReplaySession, MaxBufferReplaySessions);

            BufferReplayStatusText = "Replay session finalized";
            BufferReplaySummaryText = _activeBufferReplaySession.FinalVerdict;
            _activeBufferReplaySession = null;
            _bufferReplayDisconnectedAtUtc = null;
            _bufferReplayReconnectedAtUtc = null;
            _lastBufferReplayEventTimestampUtc = null;
            _activeBufferReplaySignatures.Clear();
        }

        private void ResetBufferReplayState()
        {
            _activeBufferReplaySession = null;
            _bufferReplayDisconnectedAtUtc = null;
            _bufferReplayReconnectedAtUtc = null;
            _lastBufferReplayEventTimestampUtc = null;
            _activeBufferReplaySignatures.Clear();
            BufferReplayStatusText = "Idle";
            BufferReplaySummaryText = "No buffer replay session yet.";
        }

        private void ResetRedundancyState()
        {
            _mainLinkFaultActive = null;
            _backupLinkFaultActive = null;
            _iedFaultActive = null;
            _nucMainConnected = false;
            _nucBackupConnected = false;
            _nucMainFlowHealthy = false;
            _nucBackupFlowHealthy = false;
            _nucMainFaultLatched = false;
            _nucBackupFaultLatched = false;
            _nucMainAcdState = null;
            _nucBackupAcdState = null;
            _nucMainLinkState = NucLinkHealthState.Fault;
            _nucBackupLinkState = NucLinkHealthState.Fault;
            _nucMainRole = NucChannelRole.Active;
            _nucBackupRole = NucChannelRole.Standby;
            _nucMainControllerState = NucChannelState.Disconnected;
            _nucBackupControllerState = NucChannelState.Disconnected;
            _nucMainRxCount = 0;
            _nucMainTxCount = 0;
            _nucBackupRxCount = 0;
            _nucBackupTxCount = 0;
            _nucMainConnectedAtUtc = null;
            _nucBackupConnectedAtUtc = null;
            _nucMainLastActivityUtc = null;
            _nucBackupLastActivityUtc = null;
            _nucMainLastTxUtc = null;
            _nucMainLastRxUtc = null;
            _nucBackupLastTxUtc = null;
            _nucBackupLastRxUtc = null;
            _nucMainLastResponseUtc = null;
            _nucBackupLastResponseUtc = null;
            _nucMainLastTimeoutUtc = null;
            _nucBackupLastTimeoutUtc = null;
            _nucMainLastFlowJournalUtc = null;
            _nucBackupLastFlowJournalUtc = null;
            _redundancyActiveLink = null;
            _redundancySwitchoverCount = 0;
            _lastRedundancySwitchUtc = null;
            _lastRedundancyDisconnectUtc = null;
            _lastRedundancyReconnectUtc = null;
            _giObservedAfterRedundancySwitch = false;
            RedundancySelectedMode = RedundancyModeOptions.FirstOrDefault() ?? "Hot-Standby";
            RedundancySelectedGiPolicy = RedundancyGiPolicyOptions.ElementAtOrDefault(1) ?? "Optional";
            RedundancyActiveLinkText = "Active link: Unknown";
            RedundancyMainLinkText = "L1FT: Unknown";
            RedundancyBackupLinkText = "L2FT: Unknown";
            RedundancyIedFaultText = "IEDF: Unknown";
            RedundancySwitchSummaryText = "Switchover count: 0";
            RedundancyGiObservationText = "GI after switchover: Not observed";
            RedundancyContinuityText = "Continuity gap: -";
            LastRedundancySwitchText = "Last switchover: -";
            RedundancyConfigSummaryText = "Link 1: - | Link 2: - | Mode: - | GI Policy: -";
            RedundancyValidationText = "Configuration not evaluated yet.";
            RedundancyControllerStatusText = "Controller: Inactive";
            RedundancyControllerDetailText = "NUC backend foundation not started.";
            UpdateNucRedundancyVisuals();
        }

        private void RefreshRedundancySerialPorts()
        {
            string[] ports = System.IO.Ports.SerialPort.GetPortNames()
                .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            RedundancySerialPortOptions.Clear();
            foreach (string port in ports)
            {
                RedundancySerialPortOptions.Add(port);
            }

            string currentPort = CurrentSettings != null ? CurrentSettings.SerialPort : null;
            if (!string.IsNullOrWhiteSpace(currentPort)
                && !RedundancySerialPortOptions.Any(port => string.Equals(port, currentPort, StringComparison.OrdinalIgnoreCase)))
            {
                RedundancySerialPortOptions.Add(currentPort);
            }

            if (string.IsNullOrWhiteSpace(RedundancyPrimaryPort))
            {
                RedundancyPrimaryPort = currentPort ?? RedundancySerialPortOptions.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(RedundancyBackupPort))
            {
                RedundancyBackupPort = RedundancySerialPortOptions
                    .FirstOrDefault(port => !string.Equals(port, RedundancyPrimaryPort, StringComparison.OrdinalIgnoreCase))
                    ?? currentPort
                    ?? RedundancySerialPortOptions.FirstOrDefault();
            }

            RefreshRedundancyConfigurationSummary();
        }

        private void RefreshRedundancyConfigurationSummary()
        {
            RedundancyConfigSummaryText = string.Format(
                CultureInfo.InvariantCulture,
                "Link 1: {0} | Link 2: {1} | Mode: {2} | GI Policy: {3}",
                string.IsNullOrWhiteSpace(RedundancyPrimaryPort) ? "-" : RedundancyPrimaryPort,
                string.IsNullOrWhiteSpace(RedundancyBackupPort) ? "-" : RedundancyBackupPort,
                string.IsNullOrWhiteSpace(RedundancySelectedMode) ? "-" : RedundancySelectedMode,
                string.IsNullOrWhiteSpace(RedundancySelectedGiPolicy) ? "-" : RedundancySelectedGiPolicy);

            string validationMessage;
            NucRedundancySettings settings;
            if (TryBuildNucRedundancySettings(out settings, out validationMessage))
            {
                RedundancyValidationText = "Configuration ready for dual-link engine handoff.";
            }
            else
            {
                RedundancyValidationText = validationMessage;
            }
        }

        private void ApplyLoadedNucRedundancySettings(NucRedundancySettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.BaseConnectionSettings != null)
            {
                CurrentSettings = settings.BaseConnectionSettings.Clone();
                _masterService.ApplySettings(CurrentSettings);
                RefreshCommands();
            }

            if (!string.IsNullOrWhiteSpace(settings.PrimarySerialPort))
            {
                RedundancyPrimaryPort = settings.PrimarySerialPort;
            }

            if (!string.IsNullOrWhiteSpace(settings.BackupSerialPort))
            {
                RedundancyBackupPort = settings.BackupSerialPort;
            }

            RedundancySelectedMode = NormalizeNucRedundancyMode(settings.RedundancyMode);

            if (!string.IsNullOrWhiteSpace(settings.GiPolicy))
            {
                RedundancySelectedGiPolicy = settings.GiPolicy;
            }
        }

        public NucRedundancySettings BuildCurrentNucRedundancySettings()
        {
            return new NucRedundancySettings
            {
                BaseConnectionSettings = CurrentSettings == null ? ConnectionSettings.CreateDefault() : CurrentSettings.Clone(),
                PrimarySerialPort = RedundancyPrimaryPort,
                BackupSerialPort = RedundancyBackupPort,
                RedundancyMode = NormalizeNucRedundancyMode(RedundancySelectedMode),
                GiPolicy = RedundancySelectedGiPolicy
            };
        }

        private static string NormalizeNucRedundancyMode(string mode)
        {
            return string.Equals(mode, "Concurrent/Parallel", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(mode)
                ? "Hot-Standby"
                : mode;
        }

        public async Task SaveNucRedundancySettingsAsync(NucRedundancySettings settings)
        {
            if (settings == null)
            {
                return;
            }

            ApplyLoadedNucRedundancySettings(settings);
            await _nucRedundancySettingsStore.SaveAsync(settings);
            if (settings.BaseConnectionSettings != null)
            {
                await _settingsStore.SaveAsync(CurrentSettings);
                AddSystemLine("CFG", "NUC link profile applied", CurrentProfileSummary);
            }
            RefreshRedundancyConfigurationSummary();
        }

        private void NucRedundancyService_SessionStateChanged(object sender, NucRedundancySessionState e)
        {
            RunOnUi(() =>
            {
                if (e == null)
                {
                    return;
                }

                  _nucSessionActive = e.IsActive;
                  OnPropertyChanged(nameof(IsNucRedundancySessionActive));
                  OnPropertyChanged(nameof(CanStartNucRedundancySessionButton));
                  OnPropertyChanged(nameof(CanStopNucRedundancySessionButton));
                  RedundancyControllerStatusText = "Controller: " + (e.StatusText ?? "Unknown");
                  RedundancyControllerDetailText = e.DetailText ?? string.Empty;
                  if (e.IsActive && !string.IsNullOrWhiteSpace(e.ActiveChannel))
                  {
                      _redundancyActiveLink = e.ActiveChannel;
                      RedundancyActiveLinkText = "Active link: " + e.ActiveChannel;
                  }
                  else if (!e.IsActive)
                  {
                      _redundancyActiveLink = null;
                      RedundancyActiveLinkText = "Active link: None";
                      ClearNucRecentTrafficBadges();
                  }
                  _nucMainRole = ParseNucRole(e.PrimaryRole, NucChannelRole.Active);
                  _nucBackupRole = ParseNucRole(e.BackupRole, NucChannelRole.Standby);
                  _nucMainControllerState = ParseNucChannelState(e.PrimaryChannelState, NucChannelState.Disconnected);
                  _nucBackupControllerState = ParseNucChannelState(e.BackupChannelState, NucChannelState.Disconnected);
                  _nucMainLinkState = MapControllerChannelState(_nucMainControllerState, _nucMainRole);
                  _nucBackupLinkState = MapControllerChannelState(_nucBackupControllerState, _nucBackupRole);
                  _nucMainRxCount = e.PrimaryRxCount;
                  _nucMainTxCount = e.PrimaryTxCount;
                  _nucBackupRxCount = e.BackupRxCount;
                  _nucBackupTxCount = e.BackupTxCount;
                  _nucMainLastActivityUtc = ParseUtcTimestamp(e.PrimaryLastActivityUtcText);
                  _nucBackupLastActivityUtc = ParseUtcTimestamp(e.BackupLastActivityUtcText);
                  _nucMainLastResponseUtc = ParseUtcTimestamp(e.PrimaryLastResponseUtcText);
                  _nucBackupLastResponseUtc = ParseUtcTimestamp(e.BackupLastResponseUtcText);
                  _nucMainLastTxUtc = _nucMainLastActivityUtc;
                  _nucMainLastRxUtc = _nucMainLastResponseUtc;
                  _nucBackupLastTxUtc = _nucBackupLastActivityUtc;
                  _nucBackupLastRxUtc = _nucBackupLastResponseUtc;

                if (e.Settings != null)
                {
                    RedundancyConfigSummaryText = string.Format(
                        CultureInfo.InvariantCulture,
                        "Link 1: {0} | Link 2: {1} | Mode: {2} | GI Policy: {3}",
                        e.Settings.PrimarySerialPort ?? "-",
                        e.Settings.BackupSerialPort ?? "-",
                        e.Settings.RedundancyMode ?? "-",
                        e.Settings.GiPolicy ?? "-");
                }

                if (!string.IsNullOrWhiteSpace(e.PrimaryStatusText) || !string.IsNullOrWhiteSpace(e.BackupStatusText))
                {
                    RedundancyControllerDetailText = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} | Active={1} | Main={2} | Backup={3} | Main sup tick/tx/rx={4}/{5}/{6} | Backup sup tick/tx/rx={7}/{8}/{9}",
                        e.DetailText ?? string.Empty,
                        string.IsNullOrWhiteSpace(e.ActiveChannel) ? "-" : e.ActiveChannel,
                        string.IsNullOrWhiteSpace(e.PrimaryStatusText) ? "-" : e.PrimaryStatusText,
                        string.IsNullOrWhiteSpace(e.BackupStatusText) ? "-" : e.BackupStatusText,
                        e.PrimarySupervisionTickCount,
                        e.PrimarySupervisionTxObservedCount,
                        e.PrimarySupervisionResponseObservedCount,
                        e.BackupSupervisionTickCount,
                        e.BackupSupervisionTxObservedCount,
                        e.BackupSupervisionResponseObservedCount);
                }

                AddRedundancyTimeline(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    "Controller",
                    e.IsActive ? "Session started" : "Session stopped",
                    e.StatusText ?? "-",
                    e.DetailText ?? string.Empty,
                    "-");
                AddRedundancyJournal(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Controller", e.StatusText ?? "Controller", e.DetailText ?? string.Empty);
                UpdateNucRedundancyVisuals();
            });
        }

        private void NucRedundancyService_ConnectionStateChanged(object sender, NucRedundancyConnectionEventArgs e)
        {
            RunOnUi(() =>
            {
                if (e == null || e.Status == null)
                {
                    return;
                }

                string channelName = string.IsNullOrWhiteSpace(e.ChannelName) ? "Link" : e.ChannelName;
                string newStatus = e.Status.DisplayText;

                if (string.Equals(channelName, "Main", StringComparison.OrdinalIgnoreCase))
                {
                    _nucMainConnected = string.Equals(newStatus, ConnectionStatusInfo.Connected.DisplayText, StringComparison.OrdinalIgnoreCase);
                    _nucMainConnectedAtUtc = _nucMainConnected ? (DateTime?)DateTime.UtcNow : null;
                    if (!_nucMainConnected)
                    {
                        _nucMainFaultLatched = true;
                        _nucMainLinkState = NucLinkHealthState.Fault;
                        _nucMainLastResponseUtc = null;
                    }
                }
                else if (string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase))
                {
                    _nucBackupConnected = string.Equals(newStatus, ConnectionStatusInfo.Connected.DisplayText, StringComparison.OrdinalIgnoreCase);
                    _nucBackupConnectedAtUtc = _nucBackupConnected ? (DateTime?)DateTime.UtcNow : null;
                    if (!_nucBackupConnected)
                    {
                        _nucBackupFaultLatched = true;
                        _nucBackupLinkState = NucLinkHealthState.Fault;
                        _nucBackupLastResponseUtc = null;
                    }
                }

                if (string.Equals(newStatus, ConnectionStatusInfo.Disconnected.DisplayText, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(newStatus, ConnectionStatusInfo.Faulted.DisplayText, StringComparison.OrdinalIgnoreCase))
                {
                    _lastRedundancyDisconnectUtc = DateTime.UtcNow;
                }
                else if (string.Equals(newStatus, ConnectionStatusInfo.Connected.DisplayText, StringComparison.OrdinalIgnoreCase))
                {
                    _lastRedundancyReconnectUtc = DateTime.UtcNow;
                    if (_lastRedundancyDisconnectUtc.HasValue)
                    {
                        double gapMs = (_lastRedundancyReconnectUtc.Value - _lastRedundancyDisconnectUtc.Value).TotalMilliseconds;
                        RedundancyContinuityText = "Continuity gap: " + gapMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
                    }
                }

                if (_nucMainConnected && !_nucBackupConnected)
                {
                    RedundancyActiveLinkText = "Active link: Main";
                }
                else if (_nucBackupConnected && !_nucMainConnected)
                {
                    RedundancyActiveLinkText = "Active link: Backup";
                }
                else if (_nucMainConnected && _nucBackupConnected)
                {
                    RedundancyActiveLinkText = "Active link: Main + Backup";
                }
                else
                {
                    RedundancyActiveLinkText = "Active link: Unknown";
                }

                AddRedundancyTimeline(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    channelName,
                    "Communication",
                    newStatus,
                    e.Status.Detail ?? string.Empty,
                    _giObservedAfterRedundancySwitch ? "Observed" : "-");
                AddRedundancyJournal(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), channelName, newStatus, e.Status.Detail ?? string.Empty);
                ObserveNucAvailabilityConnectionState(channelName, newStatus, e.Status.Detail ?? string.Empty);
                UpdateNucRedundancyVisuals();
            });
        }

        private void NucRedundancyService_LineMonitorRecordReceived(object sender, NucRedundancyLineMonitorEventArgs e)
        {
            TryCaptureNucCommandFastPath(e);

            RunOnUi(() =>
            {
                if (e == null || e.Record == null)
                {
                    return;
                }

                LineMonitorRow row = e.Record;
                string channelName = string.IsNullOrWhiteSpace(e.ChannelName) ? "Link" : e.ChannelName;
                string eventTimeText = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time;
                bool suppressUiNoise = ShouldCoalesceNucStandbyLineMonitorRow(row, channelName);
                if (!suppressUiNoise)
                {
                    AddNucLineMonitorRow(row, channelName);
                }
                AddNucTraceRow(row, channelName);

                if (string.Equals(row.ACD, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.ACD, "0", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(channelName, "Main", StringComparison.OrdinalIgnoreCase))
                    {
                        _nucMainAcdState = row.ACD == "1" ? "ON" : "OFF";
                    }
                    else if (string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase))
                    {
                        _nucBackupAcdState = row.ACD == "1" ? "ON" : "OFF";
                    }
                }

                if (string.Equals(row.Direction, "STATE", StringComparison.OrdinalIgnoreCase))
                {
                    if (row.Summary.IndexOf("GI command sent", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ObserveRedundancyGiEvent("GI sent (" + channelName + ")", DateTime.Today.ToString("yyyy-MM-dd ") + row.Time);
                    }
                    else if (row.Summary.IndexOf("GI completed", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ObserveRedundancyGiEvent("GI completed (" + channelName + ")", DateTime.Today.ToString("yyyy-MM-dd ") + row.Time);
                    }

                    if (!suppressUiNoise)
                    {
                        AddRedundancyOperationalJournal(DateTime.Today.ToString("yyyy-MM-dd ") + row.Time, channelName, row.Summary ?? "State", row.Detail ?? string.Empty);
                    }
                }

                if (string.Equals(row.Direction, "RX", StringComparison.OrdinalIgnoreCase)
                    && HasDecodedAsdu(row)
                    && (row.AsduType.IndexOf("C_SC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                        || row.AsduType.IndexOf("C_DC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                        || row.AsduType.IndexOf("C_RC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    bool isNegative = IsNegativeConfirmation(row);
                    string ioa = string.IsNullOrWhiteSpace(row.IOA) ? ExtractIoaFromDetail(row.Detail) : row.IOA;
                    string commandType = GetCommandFamilyLabel(row.AsduType);
                    string operation = NormalizeCommandOperation(commandType, GetCommandOperationLabel(row));
                    string rxMode = TryGetRxModeLabel(row);
                    CommandTransaction transaction = ResolvePendingCommand(ioa, commandType, operation, rxMode, row, isNegative);
                    if (transaction == null)
                    {
                        transaction = TryConsumeNucFastCommand(ioa, commandType, operation, rxMode);
                    }

                    if (transaction != null)
                    {
                        ioa = transaction.CommandIoa;
                        operation = transaction.Operation;
                    }

                    string commandMode = transaction != null ? transaction.Mode : (rxMode ?? "DO");
                    string rxStage = string.Equals(commandMode, "SBO Select", StringComparison.OrdinalIgnoreCase)
                        ? "SelectRx"
                        : string.Equals(commandMode, "SBO Execute", StringComparison.OrdinalIgnoreCase)
                            ? "ExecuteRx"
                            : "DoRx";

                    TrackCommandLifecycle(ioa, rxStage, operation, isNegative);

                    AddNucEventLog(new EventLogRow
                    {
                        Time = eventTimeText,
                        Source = channelName,
                        Name = "Command",
                        IOA = ioa,
                        Type = commandType,
                        Event = isNegative ? (commandMode + " rejected") : (commandMode + " confirmed"),
                        Value = operation
                    });

                    AppendCommandNucSoeForensicRow(
                        eventTimeText,
                        channelName,
                        ioa,
                        commandType,
                        row.AsduType,
                        row.CASDU,
                        row.COT,
                        operation,
                        isNegative ? (commandMode + " rejected") : (commandMode + " confirmed"));

                    if (transaction != null && !transaction.ResponsePublishedAtUtc.HasValue)
                    {
                        AddCommandLifeMonitorRow(transaction, isNegative ? "REJ" : "OK");
                    }
                }

                bool isLinkCheckState = (!string.IsNullOrWhiteSpace(row.Summary) && row.Summary.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(row.Detail) && row.Detail.IndexOf("link-layer test function", StringComparison.OrdinalIgnoreCase) >= 0);

                UpdateNucLinkTrafficEvidence(channelName, row);
                ObserveNucAvailabilityLine(channelName, row);
                UpdateNucRedundancyVisuals();
            });
        }

        private void NucRedundancyService_ValueReceived(object sender, NucRedundancyValueEventArgs e)
        {
            RunOnUi(() =>
            {
                if (e == null || e.Value == null)
                {
                    return;
                }

                ValueViewerRow value = e.Value;
                string channelName = string.IsNullOrWhiteSpace(e.ChannelName) ? "Link" : e.ChannelName;
                string oldValue = null;
                if (_nucValueIndex.TryGetValue(value.IOA, out ValueViewerRow existingNucValue))
                {
                    oldValue = existingNucValue.Value;
                }

                PointDefinition pointDefinition;
                OfficialPointProfiles.TryGetPointByIoa(value.IOA, out pointDefinition);
                string pointKey = pointDefinition == null ? string.Empty : pointDefinition.PointKey ?? string.Empty;
                bool isMainGatewayFaultPoint = string.Equals(pointKey, "GatewayMainLinkFault", StringComparison.OrdinalIgnoreCase);
                bool isBackupGatewayFaultPoint = string.Equals(pointKey, "GatewayBackupLinkFault", StringComparison.OrdinalIgnoreCase);
                bool isIedGatewayFaultPoint = string.Equals(pointKey, "GatewayIedFaulty", StringComparison.OrdinalIgnoreCase);
                bool isGatewayFaultPoint = isMainGatewayFaultPoint || isBackupGatewayFaultPoint || isIedGatewayFaultPoint;

                bool shouldLogScadaEvent = !isGatewayFaultPoint && ShouldLogNucScadaValueEvent(value);
                UpsertNucValue(value, channelName);
                if (shouldLogScadaEvent)
                {
                    ValueViewerRow currentNucValue = value;
                    if (_nucValueIndex.TryGetValue(value.IOA, out ValueViewerRow normalizedNucValue) && normalizedNucValue != null)
                    {
                        currentNucValue = normalizedNucValue;
                    }

                    EventLogRow row = new EventLogRow
                    {
                        Time = string.IsNullOrWhiteSpace(currentNucValue.Timestamp) ? "-" : currentNucValue.Timestamp,
                        RecvTime = string.IsNullOrWhiteSpace(value.ReceiveTimestampText) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) : value.ReceiveTimestampText,
                        SourceTime = string.IsNullOrWhiteSpace(currentNucValue.Timestamp) ? "-" : currentNucValue.Timestamp,
                        DeltaMs = FormatSoeDeltaMs(value.ReceiveTimestampUtc, value.EventTimestampUtc),
                        Source = channelName,
                        Name = currentNucValue.Name,
                        IOA = currentNucValue.IOA.ToString(CultureInfo.InvariantCulture),
                        Type = currentNucValue.Type,
                        TypeId = currentNucValue.TypeId,
                        Casdu = currentNucValue.Casdu,
                        Event = string.IsNullOrWhiteSpace(currentNucValue.Cot) ? "Value update" : currentNucValue.Cot,
                        Value = currentNucValue.Value,
                        Quality = currentNucValue.Quality,
                        Cot = string.IsNullOrWhiteSpace(currentNucValue.Cot) ? "-" : currentNucValue.Cot,
                        DataClass = string.IsNullOrWhiteSpace(currentNucValue.TrafficClass) ? "-" : currentNucValue.TrafficClass
                    };
                    AddNucEventLog(row);
                }
                AppendDecodedNucSoeForensicRow(value, channelName);
                ObserveRedundancyDiscreteEvent(
                    value.IOA,
                    value.Name,
                    value.Type,
                    oldValue,
                    value.Value,
                    value.Cot,
                    value.Timestamp);
                RegisterNucLinkActivity(channelName, false, false);
                ObserveNucRecentTraffic(
                    channelName,
                    value.TrafficClass,
                    value.Cot,
                    value.UpdateSource,
                    value.Cot);
                _nucAvailabilityObservedEventCount++;
                UpdateNucRedundancyVisuals();
            });
        }

        private void TryCaptureNucCommandFastPath(NucRedundancyLineMonitorEventArgs e)
        {
            if (e == null || e.Record == null)
            {
                return;
            }

            LineMonitorRow row = e.Record;
            if (!string.Equals(row.Direction, "RX", StringComparison.OrdinalIgnoreCase)
                || !HasDecodedAsdu(row))
            {
                return;
            }

            if (!(row.AsduType.IndexOf("C_SC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                || row.AsduType.IndexOf("C_DC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                || row.AsduType.IndexOf("C_RC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return;
            }

            bool isNegative = IsNegativeConfirmation(row);
            string ioa = string.IsNullOrWhiteSpace(row.IOA) ? ExtractIoaFromDetail(row.Detail) : row.IOA;
            string commandType = GetCommandFamilyLabel(row.AsduType);
            string operation = NormalizeCommandOperation(commandType, GetCommandOperationLabel(row));
            string rxMode = TryGetRxModeLabel(row);
            CommandTransaction transaction = ResolvePendingCommand(ioa, commandType, operation, rxMode, row, isNegative);
            if (transaction == null)
            {
                return;
            }

            string signature = BuildNucCommandSignature(transaction.CommandIoa, transaction.CommandType, transaction.Operation, transaction.Mode);
            _nucFastCommandCache[signature] = transaction;

            if (!transaction.ResponsePublishedAtUtc.HasValue)
            {
                QueueNucCommandLifeMonitorRow(transaction, isNegative ? "REJ" : "OK");
            }
        }

        private void QueueNucCommandLifeMonitorRow(CommandTransaction transaction, string resultShort)
        {
            if (transaction == null)
            {
                return;
            }

            RunOnUi(() =>
            {
                if (transaction.ResponsePublishedAtUtc.HasValue)
                {
                    return;
                }

                AddCommandLifeMonitorRow(transaction, resultShort);
            });
        }

        private static string BuildNucCommandSignature(string ioa, string commandType, string operation, string mode)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}",
                ioa ?? "-",
                commandType ?? "Command",
                operation ?? string.Empty,
                mode ?? string.Empty);
        }

        private CommandTransaction TryConsumeNucFastCommand(string ioa, string commandType, string operation, string mode)
        {
            string signature = BuildNucCommandSignature(ioa, commandType, operation, mode);
            CommandTransaction transaction;
            return _nucFastCommandCache.TryRemove(signature, out transaction) ? transaction : null;
        }

        public bool TryBuildNucRedundancySettings(out NucRedundancySettings settings, out string validationMessage)
        {
            settings = null;
            validationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(RedundancyPrimaryPort))
            {
                validationMessage = "Link 1 / Main COM port is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(RedundancyBackupPort))
            {
                validationMessage = "Link 2 / Backup COM port is required.";
                return false;
            }

            if (string.Equals(RedundancyPrimaryPort, RedundancyBackupPort, StringComparison.OrdinalIgnoreCase))
            {
                validationMessage = "Link 1 and Link 2 must use different COM ports.";
                return false;
            }

            settings = new NucRedundancySettings
            {
                BaseConnectionSettings = CurrentSettings == null ? ConnectionSettings.CreateDefault() : CurrentSettings.Clone(),
                PrimarySerialPort = RedundancyPrimaryPort,
                BackupSerialPort = RedundancyBackupPort,
                RedundancyMode = NormalizeNucRedundancyMode(RedundancySelectedMode),
                GiPolicy = string.IsNullOrWhiteSpace(RedundancySelectedGiPolicy) ? "Optional" : RedundancySelectedGiPolicy
            };

            return true;
        }

        private void ObserveRedundancyConnectionState(string newStatus)
        {
            if (string.Equals(newStatus, "Disconnected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(newStatus, "Faulted", StringComparison.OrdinalIgnoreCase))
            {
                _lastRedundancyDisconnectUtc = DateTime.UtcNow;
                AddRedundancyTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Session", "Communication", newStatus, ConnectionDetail, "-");
                return;
            }

            if (!string.Equals(newStatus, "Connected", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastRedundancyReconnectUtc = DateTime.UtcNow;
            string continuityText = "Continuity gap: -";
            if (_lastRedundancyDisconnectUtc.HasValue)
            {
                double gapMs = (_lastRedundancyReconnectUtc.Value - _lastRedundancyDisconnectUtc.Value).TotalMilliseconds;
                continuityText = "Continuity gap: " + gapMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            }

            RedundancyContinuityText = continuityText;
            AddRedundancyTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Session", "Communication", "Connected", continuityText, "-");
        }

        private void ObserveRedundancyDiscreteEvent(int ioa, string name, string type, string previous, string newValue, string cot, string timestamp)
        {
            PointDefinition point;
            if (!OfficialPointProfiles.TryGetPointByIoa(ioa, out point) || point == null)
            {
                return;
            }

            string pointKey = point.PointKey ?? string.Empty;
            string timeText = NormalizeTimestamp(timestamp);
            string stateText = InterpretRedundancyState(newValue);
            bool changed = !string.Equals(previous ?? string.Empty, newValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            bool shouldLogGatewayEvent = changed
                && !(string.IsNullOrWhiteSpace(previous) && string.Equals(newValue, "OFF", StringComparison.OrdinalIgnoreCase));
            string detail = string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1}{2}",
                point.DisplayName,
                string.IsNullOrWhiteSpace(previous) ? string.Empty : previous + " -> ",
                newValue ?? "-");

            if (string.Equals(pointKey, "GatewayMainLinkFault", StringComparison.OrdinalIgnoreCase))
            {
                _mainLinkFaultActive = IsFaultState(newValue);
                RedundancyMainLinkText = "L1FT: " + stateText;
                OnPropertyChanged(nameof(IsRedundancyMainFaultActive));
                if (shouldLogGatewayEvent)
                {
                    EventLogRow row = new EventLogRow
                    {
                        Time = timeText,
                        RecvTime = timeText,
                        SourceTime = timeText,
                        DeltaMs = "0",
                        Source = "Main",
                        Name = point.DisplayName,
                        IOA = ioa.ToString(CultureInfo.InvariantCulture),
                        Type = type,
                        TypeId = type,
                        Casdu = "-",
                        Event = string.IsNullOrWhiteSpace(cot) ? "Spont" : cot,
                        Value = newValue,
                        Quality = "-",
                        Cot = string.IsNullOrWhiteSpace(cot) ? "-" : cot
                    };
                    AddNucEventLog(row);
                    AppendRedundancyNucSoeForensicRow(timeText, "Main", ioa, point.DisplayName, type, cot, newValue);
                }
                AddRedundancyTimeline(timeText, "Main", "Link fault point", stateText, detail, "-");
                return;
            }

            if (string.Equals(pointKey, "GatewayBackupLinkFault", StringComparison.OrdinalIgnoreCase))
            {
                _backupLinkFaultActive = IsFaultState(newValue);
                RedundancyBackupLinkText = "L2FT: " + stateText;
                OnPropertyChanged(nameof(IsRedundancyBackupFaultActive));
                if (shouldLogGatewayEvent)
                {
                    EventLogRow row = new EventLogRow
                    {
                        Time = timeText,
                        RecvTime = timeText,
                        SourceTime = timeText,
                        DeltaMs = "0",
                        Source = "Backup",
                        Name = point.DisplayName,
                        IOA = ioa.ToString(CultureInfo.InvariantCulture),
                        Type = type,
                        TypeId = type,
                        Casdu = "-",
                        Event = string.IsNullOrWhiteSpace(cot) ? "Spont" : cot,
                        Value = newValue,
                        Quality = "-",
                        Cot = string.IsNullOrWhiteSpace(cot) ? "-" : cot
                    };
                    AddNucEventLog(row);
                    AppendRedundancyNucSoeForensicRow(timeText, "Backup", ioa, point.DisplayName, type, cot, newValue);
                }
                AddRedundancyTimeline(timeText, "Backup", "Link fault point", stateText, detail, "-");
                return;
            }

            if (string.Equals(pointKey, "GatewayIedFaulty", StringComparison.OrdinalIgnoreCase))
            {
                _iedFaultActive = IsFaultState(newValue);
                RedundancyIedFaultText = "IEDF: " + stateText;
                OnPropertyChanged(nameof(IsRedundancyIedFaultActive));
                if (shouldLogGatewayEvent)
                {
                    EventLogRow row = new EventLogRow
                    {
                        Time = timeText,
                        RecvTime = timeText,
                        SourceTime = timeText,
                        DeltaMs = "0",
                        Source = "IED",
                        Name = point.DisplayName,
                        IOA = ioa.ToString(CultureInfo.InvariantCulture),
                        Type = type,
                        TypeId = type,
                        Casdu = "-",
                        Event = string.IsNullOrWhiteSpace(cot) ? "Spont" : cot,
                        Value = newValue,
                        Quality = "-",
                        Cot = string.IsNullOrWhiteSpace(cot) ? "-" : cot
                    };
                    AddNucEventLog(row);
                    AppendRedundancyNucSoeForensicRow(timeText, "IED", ioa, point.DisplayName, type, cot, newValue);
                }
                AddRedundancyTimeline(timeText, "IED", "IED fault point", stateText, detail, "-");
            }
        }

        private bool ShouldLogNucScadaValueEvent(ValueViewerRow value)
        {
            PointDefinition point;
            bool isGatewayFaultPoint = OfficialPointProfiles.TryGetPointByIoa(value.IOA, out point)
                && point != null
                && (string.Equals(point.PointKey, "GatewayMainLinkFault", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(point.PointKey, "GatewayBackupLinkFault", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(point.PointKey, "GatewayIedFaulty", StringComparison.OrdinalIgnoreCase));

            if (!isGatewayFaultPoint && !IsNucScadaEventCandidate(value))
            {
                return false;
            }

            string previousValue;
            bool hasPreviousValue = _nucLastDiscreteStates.TryGetValue(value.IOA, out previousValue);
            _nucLastDiscreteStates[value.IOA] = value.Value;

            if (!hasPreviousValue)
            {
                return !string.Equals(value.Value, "OFF", StringComparison.OrdinalIgnoreCase);
            }

            return !string.Equals(previousValue, value.Value, StringComparison.OrdinalIgnoreCase);
        }

        private void ObserveRedundancyGiEvent(string giEvent, string timestampText)
        {
            if (!_lastRedundancySwitchUtc.HasValue)
            {
                return;
            }

            DateTime giTimestampUtc;
            if (!TryParseEventTimestampUtc(timestampText, out giTimestampUtc))
            {
                giTimestampUtc = DateTime.UtcNow;
            }

            if (giTimestampUtc - _lastRedundancySwitchUtc.Value > RedundancyGiObserveWindow)
            {
                return;
            }

            _giObservedAfterRedundancySwitch = true;
            RedundancyGiObservationText = "GI after switchover: Observed";
            RedundancyFindingSummaryText = "Redundancy findings: switchover observed with GI.";
            OnPropertyChanged(nameof(IsGiObservedAfterRedundancySwitch));
            AddRedundancyTimeline(
                NormalizeTimestamp(timestampText),
                "Session",
                giEvent,
                "Observed",
                "GI activity recorded within redundancy switchover window.",
                "Observed");
            AddAvailabilityTimeline(
                NormalizeTimestamp(timestampText),
                "Redundancy",
                "GiObservedAfterSwitchover",
                "GI activity recorded after switchover.",
                giEvent);
            AppendNucSoeForensicRow(new SoeForensicRow
            {
                RecvTimeUtc = giTimestampUtc,
                SourceTimeUtc = null,
                DeltaMs = null,
                Channel = _redundancyActiveLink,
                CA = 0,
                IOA = 0,
                TypeId = 0,
                TypeIdText = "C_IC_NA_1",
                CotText = "GI",
                CotRaw = 20,
                SignalName = "Redundancy",
                ValueText = giEvent,
                QualityText = "-",
                Origin = "Redundancy"
            });
            UpdateNucRedundancyVisuals();
        }

        private void UpdateRedundancyActiveLinkInference(string timestampText, string reason)
        {
            string inferredActiveLink = "Unknown";

            if (_mainLinkFaultActive == true && _backupLinkFaultActive == false)
            {
                inferredActiveLink = "Backup";
            }
            else if (_backupLinkFaultActive == true && _mainLinkFaultActive == false)
            {
                inferredActiveLink = "Main";
            }
            else if (_mainLinkFaultActive == false && _backupLinkFaultActive == false)
            {
                inferredActiveLink = !string.IsNullOrWhiteSpace(_redundancyActiveLink)
                    && !_redundancyActiveLink.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
                    ? _redundancyActiveLink
                    : "Main";
            }

            string previousActiveLink = _redundancyActiveLink;
            _redundancyActiveLink = inferredActiveLink;
            RedundancyActiveLinkText = "Active link: " + inferredActiveLink;

            if (string.IsNullOrWhiteSpace(previousActiveLink)
                || string.Equals(previousActiveLink, inferredActiveLink, StringComparison.OrdinalIgnoreCase)
                || previousActiveLink.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
                || inferredActiveLink.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _redundancySwitchoverCount++;
            _lastRedundancySwitchUtc = DateTime.UtcNow;
            _giObservedAfterRedundancySwitch = false;
            RedundancyFindingSummaryText = "Redundancy findings: waiting GI observation window.";
            RedundancySwitchSummaryText = "Switchover count: " + _redundancySwitchoverCount.ToString(CultureInfo.InvariantCulture)
                + " | Last switch: " + previousActiveLink + " -> " + inferredActiveLink;
            RedundancyGiObservationText = "GI after switchover: "
                + (_giObservedAfterRedundancySwitch ? "Observed" : "Not observed");
            LastRedundancySwitchText = "Last switchover: " + previousActiveLink + " -> " + inferredActiveLink
                + " @ " + NormalizeTimestamp(timestampText);
            OnPropertyChanged(nameof(RedundancySwitchoverCountValue));
            OnPropertyChanged(nameof(IsGiObservedAfterRedundancySwitch));
            AvailabilityLinkSwitchoverCountText = "Link switchover count: " + _redundancySwitchoverCount.ToString(CultureInfo.InvariantCulture);
            ObserveNucAvailabilitySwitchover(previousActiveLink, inferredActiveLink, reason, NormalizeTimestamp(timestampText));

            AddRedundancyTimeline(
                NormalizeTimestamp(timestampText),
                "Session",
                "Switchover",
                previousActiveLink + " -> " + inferredActiveLink,
                reason,
                "Pending");
            AddRedundancyJournal(NormalizeTimestamp(timestampText), "Session", "Switchover", previousActiveLink + " -> " + inferredActiveLink + " | " + reason);
            AppendNucSoeForensicRow(new SoeForensicRow
            {
                RecvTimeUtc = _lastRedundancySwitchUtc.Value,
                SourceTimeUtc = null,
                DeltaMs = null,
                Channel = inferredActiveLink,
                CA = 0,
                IOA = 0,
                TypeId = 0,
                TypeIdText = "Switch",
                CotText = "-",
                CotRaw = 0,
                SignalName = "Switchover",
                ValueText = previousActiveLink + " -> " + inferredActiveLink,
                QualityText = reason,
                Origin = "Redundancy"
            });
            ScheduleRedundancyGiObservationCheck(_redundancySwitchoverCount);
            UpdateNucRedundancyVisuals();
        }

        private void AddRedundancyTimeline(string time, string channel, string redundancyEvent, string state, string detail, string giObservation)
        {
            BoundedUiBuffer.InsertNewest(RedundancyTimeline, new RedundancyTimelineRow
            {
                Time = time,
                Channel = channel,
                Event = redundancyEvent,
                State = state,
                Detail = detail,
                GiObservation = giObservation
            }, MaxRedundancyTimelineRows);
        }

        private void AddRedundancyJournal(string time, string channel, string journalEvent, string detail)
        {
            BoundedUiBuffer.InsertNewest(RedundancyEventJournal, new RedundancyEventJournalRow
            {
                Time = time,
                Channel = channel,
                Event = journalEvent,
                Detail = detail
            }, MaxRedundancyJournalRows);
        }

        private void AddRedundancyOperationalJournal(string time, string channel, string journalEvent, string detail)
        {
            if (!IsOperationalRedundancyJournalEvent(journalEvent, detail))
            {
                return;
            }

            AddRedundancyJournal(time, channel, journalEvent, detail);
        }

        private static bool IsOperationalRedundancyJournalEvent(string journalEvent, string detail)
        {
            string eventText = (journalEvent ?? string.Empty) + " " + (detail ?? string.Empty);
            return eventText.IndexOf("connect", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("disconnect", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("gi", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("switch", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("fault", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("stuck", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("resumed", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("l1ft", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("l2ft", StringComparison.OrdinalIgnoreCase) >= 0
                || eventText.IndexOf("iedf", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void InitializeNucRedundancyVisualModels()
        {
            SetInfoRowValue(NucMasterPanel.Rows, "Status", "ONLINE");
            SetInfoRowValue(NucMasterPanel.Rows, "Active Link", "A");
            SetInfoRowValue(NucMasterPanel.Rows, "TX Poll", "RUNNING");
            SetInfoRowValue(NucMasterPanel.Rows, "RX Data", "INCOMING");
            SetInfoRowValue(NucMasterPanel.Rows, "TX Count", "1248");
            SetInfoRowValue(NucMasterPanel.Rows, "RX Count", "1250");

            SetInfoRowValue(NucSlavePanel.Rows, "Status", "ONLINE");
            SetInfoRowValue(NucSlavePanel.Rows, "Selected", "A");
            SetInfoRowValue(NucSlavePanel.Rows, "RX Poll", "DETECTED");
            SetInfoRowValue(NucSlavePanel.Rows, "TX Reply", "RUNNING");
            SetInfoRowValue(NucSlavePanel.Rows, "Last Resp", "85 ms ago");
            SetInfoRowValue(NucSlavePanel.Rows, "ACD", "OFF");

            UpdateNucRedundancyVisuals();
        }

        private void UpdateNucLinkTrafficEvidence(string channelName, LineMonitorRow row)
        {
            bool isTx = string.Equals(row.Direction, "TX", StringComparison.OrdinalIgnoreCase);
            bool isRx = string.Equals(row.Direction, "RX", StringComparison.OrdinalIgnoreCase);
            bool isTimeout = IsNucTransportTimeoutEvidence(row);

            RegisterNucLinkActivity(channelName, isTx, isRx);
            ObserveNucRecentTraffic(channelName, row.DataClass, row.Summary, row.Detail, null);

            if (isTimeout)
            {
                DateTime nowUtc = DateTime.UtcNow;
                if (string.Equals(channelName, "Main", StringComparison.OrdinalIgnoreCase))
                {
                    _nucMainLastTimeoutUtc = nowUtc;
                    _nucMainFaultLatched = true;
                }
                else if (string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase))
                {
                    _nucBackupLastTimeoutUtc = nowUtc;
                    _nucBackupFaultLatched = true;
                }
            }
        }

        private void RegisterNucLinkActivity(string channelName, bool isTx, bool isRx)
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (string.Equals(channelName, "Main", StringComparison.OrdinalIgnoreCase))
            {
                _nucMainLastActivityUtc = nowUtc;
                if (isTx)
                {
                    _nucMainTxCount++;
                    _nucMainLastTxUtc = nowUtc;
                }

                if (isRx)
                {
                    _nucMainRxCount++;
                    _nucMainLastRxUtc = nowUtc;
                    _nucMainLastResponseUtc = nowUtc;
                    _nucMainLastTimeoutUtc = null;
                    _nucMainFaultLatched = false;
                }
            }
            else if (string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase))
            {
                _nucBackupLastActivityUtc = nowUtc;
                if (isTx)
                {
                    _nucBackupTxCount++;
                    _nucBackupLastTxUtc = nowUtc;
                }

                if (isRx)
                {
                    _nucBackupRxCount++;
                    _nucBackupLastRxUtc = nowUtc;
                    _nucBackupLastResponseUtc = nowUtc;
                    _nucBackupLastTimeoutUtc = null;
                    _nucBackupFaultLatched = false;
                }
            }

            OnPropertyChanged(nameof(IsNucMainTxRecent));
            OnPropertyChanged(nameof(IsNucMainRxRecent));
            OnPropertyChanged(nameof(IsNucBackupTxRecent));
            OnPropertyChanged(nameof(IsNucBackupRxRecent));
            OnPropertyChanged(nameof(IsNucMainClass1Recent));
            OnPropertyChanged(nameof(IsNucMainClass2Recent));
            OnPropertyChanged(nameof(IsNucMainGiRecent));
            OnPropertyChanged(nameof(IsNucMainLinkCheckRecent));
            OnPropertyChanged(nameof(IsNucMainTimeoutActive));
            OnPropertyChanged(nameof(IsNucMainConnectedIndicator));
            OnPropertyChanged(nameof(IsNucMainPortOpen));
            OnPropertyChanged(nameof(NucMainPortStateText));
            OnPropertyChanged(nameof(NucMainCommStateText));
            OnPropertyChanged(nameof(IsNucBackupClass1Recent));
            OnPropertyChanged(nameof(IsNucBackupClass2Recent));
            OnPropertyChanged(nameof(IsNucBackupGiRecent));
            OnPropertyChanged(nameof(IsNucBackupLinkCheckRecent));
            OnPropertyChanged(nameof(IsNucBackupTimeoutActive));
            OnPropertyChanged(nameof(IsNucBackupConnectedIndicator));
            OnPropertyChanged(nameof(IsNucBackupPortOpen));
            OnPropertyChanged(nameof(NucBackupPortStateText));
            OnPropertyChanged(nameof(NucBackupCommStateText));
        }

        private void UpdateNucRedundancyVisuals()
        {
            _nucMainLinkState = EvaluateNucLinkState(
                true,
                _nucMainConnected,
                _nucMainConnectedAtUtc,
                _nucMainLastResponseUtc,
                _nucMainLastTimeoutUtc);
            _nucBackupLinkState = EvaluateNucLinkState(
                false,
                _nucBackupConnected,
                _nucBackupConnectedAtUtc,
                _nucBackupLastResponseUtc,
                _nucBackupLastTimeoutUtc);

            EvaluateNucActiveLinkCommit();

            string activeChannel = GetPreferredNucActiveChannel();
            bool switching = _lastRedundancySwitchUtc.HasValue
                && DateTime.UtcNow - _lastRedundancySwitchUtc.Value <= NucLinkSwitchingWindow;

            UpdateNucLinkVisual(
                NucLinkAVisual,
                "Main",
                _nucMainLinkState,
                string.Equals(activeChannel, "Main", StringComparison.OrdinalIgnoreCase),
                switching,
                _nucMainRxCount,
                _nucMainTxCount,
                _nucMainLastActivityUtc,
                _nucMainLastTimeoutUtc);
            UpdateNucLinkVisual(
                NucLinkBVisual,
                "Backup",
                _nucBackupLinkState,
                string.Equals(activeChannel, "Backup", StringComparison.OrdinalIgnoreCase),
                switching,
                _nucBackupRxCount,
                _nucBackupTxCount,
                _nucBackupLastActivityUtc,
                _nucBackupLastTimeoutUtc);

            UpdateNucEndpointPanels(activeChannel);
        }

        private void UpdateNucEndpointPanels(string activeChannel)
        {
            bool activeIsBackup = string.Equals(activeChannel, "Backup", StringComparison.OrdinalIgnoreCase);
            NucLinkHealthState activeState = activeIsBackup ? _nucBackupLinkState : _nucMainLinkState;
            NucLinkHealthState standbyState = activeIsBackup ? _nucMainLinkState : _nucBackupLinkState;
            bool activeHealthy = IsNucOperational(activeState);
            bool standbyHealthy = IsNucOperational(standbyState);
            bool anyOperational = activeHealthy || standbyHealthy;
            int totalTx = _nucMainTxCount + _nucBackupTxCount;
            int totalRx = _nucMainRxCount + _nucBackupRxCount;
            DateTime? lastActive = activeIsBackup ? _nucBackupLastResponseUtc : _nucMainLastResponseUtc;

            string masterStatus = anyOperational ? "ONLINE" : "NO RESPONSE";
            Brush masterBrush = anyOperational ? NucActiveBrush : NucFaultBrush;
            NucMasterPanel.StatusText = masterStatus;
            NucMasterPanel.StatusBrush = masterBrush;
            SetInfoRowValue(NucMasterPanel.Rows, "Status", masterStatus);
            SetInfoRowValue(NucMasterPanel.Rows, "Active Link", activeIsBackup ? "B" : "A");
            SetInfoRowValue(NucMasterPanel.Rows, "TX Poll", anyOperational && totalTx > 0 ? "RUNNING" : "IDLE");
            SetInfoRowValue(NucMasterPanel.Rows, "RX Data", anyOperational && totalRx > 0 ? "INCOMING" : "IDLE");
            SetInfoRowValue(NucMasterPanel.Rows, "TX Count", totalTx.ToString(CultureInfo.InvariantCulture));
            SetInfoRowValue(NucMasterPanel.Rows, "RX Count", totalRx.ToString(CultureInfo.InvariantCulture));

            string slaveStatus = activeHealthy ? "ONLINE" : (standbyHealthy ? "STANDBY READY" : "NO RESPONSE");
            Brush slaveBrush = activeHealthy ? NucActiveBrush : (standbyHealthy ? NucStandbyBrush : NucFaultBrush);
            NucSlavePanel.StatusText = slaveStatus;
            NucSlavePanel.StatusBrush = slaveBrush;
            SetInfoRowValue(NucSlavePanel.Rows, "Status", slaveStatus);
            SetInfoRowValue(NucSlavePanel.Rows, "Selected", activeIsBackup ? "B" : "A");
            SetInfoRowValue(NucSlavePanel.Rows, "RX Poll", activeHealthy && totalTx > 0 ? "DETECTED" : (standbyHealthy ? "SUPERVISED" : "NO RESPONSE"));
            SetInfoRowValue(NucSlavePanel.Rows, "TX Reply", activeHealthy && totalRx > 0 ? "RUNNING" : (standbyHealthy ? "STANDBY ONLY" : "IDLE"));
            SetInfoRowValue(NucSlavePanel.Rows, "Last Resp", FormatNucElapsed(lastActive));
            string activeAcd = activeIsBackup ? _nucBackupAcdState : _nucMainAcdState;
            string standbyAcd = activeIsBackup ? _nucMainAcdState : _nucBackupAcdState;
            string acdText = !string.IsNullOrWhiteSpace(activeAcd)
                ? activeAcd
                : (!string.IsNullOrWhiteSpace(standbyAcd)
                    ? standbyAcd
                    : (anyOperational ? "NOT OBSERVED YET" : "UNKNOWN"));
            SetInfoRowValue(NucSlavePanel.Rows, "ACD", acdText);

            if (_mainLinkFaultActive.HasValue)
            {
                RedundancyMainLinkText = "L1FT: " + (_mainLinkFaultActive.Value ? "Fault" : "Healthy");
                OnPropertyChanged(nameof(IsRedundancyMainFaultActive));
            }
            else if (anyOperational)
            {
                RedundancyMainLinkText = "L1FT: Not observed yet";
                OnPropertyChanged(nameof(IsRedundancyMainFaultActive));
            }

            if (_backupLinkFaultActive.HasValue)
            {
                RedundancyBackupLinkText = "L2FT: " + (_backupLinkFaultActive.Value ? "Fault" : "Healthy");
                OnPropertyChanged(nameof(IsRedundancyBackupFaultActive));
            }
            else if (anyOperational)
            {
                RedundancyBackupLinkText = "L2FT: Not observed yet";
                OnPropertyChanged(nameof(IsRedundancyBackupFaultActive));
            }

            if (_iedFaultActive.HasValue)
            {
                RedundancyIedFaultText = "IEDF: " + (_iedFaultActive.Value ? "Fault" : "Healthy");
            }
            else if (anyOperational)
            {
                RedundancyIedFaultText = "IEDF: Not observed yet";
            }
        }

        private void UpdateNucLinkVisual(
            NucLinkVisualViewModel link,
            string channelName,
            NucLinkHealthState linkState,
            bool isActive,
            bool isSwitching,
            int rxCount,
            int txCount,
            DateTime? lastActivityUtc,
            DateTime? lastTimeoutUtc)
          {
              bool isConnected = linkState != NucLinkHealthState.Fault && linkState != NucLinkHealthState.Timeout;
              bool hasRecentTimeout = linkState == NucLinkHealthState.Timeout
                  || (lastTimeoutUtc.HasValue && DateTime.UtcNow - lastTimeoutUtc.Value <= NucLinkTimeoutBadgeWindow);
              bool dataFlowActive = (linkState == NucLinkHealthState.Responsive || (!isActive && txCount > 0))
                  && lastActivityUtc.HasValue
                  && DateTime.UtcNow - lastActivityUtc.Value <= (isActive ? NucLinkFlowWindow : TimeSpan.FromSeconds(5));
              bool canShowTrafficBadges = _nucSessionActive && isActive && !isSwitching && isConnected;
              bool hasRecentClass1 = canShowTrafficBadges && HasNucRecentTraffic(channelName, "Class1");
              bool hasRecentClass2 = canShowTrafficBadges && HasNucRecentTraffic(channelName, "Class2");
              bool hasRecentGi = canShowTrafficBadges && HasNucRecentTraffic(channelName, "GI");
              bool hasRecentSupervision = _nucSessionActive && isConnected && HasNucRecentTraffic(channelName, "SUPERVISION");
              UpdateNucLinkFlowJournalState(channelName, dataFlowActive, isConnected);

            string stateText;
            Brush accentBrush;
            if (linkState == NucLinkHealthState.Fault)
            {
                stateText = "Fault";
                accentBrush = NucFaultBrush;
            }
            else if (linkState == NucLinkHealthState.Timeout)
            {
                stateText = "Timeout";
                accentBrush = NucFaultBrush;
            }
            else if (isSwitching)
            {
                stateText = "Switching";
                accentBrush = NucSwitchingBrush;
            }
            else if (linkState == NucLinkHealthState.ConnectedNoResponse)
            {
                stateText = "No Response";
                accentBrush = NucSwitchingBrush;
            }
            else if (isActive)
            {
                stateText = "Active";
                accentBrush = NucActiveBrush;
            }
            else
            {
                stateText = "Standby";
                accentBrush = NucStandbyBrush;
            }

            link.StateText = stateText;
            link.StateBrush = accentBrush;
            link.LineBrush = accentBrush;
            link.PulseBrush = accentBrush;
            link.CardBrush = isActive ? NucActivePanelBrush : NucPanelBrush;
            link.IsDataFlowActive = dataFlowActive;

            link.Badges.Clear();
            if (linkState != NucLinkHealthState.Timeout && isConnected)
            {
                link.Badges.Add(CreateNucBadge("CONNECTED", new SolidColorBrush(Color.FromRgb(30, 41, 59)), NucTextBrush));
            }

            if (_nucSessionActive && isActive)
            {
                link.Badges.Add(CreateNucBadge("ACTIVE", NucActiveBrush, Brushes.Black));
            }
            else if (_nucSessionActive && isConnected)
            {
                link.Badges.Add(CreateNucBadge("STANDBY", NucStandbyBrush, NucTextBrush));
            }

            if (linkState == NucLinkHealthState.Timeout || !isConnected)
            {
                link.Badges.Add(CreateNucBadge("TIMEOUT", NucFaultBrush, NucTextBrush));
            }
            else if (isConnected)
            {
                link.Badges.Add(CreateNucBadge("HEALTHY", NucActiveBrush, Brushes.Black));
            }

            if (isConnected && hasRecentClass1)
            {
                link.Badges.Add(CreateNucBadge("DATA: CLASS1", NucClass1Brush, Brushes.Black));
            }

            if (isConnected && hasRecentClass2)
            {
                link.Badges.Add(CreateNucBadge("DATA: CLASS2", NucClass2Brush, Brushes.Black));
            }

            if (isConnected && hasRecentGi)
            {
                link.Badges.Add(CreateNucBadge("DATA: GI", NucGiBrush, NucTextBrush));
            }

            if (isConnected && hasRecentSupervision)
            {
                link.Badges.Add(CreateNucBadge("LINK CHECK", new SolidColorBrush(Color.FromRgb(14, 165, 233)), Brushes.Black));
            }

            SetInfoRowValue(
                link.Rows,
                "Mode",
                linkState == NucLinkHealthState.Fault
                    ? "Fault"
                    : linkState == NucLinkHealthState.Timeout
                        ? "Response Timeout"
                        : (isActive ? "Active Polling" : "Standby Supervision"));
            SetInfoRowValue(link.Rows, "RX", rxCount.ToString(CultureInfo.InvariantCulture));
            SetInfoRowValue(link.Rows, "TX", txCount.ToString(CultureInfo.InvariantCulture));
            SetInfoRowValue(link.Rows, "Last Activity", FormatNucElapsed(lastActivityUtc));
        }

        private NucLinkHealthState EvaluateNucLinkState(
            bool isMainChannel,
            bool isConnected,
            DateTime? connectedAtUtc,
            DateTime? lastResponseUtc,
            DateTime? lastTimeoutUtc)
        {
            bool faultLatched = isMainChannel ? _nucMainFaultLatched : _nucBackupFaultLatched;

            if (!isConnected)
            {
                return NucLinkHealthState.Fault;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (lastTimeoutUtc.HasValue && nowUtc - lastTimeoutUtc.Value <= NucLinkTimeoutBadgeWindow)
            {
                return NucLinkHealthState.Timeout;
            }

            if (lastResponseUtc.HasValue)
            {
                if (nowUtc - lastResponseUtc.Value <= NucLinkFlowWindow)
                {
                    if (isMainChannel)
                    {
                        _nucMainFaultLatched = false;
                    }
                    else
                    {
                        _nucBackupFaultLatched = false;
                    }

                    return NucLinkHealthState.Responsive;
                }

                if (isMainChannel)
                {
                    _nucMainFaultLatched = true;
                }
                else
                {
                    _nucBackupFaultLatched = true;
                }

                return NucLinkHealthState.Timeout;
            }

            if (connectedAtUtc.HasValue && nowUtc - connectedAtUtc.Value <= NucLinkInitialResponseWindow)
            {
                return NucLinkHealthState.ConnectedNoResponse;
            }

            if (faultLatched)
            {
                return NucLinkHealthState.Fault;
            }

            if (isMainChannel)
            {
                _nucMainFaultLatched = true;
            }
            else
            {
                _nucBackupFaultLatched = true;
            }

            return NucLinkHealthState.Fault;
        }

        private void EvaluateNucActiveLinkCommit()
        {
            // Active-link ownership is committed only by the redundancy controller/service.
            // The ViewModel is display-only and must not promote/demote links based on local heuristics.
        }

        private NucLinkHealthState GetNucLinkState(string channelName)
        {
            return string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase)
                ? _nucBackupLinkState
                : _nucMainLinkState;
        }

        private static string NormalizeNucChannelName(string channelName)
        {
            if (string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(channelName, "B", StringComparison.OrdinalIgnoreCase))
            {
                return "Backup";
            }

            if (string.Equals(channelName, "Main", StringComparison.OrdinalIgnoreCase)
                || string.Equals(channelName, "A", StringComparison.OrdinalIgnoreCase))
            {
                return "Main";
            }

            return null;
        }

        private static NucChannelRole ParseNucRole(string value, NucChannelRole fallback)
        {
            NucChannelRole parsed;
            return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
        }

        private static NucChannelState ParseNucChannelState(string value, NucChannelState fallback)
        {
            NucChannelState parsed;
            return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
        }

        private static NucLinkHealthState MapControllerChannelState(NucChannelState controllerState, NucChannelRole role)
        {
            switch (controllerState)
            {
                case NucChannelState.StandbySupervision:
                    return NucLinkHealthState.ConnectedNoResponse;
                case NucChannelState.ConnectedNoResponse:
                    return NucLinkHealthState.ConnectedNoResponse;
                case NucChannelState.Responsive:
                    return NucLinkHealthState.Responsive;
                case NucChannelState.Timeout:
                case NucChannelState.FaultLatched:
                    return NucLinkHealthState.Timeout;
                case NucChannelState.Disconnected:
                default:
                    return role == NucChannelRole.Standby ? NucLinkHealthState.ConnectedNoResponse : NucLinkHealthState.Fault;
            }
        }

        private static bool IsNucOperational(NucLinkHealthState state)
        {
            return state == NucLinkHealthState.Responsive || state == NucLinkHealthState.ConnectedNoResponse;
        }

        private static DateTime? ParseUtcTimestamp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            DateTime parsed;
            if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
                out parsed))
            {
                return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
            }

            return null;
        }

        private void UpdateNucLinkFlowJournalState(string channelName, bool dataFlowActive, bool isConnected)
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (string.Equals(channelName, "Main", StringComparison.OrdinalIgnoreCase))
            {
                if (isConnected && dataFlowActive && !_nucMainFlowHealthy)
                {
                    _nucMainFlowHealthy = true;
                    if (!_nucMainLastFlowJournalUtc.HasValue || nowUtc - _nucMainLastFlowJournalUtc.Value >= NucFlowJournalCooldown)
                    {
                        _nucMainLastFlowJournalUtc = nowUtc;
                        AddRedundancyJournal(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Main", "Data flow resumed", "Main link is receiving activity again.");
                    }
                }
                else if (isConnected && !dataFlowActive && _nucMainFlowHealthy)
                {
                    _nucMainFlowHealthy = false;
                    if (!_nucMainLastFlowJournalUtc.HasValue || nowUtc - _nucMainLastFlowJournalUtc.Value >= NucFlowJournalCooldown)
                    {
                        _nucMainLastFlowJournalUtc = nowUtc;
                        AddRedundancyJournal(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Main", "Data flow stuck", "No recent polling/data-flow evidence observed on Main link.");
                    }
                }
                else if (!isConnected)
                {
                    _nucMainFlowHealthy = false;
                }
            }
            else if (string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase))
            {
                if (isConnected && dataFlowActive && !_nucBackupFlowHealthy)
                {
                    _nucBackupFlowHealthy = true;
                    if (!_nucBackupLastFlowJournalUtc.HasValue || nowUtc - _nucBackupLastFlowJournalUtc.Value >= NucFlowJournalCooldown)
                    {
                        _nucBackupLastFlowJournalUtc = nowUtc;
                        AddRedundancyJournal(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Backup", "Data flow resumed", "Backup link is receiving activity again.");
                    }
                }
                else if (isConnected && !dataFlowActive && _nucBackupFlowHealthy)
                {
                    _nucBackupFlowHealthy = false;
                    if (!_nucBackupLastFlowJournalUtc.HasValue || nowUtc - _nucBackupLastFlowJournalUtc.Value >= NucFlowJournalCooldown)
                    {
                        _nucBackupLastFlowJournalUtc = nowUtc;
                        AddRedundancyJournal(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Backup", "Data flow stuck", "No recent polling/data-flow evidence observed on Backup link.");
                    }
                }
                else if (!isConnected)
                {
                    _nucBackupFlowHealthy = false;
                }
            }
        }

        private static NucStatusBadgeViewModel CreateNucBadge(string text, Brush background, Brush foreground)
        {
            return new NucStatusBadgeViewModel
            {
                Text = text,
                BackgroundBrush = background,
                BorderBrush = background,
                ForegroundBrush = foreground
            };
        }

        private void ObserveNucRecentTraffic(string channelName, string dataClass, string summary, string detail, string cot)
        {
            DateTime nowUtc = DateTime.UtcNow;
            string normalizedClass = string.IsNullOrWhiteSpace(dataClass) ? string.Empty : dataClass.Trim();
            bool isGi = (!string.IsNullOrWhiteSpace(cot) && string.Equals(cot, "GI", StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(summary) && summary.IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(detail) && detail.IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0);
            bool isCommandTraffic = (!string.IsNullOrWhiteSpace(summary) && summary.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(detail) && detail.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(summary) && (summary.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0 || summary.IndexOf("Execute", StringComparison.OrdinalIgnoreCase) >= 0))
                || (!string.IsNullOrWhiteSpace(detail) && (detail.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0 || detail.IndexOf("Execute", StringComparison.OrdinalIgnoreCase) >= 0));
            bool isSupervision = (!string.IsNullOrWhiteSpace(summary) && summary.IndexOf("link test", StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(detail) && detail.IndexOf("link-layer test function", StringComparison.OrdinalIgnoreCase) >= 0);

            if (string.Equals(channelName, "Main", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(normalizedClass, "Class 1", StringComparison.OrdinalIgnoreCase) && !isCommandTraffic)
                {
                    _nucMainLastClass1Utc = nowUtc;
                }
                else if (string.Equals(normalizedClass, "Class 2", StringComparison.OrdinalIgnoreCase))
                {
                    _nucMainLastClass2Utc = nowUtc;
                }

                if (isGi)
                {
                    _nucMainLastGiUtc = nowUtc;
                }

                if (isSupervision)
                {
                    _nucMainLastSupervisionUtc = nowUtc;
                }
            }
            else if (string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(normalizedClass, "Class 1", StringComparison.OrdinalIgnoreCase) && !isCommandTraffic)
                {
                    _nucBackupLastClass1Utc = nowUtc;
                }
                else if (string.Equals(normalizedClass, "Class 2", StringComparison.OrdinalIgnoreCase))
                {
                    _nucBackupLastClass2Utc = nowUtc;
                }

                if (isGi)
                {
                    _nucBackupLastGiUtc = nowUtc;
                }

                if (isSupervision)
                {
                    _nucBackupLastSupervisionUtc = nowUtc;
                }
            }
        }

        private bool HasNucRecentTraffic(string channelName, string trafficKind)
        {
            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan window = string.Equals(trafficKind, "GI", StringComparison.OrdinalIgnoreCase)
                ? NucRecentGiBadgeWindow
                : NucRecentDataBadgeWindow;
            DateTime? observedAtUtc;

            if (string.Equals(channelName, "Main", StringComparison.OrdinalIgnoreCase))
            {
                observedAtUtc = string.Equals(trafficKind, "Class1", StringComparison.OrdinalIgnoreCase)
                    ? _nucMainLastClass1Utc
                    : string.Equals(trafficKind, "Class2", StringComparison.OrdinalIgnoreCase)
                        ? _nucMainLastClass2Utc
                        : string.Equals(trafficKind, "SUPERVISION", StringComparison.OrdinalIgnoreCase)
                            ? _nucMainLastSupervisionUtc
                            : _nucMainLastGiUtc;
            }
            else
            {
                observedAtUtc = string.Equals(trafficKind, "Class1", StringComparison.OrdinalIgnoreCase)
                    ? _nucBackupLastClass1Utc
                    : string.Equals(trafficKind, "Class2", StringComparison.OrdinalIgnoreCase)
                        ? _nucBackupLastClass2Utc
                        : string.Equals(trafficKind, "SUPERVISION", StringComparison.OrdinalIgnoreCase)
                            ? _nucBackupLastSupervisionUtc
                            : _nucBackupLastGiUtc;
            }

            return observedAtUtc.HasValue && nowUtc - observedAtUtc.Value <= window;
        }

        private void ClearNucRecentTrafficBadges()
        {
            _nucMainLastClass1Utc = null;
            _nucMainLastClass2Utc = null;
            _nucMainLastGiUtc = null;
            _nucMainLastSupervisionUtc = null;
            _nucBackupLastClass1Utc = null;
            _nucBackupLastClass2Utc = null;
            _nucBackupLastGiUtc = null;
            _nucBackupLastSupervisionUtc = null;
        }

        private static void SetInfoRowValue(ObservableCollection<NucInfoRowViewModel> rows, string label, string value)
        {
            NucInfoRowViewModel row = rows.FirstOrDefault(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                rows.Add(new NucInfoRowViewModel
                {
                    Label = label,
                    Value = value
                });
                return;
            }

            row.Value = value;
        }

        private string GetPreferredNucActiveChannel()
        {
            if (string.Equals(_redundancyActiveLink, "Main", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_redundancyActiveLink, "Backup", StringComparison.OrdinalIgnoreCase))
            {
                return _redundancyActiveLink;
            }

            if (_nucMainConnected && _nucBackupConnected)
            {
                return "Main";
            }

            if (_nucMainConnected)
            {
                return "Main";
            }

            if (_nucBackupConnected)
            {
                return "Backup";
            }

            return "Main";
        }

        private static bool IsRecentNucPulse(DateTime? timestampUtc)
        {
            return timestampUtc.HasValue && DateTime.UtcNow - timestampUtc.Value <= NucPulseWindow;
        }
        private static string FormatNucElapsed(DateTime? timestampUtc)
        {
            if (!timestampUtc.HasValue)
            {
                return "-";
            }

            TimeSpan elapsed = DateTime.UtcNow - timestampUtc.Value;
            if (elapsed.TotalSeconds < 1d)
            {
                return elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            }

            if (elapsed.TotalMinutes < 1d)
            {
                return elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s";
            }

            return elapsed.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture) + " min";
        }

        private void ScheduleRedundancyGiObservationCheck(int switchoverCount)
        {
            _redundancyGiCheckToken++;
            int checkToken = _redundancyGiCheckToken;

            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => RunOnUi(() =>
            {
                if (checkToken != _redundancyGiCheckToken || switchoverCount != _redundancySwitchoverCount)
                {
                    return;
                }

                if (_giObservedAfterRedundancySwitch)
                {
                    AddFindingOnce("REDUNDANCY:GI:WITH:" + switchoverCount, new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Info",
                        Category = "Redundancy",
                        RuleCode = "REDUNDANCY_SWITCHOVER_OBSERVED_WITH_GI",
                        Title = "Switchover observed with GI",
                        IOA = "-",
                        Type = "Redundancy",
                        ExpectedClass = "GI policy depends on project profile",
                        ActualClass = "GI observed within switchover observation window",
                        Detail = "General interrogation activity was observed after redundancy switchover."
                    });
                }
                else
                {
                    AddFindingOnce("REDUNDANCY:GI:WITHOUT:" + switchoverCount, new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Info",
                        Category = "Redundancy",
                        RuleCode = "REDUNDANCY_SWITCHOVER_OBSERVED_WITHOUT_GI",
                        Title = "Switchover observed without GI",
                        IOA = "-",
                        Type = "Redundancy",
                        ExpectedClass = "GI policy depends on project profile",
                        ActualClass = "No GI observed within switchover observation window",
                        Detail = "No general interrogation activity was observed after redundancy switchover during the observation window."
                    });
                    RedundancyGiObservationText = "GI after switchover: Not observed";
                    RedundancyFindingSummaryText = "Redundancy findings: switchover observed without GI.";
                    OnPropertyChanged(nameof(IsGiObservedAfterRedundancySwitch));
                }
            }));
        }

        private static bool IsFaultState(string value)
        {
            return string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "FAULT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "BAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static string InterpretRedundancyState(string value)
        {
            if (IsFaultState(value))
            {
                return "Fault";
            }

            if (string.Equals(value, "OFF", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "NORMAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
            {
                return "Healthy";
            }

            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }

        private void ResetAvailabilityState()
        {
            _availabilitySessionStartedUtc = DateTime.UtcNow;
            _availabilityDisconnectedAtUtc = null;
            _availabilityReconnectCount = 0;
            _availabilitySlaveRecoveryCount = 0;
            _availabilityRtuRestartConfirmedCount = 0;
            _availabilityRtuRestartSuspectedCount = 0;
            _availabilityRestartEvidencePendingUntilUtc = null;
            _availabilityTotalDowntimeMs = 0;
            _availabilityLongestDowntimeMs = 0;
            _availabilitySlaveUnavailableAtUtc = null;
            _availabilitySlaveDowntimeMs = 0;
            _availabilitySlaveLongestDowntimeMs = 0;
            _availabilityObservedEventCount = 0;
            _availabilityProtocolErrorCount = 0;
            _availabilityAcdAssertCount = 0;
            _nucAvailabilityDisconnectedAtUtc = null;
            _nucAvailabilityReconnectCount = 0;
            _nucAvailabilitySlaveRecoveryCount = 0;
            _nucAvailabilityTotalDowntimeMs = 0;
            _nucAvailabilityLongestDowntimeMs = 0;
            _nucAvailabilitySlaveUnavailableAtUtc = null;
            _nucAvailabilitySlaveDowntimeMs = 0;
            _nucAvailabilitySlaveLongestDowntimeMs = 0;
            _nucAvailabilityObservedEventCount = 0;
            _nucAvailabilityProtocolErrorCount = 0;
            _nucAvailabilityAcdAssertCount = 0;
            _nucAvailabilityFlapCount = 0;
            _nucAvailabilityDualUnhealthyEpisodeCount = 0;
            _nucDualUnhealthyLatched = false;
            _nucRecentSwitchoverUtc.Clear();
            _lastSlaveRxUtc = null;
            _lastSlaveValidFrameUtc = null;
            _lastSlaveValidAsduUtc = null;
            _slaveTransportConnectedAtUtc = null;
            _slaveRecentErrorUtc.Clear();
            _slaveAvailabilityState = SlaveAvailabilityState.Disconnected;
            AvailabilitySessionStartedText = "Session started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            AvailabilitySummaryText = "Availability telemetry foundation active.";
            AvailabilityUptimeText = "0m";
            AvailabilityReconnectCountText = "Reconnects: 0";
            AvailabilitySlaveRecoveryCountText = "Slave recovery: 0";
            AvailabilityRtuRestartCountText = "RTU restart: 0 confirmed | 0 suspected";
            AvailabilityDowntimeText = "Transport downtime: 0 ms";
            AvailabilityLongestDowntimeText = "Longest transport outage: 0 ms";
            AvailabilitySlaveDowntimeText = "Slave unavailable: 0 ms";
            AvailabilitySlaveLongestDowntimeText = "Longest slave outage: 0 ms";
            AvailabilityEventThroughputText = "0.0 events/min";
            AvailabilityProtocolErrorCountText = "Protocol errors: 0";
            AvailabilityAcdAssertCountText = "ACD asserted: 0";
            OnPropertyChanged(nameof(NucAvailabilityAcdAssertCountValue));
            AvailabilityFindingsTrendText = "Findings: 0 total | 0 unread";
            AvailabilityLinkSwitchoverCountText = "Link switchover count: 0";
            AvailabilityPercentValue = 100d;
            AvailabilityPercentText = "100.0%";
            AvailabilityStateText = "Healthy";
            ReliabilityScoreValue = 100d;
            ReliabilityScoreText = "100 / 100";
            ReliabilityStateText = "Reliable";
            AvailabilityHealthBreakdownText = "Session initialized. Waiting for communication evidence.";
            AvailabilityDowntimeImpactText = "No downtime recorded yet.";
            AvailabilityRedundancyImpactText = "No switchover observed yet.";
            AvailabilityAnomalyPressureText = "No protocol or finding pressure observed yet.";
            SlaveAvailabilityStateText = "Slave state: Disconnected";
            SlaveAvailabilityDetailText = "Transport is not connected.";
        }

        private void ObserveAvailabilityConnectionState(string newStatus)
        {
            if (string.Equals(newStatus, "Disconnected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(newStatus, "Faulted", StringComparison.OrdinalIgnoreCase))
            {
                _slaveTransportConnectedAtUtc = null;
                _availabilityDisconnectedAtUtc = DateTime.UtcNow;
                AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Connection", newStatus, ConnectionDetail, "-");
                RefreshAvailabilityTelemetry();
                return;
            }

            if (!string.Equals(newStatus, "Connected", StringComparison.OrdinalIgnoreCase))
            {
                RefreshSlaveAvailabilityState();
                return;
            }

            _slaveTransportConnectedAtUtc = DateTime.UtcNow;

            if (_availabilityDisconnectedAtUtc.HasValue)
            {
                double downtimeMs = (DateTime.UtcNow - _availabilityDisconnectedAtUtc.Value).TotalMilliseconds;
                _availabilityReconnectCount++;
                _availabilityTotalDowntimeMs += downtimeMs;
                _availabilityLongestDowntimeMs = Math.Max(_availabilityLongestDowntimeMs, downtimeMs);
                AddAvailabilityTimeline(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    "Connection",
                    "Recovered",
                    "Communication restored",
                    downtimeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms");
                _availabilityDisconnectedAtUtc = null;
                _availabilityRestartEvidencePendingUntilUtc = DateTime.UtcNow.AddSeconds(20);
            }
            else
            {
                AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Connection", "Connected", ConnectionDetail, "-");
            }

            RefreshAvailabilityTelemetry();
        }

        private void ObserveNucAvailabilityConnectionState(string channelName, string newStatus, string detail)
        {
            _nucAvailabilityObservedEventCount++;

            if (string.Equals(newStatus, ConnectionStatusInfo.Disconnected.DisplayText, StringComparison.OrdinalIgnoreCase)
                || string.Equals(newStatus, ConnectionStatusInfo.Faulted.DisplayText, StringComparison.OrdinalIgnoreCase))
            {
                AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Connection", "TransportDisconnected", channelName + " transport disconnected. " + detail, channelName);
            }
            else if (string.Equals(newStatus, ConnectionStatusInfo.Connected.DisplayText, StringComparison.OrdinalIgnoreCase))
            {
                AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Connection", "TransportConnected", channelName + " transport connected. " + detail, channelName);
            }

            RefreshAvailabilityTelemetry();
        }

        private void ObserveAvailabilityLine(LineMonitorRow row)
        {
            if (row == null)
            {
                return;
            }

            if (string.Equals(row.Direction, "RX", StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.ACD, "1", StringComparison.OrdinalIgnoreCase))
            {
                _availabilityAcdAssertCount++;
            }

            DateTime observedUtc = DateTime.UtcNow;

            if (string.Equals(row.Direction, "RX", StringComparison.OrdinalIgnoreCase))
            {
                _lastSlaveRxUtc = observedUtc;

                if (!string.Equals(row.FrameType, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    _lastSlaveValidFrameUtc = observedUtc;
                }

                if (string.Equals(row.FrameType, "ASDU", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(row.AsduType))
                {
                    _lastSlaveValidAsduUtc = observedUtc;
                }

                if (IsEndOfInitializationRow(row))
                {
                    _availabilityRtuRestartConfirmedCount++;
                    _availabilityRestartEvidencePendingUntilUtc = null;
                    AddAvailabilityTimeline(
                        DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                        "Slave",
                        "RTU restart confirmed",
                        "End of Initialization observed from slave.",
                        row.AsduType ?? "-");
                }
            }

            if (string.Equals(row.FrameType, "Error", StringComparison.OrdinalIgnoreCase))
            {
                _availabilityProtocolErrorCount++;
                RegisterSlaveRecentError(observedUtc);
                AddAvailabilityTimeline(
                    DateTime.Today.ToString("yyyy-MM-dd ") + row.Time,
                    "Protocol",
                    "Error frame",
                    row.Summary ?? string.Empty,
                    row.ControlFc ?? "-");
            }

            RefreshAvailabilityTelemetry();
        }

        private void ObserveNucAvailabilityLine(string channelName, LineMonitorRow row)
        {
            if (row == null)
            {
                return;
            }

            _nucAvailabilityObservedEventCount++;
            DateTime observedUtc = DateTime.UtcNow;
            string timestampText = DateTime.Today.ToString("yyyy-MM-dd ") + row.Time;

            if (string.Equals(row.ACD, "1", StringComparison.OrdinalIgnoreCase))
            {
                _nucAvailabilityAcdAssertCount++;
                OnPropertyChanged(nameof(NucAvailabilityAcdAssertCountValue));
            }

            bool isTx = string.Equals(row.Direction, "TX", StringComparison.OrdinalIgnoreCase);
            bool isRx = string.Equals(row.Direction, "RX", StringComparison.OrdinalIgnoreCase);
            bool isError = string.Equals(row.FrameType, "Error", StringComparison.OrdinalIgnoreCase);
            bool isTimeout = IsNucTransportTimeoutEvidence(row);
            bool isGi = (row.Summary ?? string.Empty).IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0
                || (row.Detail ?? string.Empty).IndexOf("GI", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isTx)
            {
                AddAvailabilityTimeline(timestampText, "Traffic", "PollRequestSent", channelName + " TX observed.", row.FrameType ?? "-");
            }

            if (isRx && !isError)
            {
                AddAvailabilityTimeline(timestampText, "Slave", "ValidRxObserved", channelName + " RX observed.", row.FrameType ?? "-");
            }

            if (isTimeout)
            {
                AddAvailabilityTimeline(timestampText, "Incident", "ResponseTimeout", channelName + " timeout/no-response evidence observed.", row.Summary ?? row.Detail ?? "-");
            }

            if (isError)
            {
                _nucAvailabilityProtocolErrorCount++;
                AddAvailabilityTimeline(timestampText, "Protocol", "ProtocolErrorObserved", channelName + " protocol error observed.", row.Summary ?? row.Detail ?? "-");
            }

            if (IsEndOfInitializationRow(row))
            {
                _availabilityRtuRestartConfirmedCount++;
                AddAvailabilityTimeline(timestampText, "Slave", "RtuRestartConfirmed", channelName + " end of initialization observed.", row.AsduType ?? "-");
            }

            if (isGi && _lastRedundancySwitchUtc.HasValue)
            {
                AddAvailabilityTimeline(timestampText, "Redundancy", "GiObservedAfterSwitchover", channelName + " GI evidence observed.", row.Summary ?? "-");
            }

            RefreshAvailabilityTelemetry();
        }

        private static bool HasDecodedAsdu(LineMonitorRow row)
        {
            return row != null
                && !string.IsNullOrWhiteSpace(row.AsduType)
                && !string.Equals(row.AsduType, "-", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNegativeConfirmation(LineMonitorRow row)
        {
            if (row == null)
            {
                return false;
            }

            string detail = row.Detail ?? string.Empty;
            string control = row.ControlFc ?? string.Empty;
            return detail.IndexOf("NEG=1", StringComparison.OrdinalIgnoreCase) >= 0
                || control.IndexOf("NEG=1", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNucTransportTimeoutEvidence(LineMonitorRow row)
        {
            if (row == null)
            {
                return false;
            }

            string summary = row.Summary ?? string.Empty;
            string detail = row.Detail ?? string.Empty;
            string frameType = row.FrameType ?? string.Empty;

            if (IsNucCommandLifecycleEvidence(summary, detail))
            {
                return false;
            }

            return summary.IndexOf("standby supervision timeout", StringComparison.OrdinalIgnoreCase) >= 0
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
        }

        private static bool IsNucCommandLifecycleEvidence(string summary, string detail)
        {
            return ContainsAnyIgnoreCase(summary, detail,
                "command",
                "sbo",
                "select rejected",
                "execute rejected",
                "rejected",
                "follow-up timeout");
        }

        private static bool ContainsAnyIgnoreCase(string summary, string detail, params string[] needles)
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

        private void ObserveNucAvailabilitySwitchover(string previousOwner, string newOwner, string reason, string timestampText)
        {
            _nucAvailabilityObservedEventCount++;
            DateTime nowUtc = DateTime.UtcNow;
            _nucRecentSwitchoverUtc.Enqueue(nowUtc);
            while (_nucRecentSwitchoverUtc.Count > 0 && nowUtc - _nucRecentSwitchoverUtc.Peek() > NucSwitchoverFlapWindow)
            {
                _nucRecentSwitchoverUtc.Dequeue();
            }

            AddAvailabilityTimeline(timestampText, "Redundancy", "SwitchoverOccurred", previousOwner + " -> " + newOwner, reason);

            if (_nucRecentSwitchoverUtc.Count >= 3)
            {
                _nucAvailabilityFlapCount++;
                AddAvailabilityTimeline(timestampText, "Finding", "FlappingDetected", "Rapid A/B ownership oscillation detected.", _nucRecentSwitchoverUtc.Count.ToString(CultureInfo.InvariantCulture));
            }

            RefreshAvailabilityTelemetry();
        }

        private void RefreshAvailabilityTelemetry()
        {
            if (_nucSessionActive)
            {
                RefreshNucAvailabilityTelemetry();
                return;
            }

            RefreshSlaveAvailabilityState();

            TimeSpan uptime = DateTime.UtcNow - _availabilitySessionStartedUtc;
            double elapsedMinutes = Math.Max(1.0 / 60.0, uptime.TotalMinutes);
            double eventsPerMinute = _availabilityObservedEventCount / elapsedMinutes;
            double totalMs = Math.Max(1d, uptime.TotalMilliseconds);
            double disconnectedMs = _availabilityTotalDowntimeMs;
            if (_availabilityDisconnectedAtUtc.HasValue)
            {
                disconnectedMs += (DateTime.UtcNow - _availabilityDisconnectedAtUtc.Value).TotalMilliseconds;
            }
            double slaveUnavailableMs = _availabilitySlaveDowntimeMs;
            if (_availabilitySlaveUnavailableAtUtc.HasValue)
            {
                slaveUnavailableMs += (DateTime.UtcNow - _availabilitySlaveUnavailableAtUtc.Value).TotalMilliseconds;
            }

            double availabilityPercent = Math.Max(0d, Math.Min(100d, 100d - ((disconnectedMs / totalMs) * 100d)));
            double slaveAvailabilityPercent = Math.Max(0d, Math.Min(100d, 100d - ((slaveUnavailableMs / totalMs) * 100d)));
            int criticalFindings = Findings.Count(f => string.Equals(f.Severity, "Critical", StringComparison.OrdinalIgnoreCase));
            int majorFindings = Findings.Count(f => string.Equals(f.Severity, "Major", StringComparison.OrdinalIgnoreCase));
            int minorFindings = Findings.Count(f => string.Equals(f.Severity, "Minor", StringComparison.OrdinalIgnoreCase));

            double reliabilityPenalty = 0d;
            reliabilityPenalty += Math.Min(25d, _availabilityReconnectCount * 4d);
            reliabilityPenalty += Math.Min(20d, (_availabilityLongestDowntimeMs / 1000d) * 1.5d);
            reliabilityPenalty += Math.Min(15d, _availabilityProtocolErrorCount * 3d);
            reliabilityPenalty += Math.Min(12d, _redundancySwitchoverCount * 2d);
            reliabilityPenalty += Math.Min(10d, criticalFindings * 8d);
            reliabilityPenalty += Math.Min(10d, majorFindings * 4d);
            reliabilityPenalty += Math.Min(8d, minorFindings * 1.5d);
            if (_slaveAvailabilityState == SlaveAvailabilityState.Silent)
            {
                reliabilityPenalty += 18d;
            }
            else if (_slaveAvailabilityState == SlaveAvailabilityState.NoApplicationData)
            {
                reliabilityPenalty += 10d;
            }
            else if (_slaveAvailabilityState == SlaveAvailabilityState.Degraded)
            {
                reliabilityPenalty += 12d;
            }

            double reliabilityScore = Math.Max(0d, Math.Min(100d, 100d - reliabilityPenalty));

            AvailabilityUptimeText = uptime.TotalHours >= 1
                ? uptime.ToString(@"dd\.hh\:mm\:ss", CultureInfo.InvariantCulture)
                : uptime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            AvailabilityReconnectCountText = "Reconnects: " + _availabilityReconnectCount.ToString(CultureInfo.InvariantCulture);
            AvailabilitySlaveRecoveryCountText = "Slave recovery: " + _availabilitySlaveRecoveryCount.ToString(CultureInfo.InvariantCulture);
            AvailabilityRtuRestartCountText = "RTU restart: "
                + _availabilityRtuRestartConfirmedCount.ToString(CultureInfo.InvariantCulture)
                + " confirmed | "
                + _availabilityRtuRestartSuspectedCount.ToString(CultureInfo.InvariantCulture)
                + " suspected";
            AvailabilityDowntimeText = "Transport downtime: " + _availabilityTotalDowntimeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilityLongestDowntimeText = "Longest transport outage: " + _availabilityLongestDowntimeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilitySlaveDowntimeText = "Slave unavailable: " + slaveUnavailableMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilitySlaveLongestDowntimeText = "Longest slave outage: " + _availabilitySlaveLongestDowntimeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilityEventThroughputText = eventsPerMinute.ToString("F1", CultureInfo.InvariantCulture) + " events/min";
            AvailabilityProtocolErrorCountText = "Protocol errors: " + _availabilityProtocolErrorCount.ToString(CultureInfo.InvariantCulture);
            AvailabilityAcdAssertCountText = "ACD asserted: " + _availabilityAcdAssertCount.ToString(CultureInfo.InvariantCulture);
            AvailabilityFindingsTrendText = "Findings: " + Findings.Count.ToString(CultureInfo.InvariantCulture)
                + " total | " + (HasUnreadFindings ? "unread present" : "all viewed");
            AvailabilityLinkSwitchoverCountText = "Link switchover count: " + _redundancySwitchoverCount.ToString(CultureInfo.InvariantCulture);
            AvailabilitySummaryText = "Connection " + ConnectionStatus + " | "
                + _availabilityObservedEventCount.ToString(CultureInfo.InvariantCulture) + " event-log rows observed | "
                + SlaveAvailabilityStateText;
            AvailabilityPercentValue = slaveAvailabilityPercent;
            AvailabilityPercentText = slaveAvailabilityPercent.ToString("F1", CultureInfo.InvariantCulture) + "%";
            AvailabilityStateText = GetAvailabilityBandText(slaveAvailabilityPercent);
            ReliabilityScoreValue = reliabilityScore;
            ReliabilityScoreText = reliabilityScore.ToString("F0", CultureInfo.InvariantCulture) + " / 100";
            ReliabilityStateText = GetReliabilityBandText(reliabilityScore);
            AvailabilityHealthBreakdownText =
                "Reconnects " + _availabilityReconnectCount.ToString(CultureInfo.InvariantCulture)
                + " | Recoveries " + _availabilitySlaveRecoveryCount.ToString(CultureInfo.InvariantCulture)
                + " | Events " + _availabilityObservedEventCount.ToString(CultureInfo.InvariantCulture)
                + " | Findings " + Findings.Count.ToString(CultureInfo.InvariantCulture)
                + " | " + SlaveAvailabilityStateText;
            AvailabilityDowntimeImpactText =
                "Transport " + disconnectedMs.ToString("F0", CultureInfo.InvariantCulture)
                + " ms | Slave " + slaveUnavailableMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilityRedundancyImpactText =
                "Switchover " + _redundancySwitchoverCount.ToString(CultureInfo.InvariantCulture)
                + " | " + (_giObservedAfterRedundancySwitch ? "GI observed after last switchover" : "GI not observed after last switchover");
            AvailabilityAnomalyPressureText =
                "Protocol " + _availabilityProtocolErrorCount.ToString(CultureInfo.InvariantCulture)
                + " | Critical " + criticalFindings.ToString(CultureInfo.InvariantCulture)
                + " | Major " + majorFindings.ToString(CultureInfo.InvariantCulture)
                + " | Minor " + minorFindings.ToString(CultureInfo.InvariantCulture)
                + " | " + SlaveAvailabilityDetailText;
        }

        private void RefreshNucAvailabilityTelemetry()
        {
            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan uptime = nowUtc - _availabilitySessionStartedUtc;
            double elapsedMinutes = Math.Max(1.0 / 60.0, uptime.TotalMinutes);
            string activeOwner = NormalizeNucChannelName(_redundancyActiveLink) ?? "Unknown";

            bool transportConnected = _nucMainConnected || _nucBackupConnected;
            bool mainResponsive = IsNucChannelApplicationResponsive("Main", nowUtc);
            bool backupResponsive = IsNucChannelApplicationResponsive("Backup", nowUtc);
            bool slaveResponsive = mainResponsive || backupResponsive;
            bool mainStandbyHealthy = IsNucStandbyHealthyForAvailability(_nucMainConnected, _nucMainLastTimeoutUtc, nowUtc);
            bool backupStandbyHealthy = IsNucStandbyHealthyForAvailability(_nucBackupConnected, _nucBackupLastTimeoutUtc, nowUtc);
            bool effectiveCommunicationHealthy = string.Equals(activeOwner, "Backup", StringComparison.OrdinalIgnoreCase)
                ? backupResponsive
                : string.Equals(activeOwner, "Main", StringComparison.OrdinalIgnoreCase)
                    ? mainResponsive
                    : slaveResponsive;
            bool protocolHealthy = effectiveCommunicationHealthy;
            bool redundancyHealthy = mainStandbyHealthy && backupStandbyHealthy;
            bool bothLinksUnhealthy = !mainStandbyHealthy && !backupStandbyHealthy;

            if (!transportConnected)
            {
                if (!_nucAvailabilityDisconnectedAtUtc.HasValue)
                {
                    _nucAvailabilityDisconnectedAtUtc = nowUtc;
                    AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Connection", "TransportDisconnected", "No link transport is currently connected.", "-");
                }
            }
            else if (_nucAvailabilityDisconnectedAtUtc.HasValue)
            {
                double downtimeMs = (nowUtc - _nucAvailabilityDisconnectedAtUtc.Value).TotalMilliseconds;
                _nucAvailabilityReconnectCount++;
                _nucAvailabilityTotalDowntimeMs += downtimeMs;
                _nucAvailabilityLongestDowntimeMs = Math.Max(_nucAvailabilityLongestDowntimeMs, downtimeMs);
                _nucAvailabilityDisconnectedAtUtc = null;
                AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Connection", "TransportConnected", "At least one link transport is connected again.", downtimeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms");
            }

            if (!effectiveCommunicationHealthy)
            {
                if (!_nucAvailabilitySlaveUnavailableAtUtc.HasValue)
                {
                    _nucAvailabilitySlaveUnavailableAtUtc = nowUtc;
                    AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Slave", "SlaveUnavailableStarted", "No effective active-link communication is currently available.", activeOwner);
                }
            }
            else if (_nucAvailabilitySlaveUnavailableAtUtc.HasValue)
            {
                double slaveOutageMs = (nowUtc - _nucAvailabilitySlaveUnavailableAtUtc.Value).TotalMilliseconds;
                _nucAvailabilitySlaveRecoveryCount++;
                _nucAvailabilitySlaveDowntimeMs += slaveOutageMs;
                _nucAvailabilitySlaveLongestDowntimeMs = Math.Max(_nucAvailabilitySlaveLongestDowntimeMs, slaveOutageMs);
                _nucAvailabilitySlaveUnavailableAtUtc = null;
                AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Slave", "SlaveUnavailableEnded", "Effective active-link communication has recovered.", slaveOutageMs.ToString("F0", CultureInfo.InvariantCulture) + " ms");
            }

            if (bothLinksUnhealthy && !_nucDualUnhealthyLatched)
            {
                _nucDualUnhealthyLatched = true;
                _nucAvailabilityDualUnhealthyEpisodeCount++;
                AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Redundancy", "DualLinkUnhealthy", "Both links are currently unhealthy for export/service.", "-");
            }
            else if (!bothLinksUnhealthy)
            {
                _nucDualUnhealthyLatched = false;
            }

            double disconnectedMs = _nucAvailabilityTotalDowntimeMs;
            if (_nucAvailabilityDisconnectedAtUtc.HasValue)
            {
                disconnectedMs += (nowUtc - _nucAvailabilityDisconnectedAtUtc.Value).TotalMilliseconds;
            }

            double slaveUnavailableMs = _nucAvailabilitySlaveDowntimeMs;
            if (_nucAvailabilitySlaveUnavailableAtUtc.HasValue)
            {
                slaveUnavailableMs += (nowUtc - _nucAvailabilitySlaveUnavailableAtUtc.Value).TotalMilliseconds;
            }

            double slaveAvailabilityPercent = Math.Max(0d, Math.Min(100d, 100d - ((slaveUnavailableMs / Math.Max(1d, uptime.TotalMilliseconds)) * 100d)));
            double reliabilityPenalty = 0d;
            reliabilityPenalty += Math.Min(24d, _nucAvailabilityReconnectCount * 4d);
            reliabilityPenalty += Math.Min(18d, _nucAvailabilityProtocolErrorCount * 2.5d);
            reliabilityPenalty += Math.Min(16d, _nucAvailabilityFlapCount * 5d);
            reliabilityPenalty += Math.Min(14d, _nucAvailabilityDualUnhealthyEpisodeCount * 4d);
            reliabilityPenalty += Math.Min(12d, _redundancySwitchoverCount * 1.5d);
            reliabilityPenalty += Math.Min(10d, Findings.Count * 1.25d);
            if (!protocolHealthy)
            {
                reliabilityPenalty += 10d;
            }

            double reliabilityScore = Math.Max(0d, Math.Min(100d, 100d - reliabilityPenalty));
            double eventsPerMinute = _nucAvailabilityObservedEventCount / elapsedMinutes;

            AvailabilityUptimeText = uptime.TotalHours >= 1
                ? uptime.ToString(@"dd\.hh\:mm\:ss", CultureInfo.InvariantCulture)
                : uptime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            AvailabilityReconnectCountText = "Reconnects: " + _nucAvailabilityReconnectCount.ToString(CultureInfo.InvariantCulture);
            AvailabilitySlaveRecoveryCountText = "Slave recovery: " + _nucAvailabilitySlaveRecoveryCount.ToString(CultureInfo.InvariantCulture);
            AvailabilityRtuRestartCountText = "RTU restart: "
                + _availabilityRtuRestartConfirmedCount.ToString(CultureInfo.InvariantCulture)
                + " confirmed | "
                + _availabilityRtuRestartSuspectedCount.ToString(CultureInfo.InvariantCulture)
                + " suspected";
            AvailabilityDowntimeText = "Transport downtime: " + disconnectedMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilityLongestDowntimeText = "Longest transport outage: " + _nucAvailabilityLongestDowntimeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilitySlaveDowntimeText = "Slave unavailable: " + slaveUnavailableMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilitySlaveLongestDowntimeText = "Longest slave outage: " + _nucAvailabilitySlaveLongestDowntimeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilityEventThroughputText = eventsPerMinute.ToString("F1", CultureInfo.InvariantCulture) + " events/min";
            AvailabilityProtocolErrorCountText = "Protocol errors: " + _nucAvailabilityProtocolErrorCount.ToString(CultureInfo.InvariantCulture);
            AvailabilityAcdAssertCountText = "ACD asserted: " + _nucAvailabilityAcdAssertCount.ToString(CultureInfo.InvariantCulture);
            AvailabilityFindingsTrendText = "Findings: " + Findings.Count.ToString(CultureInfo.InvariantCulture)
                + " total | " + (HasUnreadFindings ? "unread present" : "all viewed");
            AvailabilityLinkSwitchoverCountText = "Link switchover count: " + _redundancySwitchoverCount.ToString(CultureInfo.InvariantCulture);
            AvailabilityPercentValue = slaveAvailabilityPercent;
            AvailabilityPercentText = slaveAvailabilityPercent.ToString("F1", CultureInfo.InvariantCulture) + "%";
            AvailabilityStateText = GetAvailabilityBandText(slaveAvailabilityPercent);
            ReliabilityScoreValue = reliabilityScore;
            ReliabilityScoreText = reliabilityScore.ToString("F0", CultureInfo.InvariantCulture) + " / 100";
            ReliabilityStateText = GetReliabilityBandText(reliabilityScore);
            AvailabilitySummaryText =
                "Transport " + (transportConnected ? "Connected" : "Disconnected")
                + " | Slave " + (slaveResponsive ? "Responsive" : "Unavailable")
                + " | Protocol " + (protocolHealthy ? "Healthy" : "Degraded")
                + " | Redundancy " + (redundancyHealthy ? "Healthy" : "At risk")
                + " | Active owner: " + activeOwner;
            AvailabilityHealthBreakdownText =
                "TransportConnected=" + (transportConnected ? "Yes" : "No")
                + " | SlaveResponsive=" + (slaveResponsive ? "Yes" : "No")
                + " | ProtocolHealthy=" + (protocolHealthy ? "Yes" : "No")
                + " | RedundancyHealthy=" + (redundancyHealthy ? "Yes" : "No")
                + " | ActiveLinkOwner=" + activeOwner;
            AvailabilityDowntimeImpactText =
                "Transport " + disconnectedMs.ToString("F0", CultureInfo.InvariantCulture)
                + " ms | Slave " + slaveUnavailableMs.ToString("F0", CultureInfo.InvariantCulture)
                + " ms | Longest slave " + _nucAvailabilitySlaveLongestDowntimeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            AvailabilityRedundancyImpactText =
                "Switchovers " + _redundancySwitchoverCount.ToString(CultureInfo.InvariantCulture)
                + " | Flaps " + _nucAvailabilityFlapCount.ToString(CultureInfo.InvariantCulture)
                + " | Dual-link unhealthy episodes " + _nucAvailabilityDualUnhealthyEpisodeCount.ToString(CultureInfo.InvariantCulture)
                + " | " + (_giObservedAfterRedundancySwitch ? "GI observed after last switchover" : "GI not observed after last switchover");
            AvailabilityAnomalyPressureText =
                "Protocol errors " + _nucAvailabilityProtocolErrorCount.ToString(CultureInfo.InvariantCulture)
                + " | ACD assertions " + _nucAvailabilityAcdAssertCount.ToString(CultureInfo.InvariantCulture)
                + " | Findings " + Findings.Count.ToString(CultureInfo.InvariantCulture)
                + " | Main=" + (mainResponsive ? "Responsive" : (mainStandbyHealthy ? "Standby healthy" : "Unhealthy"))
                + " | Backup=" + (backupResponsive ? "Responsive" : (backupStandbyHealthy ? "Standby healthy" : "Unhealthy"));
            SlaveAvailabilityStateText =
                "Transport: " + (transportConnected ? "Connected" : "Disconnected")
                + "\nSlave: " + (slaveResponsive ? "Responsive" : "Unavailable");
            SlaveAvailabilityDetailText =
                "Protocol: " + (protocolHealthy ? "Healthy" : "Degraded")
                + "\nRedundancy: " + (redundancyHealthy ? "Healthy" : "At risk")
                + "\nActive owner: " + activeOwner
                + "\nGI after switch: " + (_giObservedAfterRedundancySwitch ? "Observed" : "Not observed");
        }

        private static bool IsNucResponsiveForAvailability(bool isConnected, DateTime? lastRxUtc, DateTime? lastTimeoutUtc, DateTime nowUtc)
        {
            if (!isConnected)
            {
                return false;
            }

            if (lastTimeoutUtc.HasValue && nowUtc - lastTimeoutUtc.Value <= NucLinkTimeoutBadgeWindow)
            {
                return false;
            }

            return lastRxUtc.HasValue && nowUtc - lastRxUtc.Value <= NucLinkFlowWindow;
        }

        private static bool IsNucStandbyHealthyForAvailability(bool isConnected, DateTime? lastTimeoutUtc, DateTime nowUtc)
        {
            if (!isConnected)
            {
                return false;
            }

            return !lastTimeoutUtc.HasValue || nowUtc - lastTimeoutUtc.Value > NucLinkTimeoutBadgeWindow;
        }

        private string GetNucCommStateText(string channelName)
        {
            DateTime nowUtc = DateTime.UtcNow;
            bool isConnected = string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase) ? _nucBackupConnected : _nucMainConnected;
            DateTime? lastTimeoutUtc = string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase) ? _nucBackupLastTimeoutUtc : _nucMainLastTimeoutUtc;
            bool isStandby = IsNucStandbyChannel(channelName);

            if (!isConnected)
            {
                return "NO RESPONSE";
            }

            if (lastTimeoutUtc.HasValue && nowUtc - lastTimeoutUtc.Value <= NucLinkTimeoutBadgeWindow)
            {
                return "TIMEOUT";
            }

            if (IsNucChannelApplicationResponsive(channelName, nowUtc))
            {
                return "RESPONSIVE";
            }

            if (isStandby && HasNucRecentTraffic(channelName, "SUPERVISION"))
            {
                return "RESPONSIVE";
            }

            if (HasNucRecentTraffic(channelName, "SUPERVISION") || string.Equals(channelName, GetPreferredNucActiveChannel(), StringComparison.OrdinalIgnoreCase))
            {
                return "RECOVERING";
            }

            return "NO RESPONSE";
        }

        private bool IsNucStandbyChannel(string channelName)
        {
            if (string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase))
            {
                return _nucBackupRole == NucChannelRole.Standby;
            }

            return _nucMainRole == NucChannelRole.Standby;
        }

        private bool IsNucChannelApplicationResponsive(string channelName, DateTime nowUtc)
        {
            bool isConnected = string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase) ? _nucBackupConnected : _nucMainConnected;
            DateTime? lastTimeoutUtc = string.Equals(channelName, "Backup", StringComparison.OrdinalIgnoreCase) ? _nucBackupLastTimeoutUtc : _nucMainLastTimeoutUtc;
            if (!isConnected)
            {
                return false;
            }

            if (lastTimeoutUtc.HasValue && nowUtc - lastTimeoutUtc.Value <= NucLinkTimeoutBadgeWindow)
            {
                return false;
            }

            return HasNucRecentTraffic(channelName, "Class1")
                || HasNucRecentTraffic(channelName, "Class2")
                || HasNucRecentTraffic(channelName, "GI");
        }

        private void RegisterSlaveRecentError(DateTime observedUtc)
        {
            _slaveRecentErrorUtc.Enqueue(observedUtc);
            TrimSlaveRecentErrors(observedUtc);
        }

        private void TrimSlaveRecentErrors(DateTime nowUtc)
        {
            while (_slaveRecentErrorUtc.Count > 0 && nowUtc - _slaveRecentErrorUtc.Peek() > SlaveRecentErrorWindow)
            {
                _slaveRecentErrorUtc.Dequeue();
            }
        }

        private void RefreshSlaveAvailabilityState()
        {
            DateTime nowUtc = DateTime.UtcNow;
            TrimSlaveRecentErrors(nowUtc);

            SlaveAvailabilityState newState;
            string detail;

            if (string.Equals(ConnectionStatus, ConnectionStatusInfo.Disconnected.DisplayText, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ConnectionStatus, ConnectionStatusInfo.Faulted.DisplayText, StringComparison.OrdinalIgnoreCase))
            {
                newState = SlaveAvailabilityState.Disconnected;
                detail = "Transport is not connected.";
            }
            else if (!string.Equals(ConnectionStatus, ConnectionStatusInfo.Connected.DisplayText, StringComparison.OrdinalIgnoreCase))
            {
                newState = SlaveAvailabilityState.Connecting;
                detail = "Transport is transitioning and waiting for slave response.";
            }
            else if (!_lastSlaveRxUtc.HasValue)
            {
                if (_slaveTransportConnectedAtUtc.HasValue
                    && nowUtc - _slaveTransportConnectedAtUtc.Value > SlaveNoRxWindow)
                {
                    newState = SlaveAvailabilityState.Silent;
                    detail = "Transport is connected but the slave has not produced any RX response in the startup silence window.";
                }
                else
                {
                    newState = SlaveAvailabilityState.TransportUp;
                    detail = "Transport is connected but no RX evidence has been observed yet.";
                }
            }
            else if (nowUtc - _lastSlaveRxUtc.Value > SlaveNoRxWindow)
            {
                newState = SlaveAvailabilityState.Silent;
                detail = "Session is connected but no RX frame has been observed in the silence window.";
            }
            else if (_lastSlaveValidAsduUtc.HasValue && nowUtc - _lastSlaveValidAsduUtc.Value <= SlaveNoAsduWindow)
            {
                newState = SlaveAvailabilityState.ApplicationResponsive;
                detail = _slaveRecentErrorUtc.Count >= SlaveRecentErrorDegradedThreshold
                    ? "Recent valid ASDU/application activity has been observed; transient protocol/error pressure is present."
                    : "Recent valid ASDU/application activity has been observed.";
            }
            else if (_slaveRecentErrorUtc.Count >= SlaveRecentErrorDegradedThreshold)
            {
                newState = SlaveAvailabilityState.Degraded;
                detail = "Recent protocol/error pressure is high and no fresh application ASDU has been observed in the freshness window.";
            }
            else if (_lastSlaveValidFrameUtc.HasValue)
            {
                TimeSpan sinceValidFrame = nowUtc - _lastSlaveValidFrameUtc.Value;
                if (_lastSlaveValidAsduUtc.HasValue && nowUtc - _lastSlaveValidAsduUtc.Value > SlaveNoAsduWindow)
                {
                    newState = SlaveAvailabilityState.NoApplicationData;
                    detail = "Frame-level responses still exist but valid application data is stale.";
                }
                else
                {
                    newState = SlaveAvailabilityState.LinkResponsive;
                    detail = "Frame-level RX is healthy; application freshness is still building.";
                }
            }
            else
            {
                newState = SlaveAvailabilityState.TransportUp;
                detail = "Transport is connected but frame-level responsiveness is not yet proven.";
            }

            if (_slaveAvailabilityState != newState)
            {
                bool oldUnavailable = IsSlaveUnavailableState(_slaveAvailabilityState);
                bool newUnavailable = IsSlaveUnavailableState(newState);
                if (!oldUnavailable && newUnavailable)
                {
                    _availabilitySlaveUnavailableAtUtc = nowUtc;
                }
                else if (oldUnavailable && !newUnavailable && _availabilitySlaveUnavailableAtUtc.HasValue)
                {
                    double slaveOutageMs = (nowUtc - _availabilitySlaveUnavailableAtUtc.Value).TotalMilliseconds;
                    _availabilitySlaveDowntimeMs += slaveOutageMs;
                    _availabilitySlaveLongestDowntimeMs = Math.Max(_availabilitySlaveLongestDowntimeMs, slaveOutageMs);
                    _availabilitySlaveUnavailableAtUtc = null;
                    _availabilitySlaveRecoveryCount++;
                    AddAvailabilityTimeline(
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        "Slave",
                        "Slave recovered",
                        "Slave recovered from unavailable state and is responsive again.",
                        slaveOutageMs.ToString("F0", CultureInfo.InvariantCulture) + " ms");

                    if (_availabilityRestartEvidencePendingUntilUtc.HasValue
                        && nowUtc <= _availabilityRestartEvidencePendingUntilUtc.Value)
                    {
                        _availabilityRtuRestartSuspectedCount++;
                        _availabilityRestartEvidencePendingUntilUtc = null;
                        AddAvailabilityTimeline(
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            "Slave",
                            "RTU restart suspected",
                            "Slave recovered after reconnect without explicit End of Initialization evidence.",
                            "-");
                    }
                }

                _slaveAvailabilityState = newState;
                AddAvailabilityTimeline(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    "Slave",
                    GetSlaveAvailabilityTitle(newState),
                    detail,
                    "-");
                AddStatusHistory(new StatusHistoryRow
                {
                    Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Status = "Slave state",
                    Detail = detail,
                    Level = GetStatusLevelForSlaveAvailabilityState(newState)
                });

                if (newState == SlaveAvailabilityState.Silent)
                {
                    AddFindingOnce("SLAVE:Silent", new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Major",
                        Category = "Availability",
                        RuleCode = "SLAVE_SILENT",
                        Title = "Slave silent while transport stays connected",
                        Detail = detail,
                        IOA = "-",
                        Type = "Slave",
                        ExpectedClass = "Responsive",
                        ActualClass = "Silent"
                    });
                }
                else if (newState == SlaveAvailabilityState.NoApplicationData)
                {
                    AddFindingOnce("SLAVE:NoApplicationData", new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Warning",
                        Category = "Availability",
                        RuleCode = "SLAVE_NO_APPLICATION_DATA",
                        Title = "Slave has frame responses but no fresh application data",
                        Detail = detail,
                        IOA = "-",
                        Type = "Slave",
                        ExpectedClass = "Recent ASDU",
                        ActualClass = "Stale application data"
                    });
                }
                else if (newState == SlaveAvailabilityState.Degraded)
                {
                    AddFindingOnce("SLAVE:Degraded", new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Warning",
                        Category = "Availability",
                        RuleCode = "SLAVE_CORRUPT_RESPONSE_PRESSURE",
                        Title = "Slave response quality degraded",
                        Detail = detail,
                        IOA = "-",
                        Type = "Slave",
                        ExpectedClass = "Stable valid responses",
                        ActualClass = "Repeated error pressure"
                    });
                }
            }

            SlaveAvailabilityStateText = "Slave state: " + GetSlaveAvailabilityTitle(newState);
            SlaveAvailabilityDetailText = detail;

            if (string.Equals(ConnectionStatus, ConnectionStatusInfo.Connected.DisplayText, StringComparison.OrdinalIgnoreCase))
            {
                string baseDetail = ConnectionStatusInfo.Connected.Detail;
                if (newState == SlaveAvailabilityState.TransportUp)
                {
                    ConnectionDetail = baseDetail + " Waiting for slave response.";
                }
                else if (newState == SlaveAvailabilityState.Silent
                    || newState == SlaveAvailabilityState.NoApplicationData
                    || newState == SlaveAvailabilityState.Degraded)
                {
                    ConnectionDetail = baseDetail + " " + detail;
                }
                else
                {
                    ConnectionDetail = baseDetail;
                }
            }
        }

        private static bool IsEndOfInitializationRow(LineMonitorRow row)
        {
            string asduType = row != null ? (row.AsduType ?? string.Empty) : string.Empty;
            return asduType.IndexOf("M_EI_NA_1", StringComparison.OrdinalIgnoreCase) >= 0
                || asduType.IndexOf("EndOfInitialization", StringComparison.OrdinalIgnoreCase) >= 0
                || asduType.IndexOf("End of Initialization", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSlaveUnavailableState(SlaveAvailabilityState state)
        {
            return state == SlaveAvailabilityState.Disconnected
                || state == SlaveAvailabilityState.Silent
                || state == SlaveAvailabilityState.NoApplicationData;
        }

        private static string GetSlaveAvailabilityTitle(SlaveAvailabilityState state)
        {
            switch (state)
            {
                case SlaveAvailabilityState.Disconnected:
                    return "Disconnected";
                case SlaveAvailabilityState.Connecting:
                    return "Connecting";
                case SlaveAvailabilityState.TransportUp:
                    return "Transport up";
                case SlaveAvailabilityState.LinkResponsive:
                    return "Link responsive";
                case SlaveAvailabilityState.ApplicationResponsive:
                    return "Application responsive";
                case SlaveAvailabilityState.NoApplicationData:
                    return "No application data";
                case SlaveAvailabilityState.Silent:
                    return "Silent";
                case SlaveAvailabilityState.Degraded:
                    return "Degraded";
                default:
                    return "Unknown";
            }
        }

        private static string GetStatusLevelForSlaveAvailabilityState(SlaveAvailabilityState state)
        {
            switch (state)
            {
                case SlaveAvailabilityState.ApplicationResponsive:
                case SlaveAvailabilityState.LinkResponsive:
                    return "Info";
                case SlaveAvailabilityState.TransportUp:
                case SlaveAvailabilityState.Connecting:
                case SlaveAvailabilityState.NoApplicationData:
                case SlaveAvailabilityState.Degraded:
                    return "Warn";
                case SlaveAvailabilityState.Silent:
                case SlaveAvailabilityState.Disconnected:
                    return "Error";
                default:
                    return "Info";
            }
        }

        private static string GetReliabilityBandText(double score)
        {
            if (score >= 90d)
            {
                return "Reliable";
            }

            if (score >= 75d)
            {
                return "Degraded";
            }

            return "Critical";
        }

        private static string GetAvailabilityBandText(double score)
        {
            if (score >= 95d)
            {
                return "Healthy";
            }

            if (score >= 80d)
            {
                return "Warning";
            }

            return "Unstable";
        }

        private void AddAvailabilityTimeline(string time, string category, string availabilityEvent, string detail, string metric)
        {
            BoundedUiBuffer.InsertNewest(AvailabilityTimeline, new AvailabilityTimelineRow
            {
                Time = time,
                Category = category,
                Event = availabilityEvent,
                Detail = detail,
                Metric = metric
            }, MaxAvailabilityTimelineRows);
        }

        private static bool TryParseEventTimestampUtc(string timestamp, out DateTime parsedUtc)
        {
            parsedUtc = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(timestamp) || timestamp == "-")
            {
                return false;
            }

            DateTime parsed;
            if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return false;
            }

            parsedUtc = parsed.ToUniversalTime();
            return true;
        }

        private static string FormatSoeDeltaMs(DateTime receiveUtc, DateTime? sourceUtc)
        {
            if (!sourceUtc.HasValue)
            {
                return "-";
            }

            return Math.Round((receiveUtc - sourceUtc.Value).TotalMilliseconds, 0, MidpointRounding.AwayFromZero)
                .ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildCompactClass1BurstSummary(int binaryCount, int analogCount, int commandCount, int otherCount, double durationMs)
        {
            List<string> parts = new List<string>();

            if (commandCount > 0)
            {
                parts.Add(commandCount.ToString(CultureInfo.InvariantCulture) + " command");
            }

            if (binaryCount > 0)
            {
                parts.Add(binaryCount.ToString(CultureInfo.InvariantCulture) + " binary");
            }

            if (analogCount > 0)
            {
                parts.Add(analogCount.ToString(CultureInfo.InvariantCulture) + " analog");
            }

            if (otherCount > 0)
            {
                parts.Add(otherCount.ToString(CultureInfo.InvariantCulture) + "other");
            }

            parts.Add(durationMs.ToString("F0", CultureInfo.InvariantCulture) + "ms");
            return string.Join(" ", parts);
        }

        private void ResetClass1BurstAnalysis()
        {
            _class1BurstActive = false;
            _class1BurstStartUtc = DateTime.MinValue;
            _class1BurstTotalCount = 0;
            _class1BurstMeasurementCount = 0;
            _class1BurstMeteringCount = 0;
            _class1BurstDiscreteCount = 0;
            _class1BurstCommandCount = 0;
            _class1BurstOtherCount = 0;
            _class1BurstToggleCount = 0;
            _class1BurstBinaryIoas.Clear();
            _class1BurstAnalogIoas.Clear();
            _class1BurstCommandIoas.Clear();
        }

        private void StartClass1Burst(DateTime timestampUtc)
        {
            unchecked
            {
                _class1BurstFinalizeToken++;
            }

            _class1BurstActive = true;
            _class1BurstStartUtc = timestampUtc;
            _class1BurstTotalCount = 0;
            _class1BurstMeasurementCount = 0;
            _class1BurstMeteringCount = 0;
            _class1BurstDiscreteCount = 0;
            _class1BurstCommandCount = 0;
            _class1BurstOtherCount = 0;
            _class1BurstToggleCount++;
            _class1BurstBinaryIoas.Clear();
            _class1BurstAnalogIoas.Clear();
            _class1BurstCommandIoas.Clear();
        }

        private void NoteClass1BurstValue(int ioa, string type)
        {
            if (!_class1BurstActive)
            {
                return;
            }

            if (IsDiscreteType(type))
            {
                if (_class1BurstBinaryIoas.Add(ioa))
                {
                    _class1BurstDiscreteCount++;
                    _class1BurstTotalCount++;
                }

                return;
            }

            if (IsMeteringType(type))
            {
                if (_class1BurstAnalogIoas.Add(ioa))
                {
                    _class1BurstMeasurementCount++;
                    _class1BurstTotalCount++;

                    if (!string.IsNullOrWhiteSpace(type)
                        && type.IndexOf("Integrated", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _class1BurstMeteringCount++;
                    }
                }

                return;
            }
        }

        private void NoteClass1BurstCommand(string ioaText)
        {
            if (!_class1BurstActive)
            {
                return;
            }

            if (!int.TryParse(ioaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ioa))
            {
                return;
            }

            if (_class1BurstCommandIoas.Add(ioa))
            {
                _class1BurstCommandCount++;
                _class1BurstTotalCount++;
            }
        }

        private void NoteClass1BurstAsdu(LineMonitorRow row)
        {
            if (!_class1BurstActive || row == null)
            {
                return;
            }

            if (!string.Equals(row.Direction, "RX", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(row.DataClass, "Class 1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string asduType = row.AsduType ?? string.Empty;

            // Ignore poll/system rows that are not actual information objects
            if (string.IsNullOrWhiteSpace(asduType) || asduType == "-")
            {
                return;
            }

            // Command ASDUs are counted separately elsewhere
            if (asduType.IndexOf("C_SC_", StringComparison.OrdinalIgnoreCase) >= 0
                || asduType.IndexOf("C_DC_", StringComparison.OrdinalIgnoreCase) >= 0
                || asduType.IndexOf("C_RC_", StringComparison.OrdinalIgnoreCase) >= 0
                || asduType.IndexOf("C_IC_", StringComparison.OrdinalIgnoreCase) >= 0
                || asduType.IndexOf("C_CS_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            string ioaText = string.IsNullOrWhiteSpace(row.IOA) ? ExtractIoaFromDetail(row.Detail) : row.IOA;
            if (int.TryParse(ioaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ioa))
            {
                if (asduType.IndexOf("M_SP_", StringComparison.OrdinalIgnoreCase) >= 0
                    || asduType.IndexOf("M_DP_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (_class1BurstBinaryIoas.Add(ioa))
                    {
                        _class1BurstDiscreteCount++;
                        _class1BurstTotalCount++;
                    }

                    return;
                }

                if (asduType.IndexOf("M_ME_", StringComparison.OrdinalIgnoreCase) >= 0
                    || asduType.IndexOf("M_IT_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (_class1BurstAnalogIoas.Add(ioa))
                    {
                        _class1BurstMeasurementCount++;
                        _class1BurstTotalCount++;

                        if (asduType.IndexOf("M_IT_", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _class1BurstMeteringCount++;
                        }
                    }

                    return;
                }
            }

            _class1BurstOtherCount++;
            _class1BurstTotalCount++;
        }

        private void ScheduleCompleteClass1Burst(string eventTimeText)
        {
            int finalizeToken = _class1BurstFinalizeToken;

            Task.Delay(Class1BurstFinalizeGraceWindow).ContinueWith(_ =>
                RunOnUi(() =>
                {
                    if (finalizeToken != _class1BurstFinalizeToken)
                    {
                        return;
                    }

                    CompleteClass1Burst(eventTimeText);
                }));
        }

        private void CompleteClass1Burst(string eventTimeText)
        {
            if (!_class1BurstActive)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            double durationMs = 0;
            if (_class1BurstStartUtc != DateTime.MinValue)
            {
                durationMs = Math.Max(0, (nowUtc - _class1BurstStartUtc).TotalMilliseconds);
            }

            int measurementLikeCount = _class1BurstMeasurementCount;
            int discreteCount = _class1BurstDiscreteCount;
            int commandCount = _class1BurstCommandCount;
            int otherCount = _class1BurstOtherCount;
            int totalCount = _class1BurstTotalCount;

            string summaryEvent;
            if (totalCount <= 0)
            {
                if (ShouldSuppressEmptyClass1BurstSummaryDuringGi(nowUtc))
                {
                    ResetClass1BurstAnalysis();
                    return;
                }

                summaryEvent = string.Format(
                    CultureInfo.InvariantCulture,
                    "⚡ Class 1 burst summary: no mapped ASDU objects observed, duration {0:F0} ms",
                    durationMs);
            }
            else
            {
                summaryEvent = string.Format(
                    CultureInfo.InvariantCulture,
                    "⚡ Class 1 burst summary: {0} objects ({1} measurement, {2} discrete, {3} command, {4} other), duration {5:F0} ms",
                    totalCount,
                    measurementLikeCount,
                    discreteCount,
                    commandCount,
                    otherCount,
                    durationMs);
            }

            if (totalCount <= 0)
            {
                summaryEvent = string.Format(
                    CultureInfo.InvariantCulture,
                    "\u26A0 Class 1 empty {0:F0} ms",
                    durationMs);
            }
            else
            {
                summaryEvent = string.Format(
                    CultureInfo.InvariantCulture,
                    "\u26A1 Class 1: {0}",
                    BuildCompactClass1BurstSummary(discreteCount, measurementLikeCount, commandCount, otherCount, durationMs));
            }

            TryAddEventLog(new EventLogRow
            {
                Time = eventTimeText,
                Name = "System",
                IOA = "-",
                Type = "Class 1",
                Event = summaryEvent,
                Value = string.Empty,
                Quality = string.Empty,
                Acd = "-",
                Cot = "Summary",
                Source = "System",
                DataClass = "Class 1"
            });

            if (totalCount == 0)
            {
                string findingKey = "ACDNOISE:EMPTY";
                int evidenceCount = 0;
                _findingEvidenceCounts.TryGetValue(findingKey, out evidenceCount);
                evidenceCount++;
                _findingEvidenceCounts[findingKey] = evidenceCount;

                if (evidenceCount >= 3)
                {
                    AddFindingOnce(findingKey, new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Warning",
                        Category = "Protocol",
                        Title = "Repeated empty Class 1 bursts",
                        Detail = string.Format(
                            CultureInfo.InvariantCulture,
                            "RTU asserted Class 1 availability (ACD=1) but no mapped ASDU objects were observed before queue clear. Observed {0} times. This may indicate empty Class 1 bursts, event buffer anomalies, or RTU/gateway classification issues.",
                            evidenceCount),
                        IOA = "-",
                        Type = "Class 1",
                        ExpectedClass = "Class 1 payload present",
                        ActualClass = "ACD asserted with no mapped payload"
                    });
                }
            }

            ResetClass1BurstAnalysis();
        }

        private static string GetCommandFamilyLabel(string asduType)
        {
            if (asduType.IndexOf("C_SC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Single Command";
            }

            if (asduType.IndexOf("C_DC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Double Command";
            }

            if (asduType.IndexOf("C_RC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Regulating Command";
            }

            if (asduType.IndexOf("C_SE_NA_1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Setpoint Command";
            }

            return "Command";
        }

        private static string GetCommandModeLabel(bool select)
        {
            return select ? "SBO Select" : "DO";
        }

        private static string GetCommandOperationLabel(LineMonitorRow row)
        {
            string detail = (row.Detail ?? string.Empty) + " " + (row.RawHex ?? string.Empty);

            if (row.AsduType.IndexOf("C_DC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (detail.IndexOf("CLOSE", StringComparison.OrdinalIgnoreCase) >= 0 || detail.IndexOf(" ON", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "CLOSE";
                }

                if (detail.IndexOf("OPEN", StringComparison.OrdinalIgnoreCase) >= 0 || detail.IndexOf(" OFF", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "OPEN";
                }
            }
            else if (row.AsduType.IndexOf("C_SC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (detail.IndexOf(" ON", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "ON";
                }

                if (detail.IndexOf(" OFF", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "OFF";
                }
            }
            else if (row.AsduType.IndexOf("C_RC_NA_1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (detail.IndexOf("HIGHER", StringComparison.OrdinalIgnoreCase) >= 0 || detail.IndexOf("RAISE", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "RAISE";
                }

                if (detail.IndexOf("LOWER", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "LOWER";
                }
            }
            else if (row.AsduType.IndexOf("C_SE_NA_1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                const string marker = "Value=";
                int start = detail.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (start >= 0)
                {
                    start += marker.Length;
                    int end = start;
                    while (end < detail.Length && "-+.0123456789".IndexOf(detail[end]) >= 0)
                    {
                        end++;
                    }

                    if (end > start)
                    {
                        return detail.Substring(start, end - start);
                    }
                }
            }

            return string.Empty;
        }

        private static string ExtractIoaFromDetail(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return "-";
            }

            string[] markers = { "IOA ", "IOA=" };

            foreach (string marker in markers)
            {
                int index = detail.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    continue;
                }

                index += marker.Length;
                int end = index;
                while (end < detail.Length && char.IsDigit(detail[end]))
                {
                    end++;
                }

                if (end > index)
                {
                    return detail.Substring(index, end - index);
                }
            }

            return "-";
        }

        private CommandTransaction RegisterPendingCommand(int ioa, string commandType, string operation, bool select)
        {
            string ioaText = ioa.ToString(CultureInfo.InvariantCulture);
            string modeLabel = DetermineCommandModeLabel(ioaText, select);
            CommandTransaction transaction = _commandTracker.RegisterTx(
                ioaText,
                commandType,
                operation,
                modeLabel,
                DateTime.UtcNow,
                GetCommandTransactionTimeout());

            string txStage;
            if (string.Equals(modeLabel, "SBO Select", StringComparison.OrdinalIgnoreCase))
            {
                txStage = "SelectTx";
            }
            else if (string.Equals(modeLabel, "SBO Execute", StringComparison.OrdinalIgnoreCase))
            {
                txStage = "ExecuteTx";
            }
            else
            {
                txStage = "DoTx";
            }

            TrackCommandLifecycle(ioaText, txStage, operation, false);
            return transaction;
        }

        private void LogCommandTransmission(CommandTransaction transaction)
        {
            if (transaction == null)
            {
                return;
            }

            AddCommandLifeMonitorRow(transaction, "TX");

            TryAddEventLog(new EventLogRow
            {
                Time = transaction.TxTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Name = "Command",
                IOA = transaction.CommandIoa,
                Type = transaction.CommandType,
                Event = "🢂 " + transaction.Mode + " transmitted",
                Value = transaction.Operation,
                Quality = string.Empty,
                Acd = "-",
                Cot = "-",
                Source = "Master",
                DataClass = "Class 1"
            });
        }

        private CommandTransaction ResolvePendingCommand(string ioa, string commandType, string operation, string rxMode, LineMonitorRow row, bool isNegative)
        {
            string resolvedMode = rxMode;

            if (string.IsNullOrWhiteSpace(resolvedMode))
            {
                bool? selectFlag = TryGetSelectFlag(row);
                if (selectFlag.HasValue)
                {
                    resolvedMode = selectFlag.Value ? "SBO Select" : "SBO Execute";
                }
            }

            return _commandTracker.TryResolveRx(ioa, commandType, operation, resolvedMode, DateTime.UtcNow, isNegative);
        }

        private string DetermineCommandModeLabel(string ioa, bool select)
        {
            if (select)
            {
                return "SBO Select";
            }

            string lastStage;
            if (!string.IsNullOrWhiteSpace(ioa) && _commandLifecycle.TryGetValue(ioa, out lastStage))
            {
                if (string.Equals(lastStage, "SelectTx", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(lastStage, "SelectConfirmed", StringComparison.OrdinalIgnoreCase))
                {
                    return "SBO Execute";
                }
            }

            return "DO";
        }

        private static string TryGetRxModeLabel(LineMonitorRow row)
        {
            bool? selectFlag = TryGetSelectFlag(row);
            if (selectFlag.HasValue)
            {
                return selectFlag.Value ? "SBO Select" : "SBO Execute";
            }

            return null;
        }

        private TimeSpan GetCommandTransactionTimeout()
        {
            int responseTimeoutMs = CurrentSettings != null ? CurrentSettings.ResponseTimeoutMs : 1000;

            double timeoutMs = Math.Max(3000, responseTimeoutMs * 4);

            return TimeSpan.FromMilliseconds(timeoutMs);
        }

        private void ScheduleCommandTimeoutCheck()
        {
            TimeSpan delay = GetCommandTransactionTimeout();

            // give a little extra margin so late confirmations are less likely
            // to be marked as timeout too early
            delay = delay.Add(TimeSpan.FromMilliseconds(500));

            Task.Delay(delay).ContinueWith(_ => RunOnUi(ProcessTimedOutCommandTransactions));
        }

        private void ProcessTimedOutCommandTransactions()
        {
            List<CommandTransaction> timedOutTransactions = _commandTracker.GetTimedOutTransactions(DateTime.UtcNow);
            for (int index = 0; index < timedOutTransactions.Count; index++)
            {
                CommandTransaction transaction = timedOutTransactions[index];
                string ioa = transaction.CommandIoa;

                string timeoutStage = string.Equals(transaction.Mode, "SBO Select", StringComparison.OrdinalIgnoreCase)
                    ? "SelectTimeout"
                    : string.Equals(transaction.Mode, "SBO Execute", StringComparison.OrdinalIgnoreCase)
                        ? "ExecuteTimeout"
                        : "DoTimeout";

                TrackCommandLifecycle(ioa, timeoutStage, transaction.Operation, false);

                string timeoutText = string.Equals(transaction.Mode, "SBO Select", StringComparison.OrdinalIgnoreCase)
                    ? "\u26A0 SBO Select timeout"
                    : string.Equals(transaction.Mode, "SBO Execute", StringComparison.OrdinalIgnoreCase)
                        ? "\u26A0 SBO Execute timeout"
                        : "\u26A0 Command timeout";

                TryAddEventLog(new EventLogRow
                {
                    Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Name = "Command",
                    IOA = ioa,
                    Type = transaction.CommandType,
                    Event = timeoutText,
                    Value = transaction.Operation,
                    Quality = string.Empty,
                    Acd = "-",
                    Cot = "Timeout",
                    Source = "System",
                    DataClass = "Class 1"
                });

                AddCommandLifeMonitorRow(transaction, "TO");
            }
        }

        private void AddCommandLifeMonitorRow(CommandTransaction transaction, string resultShort)
        {
            if (transaction == null)
            {
                return;
            }

            transaction.UiPublishedAtUtc = DateTime.UtcNow;
            if (string.Equals(resultShort, "OK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(resultShort, "REJ", StringComparison.OrdinalIgnoreCase))
            {
                transaction.ResponsePublishedAtUtc = DateTime.UtcNow;
            }
            BoundedUiBuffer.InsertNewest(CommandLifeMonitor, new CommandLifeMonitorRow
            {
                TimeText = BuildCommandLifeMonitorTime(transaction, resultShort),
                IoaText = string.IsNullOrWhiteSpace(transaction.CommandIoa) ? "-" : transaction.CommandIoa,
                Operation = string.IsNullOrWhiteSpace(transaction.Operation) ? "-" : transaction.Operation,
                ModeShort = ToCommandLifeModeShort(transaction.Mode),
                ResultShort = string.IsNullOrWhiteSpace(resultShort) ? "-" : resultShort,
                LatencyText = BuildCommandLifeLatencyText(transaction, resultShort),
                FeedbackText = ResolveCommandFeedbackValue(transaction.CommandIoa)
            }, MaxCommandLifeMonitorRows);
        }

        private static string BuildCommandLifeMonitorTime(CommandTransaction transaction, string resultShort)
        {
            DateTime timestamp = transaction.IssuedAtUtc;
            if (!string.Equals(resultShort, "TX", StringComparison.OrdinalIgnoreCase)
                && transaction.ConfirmTimeUtc.HasValue)
            {
                timestamp = transaction.ConfirmTimeUtc.Value;
            }

            return timestamp.ToLocalTime().ToString("HH:mm:ss");
        }

        private static string ToCommandLifeModeShort(string mode)
        {
            if (string.Equals(mode, "SBO Select", StringComparison.OrdinalIgnoreCase))
            {
                return "SBO-SEL";
            }

            if (string.Equals(mode, "SBO Execute", StringComparison.OrdinalIgnoreCase))
            {
                return "SBO-EXE";
            }

            return "DO";
        }

        private static string BuildCommandLifeLatencyText(CommandTransaction transaction, string resultShort)
        {
            if (string.Equals(resultShort, "TX", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "issue->tx {0:F0} ms",
                    (transaction.TxTimeUtc - transaction.IssuedAtUtc).TotalMilliseconds);
            }

            if ((string.Equals(resultShort, "OK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(resultShort, "REJ", StringComparison.OrdinalIgnoreCase))
                && transaction.ConfirmLatencyMs.HasValue)
            {
                double issueToTx = (transaction.TxTimeUtc - transaction.IssuedAtUtc).TotalMilliseconds;
                double txToRx = transaction.ConfirmLatencyMs.Value;
                double rxToUi = transaction.UiPublishedAtUtc.HasValue
                    ? (transaction.UiPublishedAtUtc.Value - transaction.ConfirmTimeUtc.GetValueOrDefault(transaction.UiPublishedAtUtc.Value)).TotalMilliseconds
                    : 0;
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "i->t {0:F0} | t->r {1:F0} | r->u {2:F0}",
                    issueToTx,
                    txToRx,
                    rxToUi);
            }

            return string.Empty;
        }

        private static bool IsSelectCommand(LineMonitorRow row)
        {
            bool? selectFlag = TryGetSelectFlag(row);
            return selectFlag.HasValue && selectFlag.Value;
        }

        private static bool? TryGetSelectFlag(LineMonitorRow row)
        {
            if (row == null)
            {
                return null;
            }

            string detail = row.Detail ?? string.Empty;
            if (detail.IndexOf("Select=1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (detail.IndexOf("Select=0", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return null;
        }

        private static string GetCommandTypeForFamily(string family)
        {
            switch (family)
            {
                case "Single":
                    return "Single Command";
                case "Double":
                    return "Double Command";
                case "Regulating":
                    return "Regulating Command";
                case "Setpoint":
                    return "Setpoint Command";
                default:
                    return "Command";
            }
        }

        private string ResolveCommandFeedbackValue(string commandIoaText)
        {
            if (string.IsNullOrWhiteSpace(commandIoaText))
            {
                return "-";
            }

            int commandIoa;
            if (!int.TryParse(commandIoaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out commandIoa))
            {
                return "-";
            }

            int? feedbackIoa = OfficialPointProfiles.TryGetRelatedFeedbackIoa(commandIoa);
            if (!feedbackIoa.HasValue)
            {
                return "-";
            }

            ValueViewerRow row;
            if (_nucValueIndex.TryGetValue(feedbackIoa.Value, out row) && !string.IsNullOrWhiteSpace(row.Value))
            {
                return row.Value;
            }

            if (_valueIndex.TryGetValue(feedbackIoa.Value, out row) && !string.IsNullOrWhiteSpace(row.Value))
            {
                return row.Value;
            }

            return "-";
        }

        public ValueViewerRow TryGetCurrentValueByIoa(int ioa, bool useNucSession)
        {
            ValueViewerRow row;
            if (useNucSession)
            {
                return _nucValueIndex.TryGetValue(ioa, out row) ? row : null;
            }

            return _valueIndex.TryGetValue(ioa, out row) ? row : null;
        }

        private static string NormalizeCommandOperation(string familyOrType, string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                return string.Empty;
            }

            if (string.Equals(familyOrType, "Double", StringComparison.OrdinalIgnoreCase)
                || string.Equals(familyOrType, "Double Command", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(operation, "ON", StringComparison.OrdinalIgnoreCase))
                {
                    return "CLOSE";
                }

                if (string.Equals(operation, "OFF", StringComparison.OrdinalIgnoreCase))
                {
                    return "OPEN";
                }
            }

            if (string.Equals(familyOrType, "Setpoint", StringComparison.OrdinalIgnoreCase)
                || string.Equals(familyOrType, "Setpoint Command", StringComparison.OrdinalIgnoreCase))
            {
                float normalizedValue;
                return float.TryParse(operation, NumberStyles.Float, CultureInfo.InvariantCulture, out normalizedValue)
                    ? normalizedValue.ToString("0.###", CultureInfo.InvariantCulture)
                    : operation;
            }

            return operation.ToUpperInvariant();
        }

        private void TrackCommandLifecycle(string ioa, string stage, string operation, bool isNegative)
        {
            if (string.IsNullOrWhiteSpace(ioa) || ioa == "-")
                return;

            string lastStage;
            _commandLifecycle.TryGetValue(ioa, out lastStage);

            if (stage == "SelectTx")
            {
                _commandLifecycle[ioa] = "SelectTx";
                return;
            }

            if (stage == "SelectRx")
            {
                _commandLifecycle[ioa] = isNegative ? "SelectRejected" : "SelectConfirmed";
                if (isNegative)
                {
                    AddFindingOnce("CMDREJ:SEL:" + ioa, new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Warning",
                        Category = "Command",
                        RuleCode = "COMMAND_SELECT_REJECTED",
                        Title = "Select command rejected",
                        Detail = $"IOA {ioa} select command was rejected by slave.",
                        IOA = ioa,
                        Type = "Command",
                        ExpectedClass = "Select confirmed",
                        ActualClass = "Select rejected"
                    });
                }
                return;
            }

            if (stage == "SelectTimeout")
            {
                _commandLifecycle[ioa] = "SelectTimeout";
                return;
            }

            if (stage == "ExecuteTx")
            {
                if (!string.Equals(lastStage, "SelectTx", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(lastStage, "SelectConfirmed", StringComparison.OrdinalIgnoreCase))
                {
                    AddFindingOnce("CMDEXEC:" + ioa, new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Warning",
                        Category = "Command",
                        RuleCode = "EXECUTE_WITHOUT_SELECT",
                        Title = "Execute without Select",
                        Detail = $"IOA {ioa} execute command was transmitted without prior select confirmation.",
                        IOA = ioa,
                        Type = "Command",
                        ExpectedClass = "Select → Execute",
                        ActualClass = "Execute only"
                    });
                }

                _commandLifecycle[ioa] = "ExecuteTx";
                return;
            }

            if (stage == "ExecuteRx")
            {
                _commandLifecycle[ioa] = isNegative ? "ExecuteRejected" : "ExecuteConfirmed";
                if (isNegative)
                {
                    AddFindingOnce("CMDREJ:EXE:" + ioa, new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Warning",
                        Category = "Command",
                        RuleCode = "COMMAND_EXECUTE_REJECTED",
                        Title = "Execute command rejected",
                        Detail = $"IOA {ioa} execute command was rejected by slave.",
                        IOA = ioa,
                        Type = "Command",
                        ExpectedClass = "Execute confirmed",
                        ActualClass = "Execute rejected"
                    });
                }
                return;
            }

            if (stage == "ExecuteTimeout")
            {
                _commandLifecycle[ioa] = "ExecuteTimeout";
                return;
            }

            if (stage == "DoTx")
            {
                _commandLifecycle[ioa] = "DoTx";
                return;
            }

            if (stage == "DoRx")
            {
                _commandLifecycle[ioa] = isNegative ? "DoRejected" : "DoConfirmed";
                if (isNegative)
                {
                    AddFindingOnce("CMDREJ:DO:" + ioa, new FindingRow
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Severity = "Warning",
                        Category = "Command",
                        RuleCode = "COMMAND_DIRECT_REJECTED",
                        Title = "Direct command rejected",
                        Detail = $"IOA {ioa} direct operate command was rejected by slave.",
                        IOA = ioa,
                        Type = "Command",
                        ExpectedClass = "Direct confirmed",
                        ActualClass = "Direct rejected"
                    });
                }
                return;
            }

            if (stage == "DoTimeout")
            {
                _commandLifecycle[ioa] = "DoTimeout";
                return;
            }
        }

        private void AddFindingOnce(string key, FindingRow finding)
        {
            if (finding == null || string.IsNullOrWhiteSpace(key))
                return;

            if (_activeFindingKeys.Add(key))
            {
                BoundedUiBuffer.InsertNewest(Findings, finding, MaxFindingRows);
                HasUnreadFindings = true;
                RefreshRedundancyFindingsDashboard();
                RefreshAvailabilityTelemetry();
                AddAvailabilityTimeline(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "Finding", finding.RuleCode ?? finding.Title ?? "Finding", finding.Detail ?? string.Empty, finding.Severity ?? "-");
                return;
            }

            for (int index = 0; index < Findings.Count; index++)
            {
                FindingRow existing = Findings[index];
                if (existing == null)
                {
                    continue;
                }

                if (string.Equals(existing.IOA, finding.IOA, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Title, finding.Title, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    RefreshRedundancyFindingsDashboard();
                    break;
                }
            }
        }

        private void RefreshRedundancyFindingsDashboard()
        {
            List<FindingRow> redundancyFindings = Findings
                .Where(f => f != null
                    && (string.Equals(f.Category, "Redundancy", StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrWhiteSpace(f.RuleCode) && f.RuleCode.IndexOf("REDUNDANCY", StringComparison.OrdinalIgnoreCase) >= 0)))
                .Take(4)
                .ToList();

            if (redundancyFindings.Count == 0)
            {
                RedundancyFindingDetailsText =
                    "No active redundancy finding yet.\n"
                    + "Watch here for switchover, GI-after-switch, timeout, and link-fault verdicts.";
                return;
            }

            RedundancyFindingDetailsText = string.Join(
                Environment.NewLine + Environment.NewLine,
                redundancyFindings.Select(f =>
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "[{0}] {1}{2}",
                        string.IsNullOrWhiteSpace(f.Severity) ? "-" : f.Severity.ToUpperInvariant(),
                        string.IsNullOrWhiteSpace(f.RuleCode) ? (f.Title ?? "Finding") : f.RuleCode,
                        string.IsNullOrWhiteSpace(f.Detail) ? string.Empty : Environment.NewLine + f.Detail)));
        }

        public void MarkFindingsViewed()
        {
            HasUnreadFindings = false;
            RefreshAvailabilityTelemetry();
        }

        private void NormalizeLine(LineMonitorRow e)
        {
            if (!string.IsNullOrWhiteSpace(e.Detail) && string.IsNullOrWhiteSpace(e.RawHex))
            {
                e.RawHex = e.Detail;
            }

            e.RawHex = e.RawHex ?? string.Empty;
            e.Detail = e.Detail ?? string.Empty;
            e.Summary = e.Summary ?? string.Empty;
            e.ControlFc = e.ControlFc ?? "-";
            e.ACD = e.ACD ?? "-";
            e.DFC = e.DFC ?? "-";
            e.AsduType = e.AsduType ?? "-";
            e.COT = e.COT ?? "-";
            e.CASDU = e.CASDU ?? "-";
            e.FrameType = e.FrameType ?? "Info";
            e.Direction = e.Direction ?? "-";
            e.Time = string.IsNullOrWhiteSpace(e.Time) ? DateTime.Now.ToString("HH:mm:ss.fff") : e.Time;
            e.DataClass = string.IsNullOrWhiteSpace(e.DataClass) ? "-" : e.DataClass;
        }

        private void RefreshCommands()
        {
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
            SendGeneralInterrogationCommand.RaiseCanExecuteChanged();
            SendClockSyncCommand.RaiseCanExecuteChanged();
            SendSingleOnCommand.RaiseCanExecuteChanged();
            SendSingleOffCommand.RaiseCanExecuteChanged();
            SendSingleSelectOnCommand.RaiseCanExecuteChanged();
            SendSingleSelectOffCommand.RaiseCanExecuteChanged();
            SendDoubleOpenCommand.RaiseCanExecuteChanged();
            SendDoubleCloseCommand.RaiseCanExecuteChanged();
            SendDoubleSelectOpenCommand.RaiseCanExecuteChanged();
            SendDoubleSelectCloseCommand.RaiseCanExecuteChanged();
            SendRaiseCommand.RaiseCanExecuteChanged();
            SendLowerCommand.RaiseCanExecuteChanged();
            SendSelectRaiseCommand.RaiseCanExecuteChanged();
            SendSelectLowerCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanEditSettings));
            OnPropertyChanged(nameof(CanSendCommands));
        }

        private void RunOnUi(Action action)
        {
            if (action == null)
            {
                return;
            }

            Application currentApplication = Application.Current;
            if (currentApplication == null || currentApplication.Dispatcher == null)
            {
                action();
                return;
            }

            if (currentApplication.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                currentApplication.Dispatcher.BeginInvoke(action);
            }
        }

        private static bool ShouldOverwriteMetadata(string currentCot, string incomingCot)
        {
            if (string.IsNullOrWhiteSpace(incomingCot))
            {
                return false;
            }

            if (!string.Equals(incomingCot, "GI", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(currentCot)
                || currentCot == "-"
                || string.Equals(currentCot, "GI", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsDiscreteType(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && (type.IndexOf("Single Point", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Double Point", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetSignalCommandFamily(ValueViewerRow row)
        {
            if (row == null)
            {
                return null;
            }

            string family = GetSignalCommandFamily(row.Type);
            if (family != null)
            {
                return family;
            }

            int? relatedCommandIoa = OfficialPointProfiles.TryGetRelatedCommandIoa(row.IOA);
            PointDefinition commandPoint;
            if (relatedCommandIoa.HasValue
                && OfficialPointProfiles.TryGetPointByIoa(relatedCommandIoa.Value, out commandPoint)
                && commandPoint != null)
            {
                return GetSignalCommandFamilyForTypeId(commandPoint.TypeId);
            }

            PointDefinition point;
            if (OfficialPointProfiles.TryGetPointByIoa(row.IOA, out point) && point != null)
            {
                return GetSignalCommandFamilyForTypeId(point.TypeId);
            }

            return null;
        }

        private static string GetSignalCommandFamily(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return null;

            if (type.IndexOf("Double Point", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Double";

            if (type.IndexOf("Single Point", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Single";

            if (type.IndexOf("Step", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Regulating";

            return null;
        }

        private static string GetSignalCommandFamilyForTypeId(int typeId)
        {
            switch (typeId)
            {
                case 45:
                    return "Single";
                case 46:
                    return "Double";
                case 47:
                    return "Regulating";
                case 48:
                    return "Setpoint";
                default:
                    return null;
            }
        }
        private static bool IsMeteringType(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && (type.IndexOf("Measured", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Integrated", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string NormalizeTimestamp(string timestamp)
        {
            return string.IsNullOrWhiteSpace(timestamp) || timestamp == "-"
                ? "-"
                : timestamp;
        }

        private static string MergeTimestamp(string currentTimestamp, string newTimestamp)
        {
            if (!string.IsNullOrWhiteSpace(newTimestamp) && newTimestamp != "-")
            {
                return newTimestamp;
            }

            return string.IsNullOrWhiteSpace(currentTimestamp) ? "-" : currentTimestamp;
        }

        private bool ShouldCreateConnectionEvent(string newStatus)
        {
            if (string.IsNullOrWhiteSpace(newStatus))
            {
                return false;
            }

            if (string.Equals(_lastConnectionEvent, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(newStatus, "Connected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(newStatus, "Disconnected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(newStatus, "Faulted", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildProfileSummary(ConnectionSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            return string.Format("{0} | Link {1} | CA {2} | GI {3} | Clock {4}", settings.SerialSummary, settings.LinkAddress, settings.CasduAddress, settings.UseGeneralInterrogationOnConnect ? "On" : "Off", settings.UseClockSyncOnConnect ? "On" : "Off");
        }

        private static string GetLevelForConnectionStatus(string status)
        {
            switch (status)
            {
                case "Faulted":
                    return "Error";
                case "Disconnected":
                case "Disconnecting":
                    return "Warning";
                default:
                    return "Info";
            }
        }

        private static string ToStatusLevel(string frameType)
        {
            if (string.Equals(frameType, "Warning", StringComparison.OrdinalIgnoreCase))
            {
                return "Warning";
            }

            if (string.Equals(frameType, "Error", StringComparison.OrdinalIgnoreCase))
            {
                return "Error";
            }

            return "Info";
        }
    }
}






