using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using IecSlaveSimulator.Models;
using IecSlaveSimulator.Services;

namespace IecSlaveSimulator.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly JsonProjectStore _projectStore;
        private readonly DispatcherTimer _runtimeTimer;
        private readonly Dispatcher _uiDispatcher;
        private readonly NucSlaveController _nucSlaveController;
        private Iec101SlaveService _slaveService;
        private NucDualLinkSlaveHost _nucDualLinkHost;
        private string _projectName;
        private int _commonAddress;
        private int _linkAddress;
        private string _projectNotes;
        private string _statusText;
        private string _currentFilePath;
        private bool _isRuntime;
        private SignalDefinition _selectedEditSignal;
        private SignalDefinition _selectedRuntimeSignal;
        private string _selectedComPort;
        private int _baudRate;
        private string _parity;
        private int _dataBits;
        private string _stopBits;
        private string _linkMode;
        private int _class1Queue;
        private bool _enableMeasurementStreaming;
        private bool _isConnectionSetupExpanded;
        private bool _isTxActive;
        private bool _isRxActive;

        public MainViewModel()
        {
            _projectStore = new JsonProjectStore();
            _uiDispatcher = Application.Current != null ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher;
            _runtimeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _runtimeTimer.Tick += RuntimeTimer_Tick;
            _nucSlaveController = new NucSlaveController();

            EditableSignals = new ObservableCollection<SignalDefinition>();
            RuntimeSignals = new ObservableCollection<SignalDefinition>();
            RuntimeLog = new ObservableCollection<RuntimeLogEntry>();
            StatusHistory = new ObservableCollection<RuntimeLogEntry>();
            LinkActivity = new ObservableCollection<RuntimeLogEntry>();
            AvailableComPorts = new ObservableCollection<string>();

            SignalTypeOptions = Enum.GetValues(typeof(SlaveSignalType)).Cast<SlaveSignalType>().ToArray();
            SignalClassOptions = Enum.GetValues(typeof(SignalClass)).Cast<SignalClass>().ToArray();
            PublishModeOptions = Enum.GetValues(typeof(SignalPublishMode)).Cast<SignalPublishMode>().ToArray();
            AnalogAnimationOptions = Enum.GetValues(typeof(AnalogAnimationKind)).Cast<AnalogAnimationKind>().ToArray();
            DiscreteAnimationOptions = Enum.GetValues(typeof(DiscreteAnimationKind)).Cast<DiscreteAnimationKind>().ToArray();
            CommandSemanticOptions = Enum.GetValues(typeof(CommandSemantic)).Cast<CommandSemantic>().ToArray();
            CommandBindingModeOptions = Enum.GetValues(typeof(CommandBindingMode)).Cast<CommandBindingMode>().ToArray();
            LiveCotOptions = new[] { "BgScan", "Spont", "GI", "CmdFb" };
            BaudRateOptions = new[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
            ParityOptions = new[] { "None", "Even", "Odd" };
            DataBitsOptions = new[] { 7, 8 };
            StopBitsOptions = new[] { "One", "Two" };
            LinkModeOptions = new[] { "Unbalanced", "Balanced" };

            AddSignalCommand = new RelayCommand(AddSignal, () => !IsRuntime);
            RemoveSignalCommand = new RelayCommand(RemoveSelectedSignal, () => !IsRuntime && SelectedEditSignal != null);
            NewProjectCommand = new RelayCommand(NewProject, () => !IsRuntime);
            RunCommand = new RelayCommand(StartRuntime, () => !IsRuntime && EditableSignals.Count > 0);
            StopCommand = new RelayCommand(StopRuntime, () => IsRuntime);
            RefreshPortsCommand = new RelayCommand(RefreshAvailableComPorts);
            SimulateOpenCommand = new RelayCommand(() => ApplySelectedCommand(CommandIntent.Open), CanSimulateSelectedCommand);
            SimulateCloseCommand = new RelayCommand(() => ApplySelectedCommand(CommandIntent.Close), CanSimulateSelectedCommand);
            SimulateOnCommand = new RelayCommand(() => ApplySelectedCommand(CommandIntent.On), CanSimulateSelectedCommand);
            SimulateOffCommand = new RelayCommand(() => ApplySelectedCommand(CommandIntent.Off), CanSimulateSelectedCommand);
            InjectBufferBurstCommand = new RelayCommand(InjectBufferBurst, () => IsRuntime && _nucDualLinkHost != null);
            ToggleNucLinkACommand = new RelayCommand(ToggleNucLinkA, () => IsRuntime && _nucDualLinkHost != null);
            ToggleNucLinkBCommand = new RelayCommand(ToggleNucLinkB, () => IsRuntime && _nucDualLinkHost != null);

            RefreshAvailableComPorts();
            BaudRate = 9600;
            Parity = "Even";
            DataBits = 8;
            StopBits = "One";
            LinkMode = "Unbalanced";
            Class1Queue = 50;
            EnableMeasurementStreaming = true;
            IsConnectionSetupExpanded = true;

            ApplyProject(SlaveProjectDefinition.CreateDefault());
            CurrentFilePath = _projectStore.GetDefaultPath();
            StatusText = "Project initialized. Edit the signal database, then switch to runtime.";
        }

        public ObservableCollection<SignalDefinition> EditableSignals { get; }
        public ObservableCollection<SignalDefinition> RuntimeSignals { get; }
        public ObservableCollection<RuntimeLogEntry> RuntimeLog { get; }
        public ObservableCollection<RuntimeLogEntry> StatusHistory { get; }
        public ObservableCollection<RuntimeLogEntry> LinkActivity { get; }
        public ObservableCollection<string> AvailableComPorts { get; }

        public SlaveSignalType[] SignalTypeOptions { get; }
        public SignalClass[] SignalClassOptions { get; }
        public SignalPublishMode[] PublishModeOptions { get; }
        public AnalogAnimationKind[] AnalogAnimationOptions { get; }
        public DiscreteAnimationKind[] DiscreteAnimationOptions { get; }
        public CommandSemantic[] CommandSemanticOptions { get; }
        public CommandBindingMode[] CommandBindingModeOptions { get; }
        public string[] LiveCotOptions { get; }
        public int[] BaudRateOptions { get; }
        public string[] ParityOptions { get; }
        public int[] DataBitsOptions { get; }
        public string[] StopBitsOptions { get; }
        public string[] LinkModeOptions { get; }

        public RelayCommand AddSignalCommand { get; }
        public RelayCommand RemoveSignalCommand { get; }
        public RelayCommand NewProjectCommand { get; }
        public RelayCommand RunCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand RefreshPortsCommand { get; }
        public RelayCommand SimulateOpenCommand { get; }
        public RelayCommand SimulateCloseCommand { get; }
        public RelayCommand SimulateOnCommand { get; }
        public RelayCommand SimulateOffCommand { get; }
        public RelayCommand InjectBufferBurstCommand { get; }
        public RelayCommand ToggleNucLinkACommand { get; }
        public RelayCommand ToggleNucLinkBCommand { get; }

        public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }
        public int CommonAddress { get => _commonAddress; set => SetProperty(ref _commonAddress, value); }
        public int LinkAddress { get => _linkAddress; set => SetProperty(ref _linkAddress, value); }
        public string ProjectNotes { get => _projectNotes; set => SetProperty(ref _projectNotes, value); }
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public string CurrentFilePath { get => _currentFilePath; set => SetProperty(ref _currentFilePath, value); }
        public string SelectedComPort { get => _selectedComPort; set => SetProperty(ref _selectedComPort, value); }
        public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }
        public string Parity { get => _parity; set => SetProperty(ref _parity, value); }
        public int DataBits { get => _dataBits; set => SetProperty(ref _dataBits, value); }
        public string StopBits { get => _stopBits; set => SetProperty(ref _stopBits, value); }
        public string LinkMode { get => _linkMode; set => SetProperty(ref _linkMode, value); }
        public int Class1Queue { get => _class1Queue; set => SetProperty(ref _class1Queue, value); }
        public bool EnableMeasurementStreaming { get => _enableMeasurementStreaming; set => SetProperty(ref _enableMeasurementStreaming, value); }
        public bool IsConnectionSetupExpanded { get => _isConnectionSetupExpanded; set => SetProperty(ref _isConnectionSetupExpanded, value); }
        public string ConnectionSummary => _nucSlaveController.Settings.OperatingMode == SlaveOperatingMode.NucDualLink
            ? string.Format("{0} | A:{1} | B:{2} | Link:{3} | CA {4}", LinkMode ?? "Unbalanced", SelectedComPort ?? "-", _nucSlaveController.Settings.BackupPortName ?? "-", LinkAddress, CommonAddress)
            : string.Format("{0}, {1} bps, {2}, LinkLen {3}, Link {4}, CA {5}, IOA {6}", SelectedComPort ?? "-", BaudRate, LinkMode ?? "Unbalanced", 2, LinkAddress, CommonAddress, 3);
        public bool IsNucDualLinkMode => _nucSlaveController.Settings.OperatingMode == SlaveOperatingMode.NucDualLink;
        public string NucRuntimeSummary => !IsNucDualLinkMode
            ? "Single-link runtime."
            : string.Format("NUC dual-link. A={0} | B={1} | Shared Link Address={2} | Active={3}", string.IsNullOrWhiteSpace(_nucSlaveController.Settings.PrimaryPortName) ? (SelectedComPort ?? "-") : _nucSlaveController.Settings.PrimaryPortName, _nucSlaveController.Settings.BackupPortName ?? "-", _nucSlaveController.Settings.PrimaryLinkAddress, _nucSlaveController.ActiveEndpoint);
        public string NucLink1StatusText => BuildNucLinkStatusText("A", _nucSlaveController.LinkA);
        public string NucLink2StatusText => BuildNucLinkStatusText("B", _nucSlaveController.LinkB);
        public string NucBufferStatusText => string.Format("Shared buffer={0} | Inject mode={1} | Target={2} signals", _nucSlaveController.Settings.ShareEventBufferAcrossLinks ? "ON" : "OFF", _nucSlaveController.Settings.BufferInjectionMode, _nucSlaveController.Settings.BufferInjectionSignalCount);
        public string NucLinkAActionText => _nucSlaveController.LinkA != null && _nucSlaveController.LinkA.IsConnected ? "Disconnect A" : "Reconnect A";
        public string NucLinkBActionText => _nucSlaveController.LinkB != null && _nucSlaveController.LinkB.IsConnected ? "Disconnect B" : "Reconnect B";
        public bool ShowEditChrome => !IsRuntime;
        public bool ShowRuntimeChrome => IsRuntime;
        public bool IsTxActive { get => _isTxActive; private set => SetProperty(ref _isTxActive, value); }
        public bool IsRxActive { get => _isRxActive; private set => SetProperty(ref _isRxActive, value); }
        public NucSlaveSettings CurrentNucSettings => _nucSlaveController.Settings;

        public bool IsRuntime
        {
            get => _isRuntime;
            private set
            {
                if (SetProperty(ref _isRuntime, value))
                {
                    RaisePropertyChanged(nameof(IsEditMode));
                    RaisePropertyChanged(nameof(ShowEditChrome));
                    RaisePropertyChanged(nameof(ShowRuntimeChrome));
                    RaisePropertyChanged(nameof(ConnectionSummary));
                    RaiseNucRuntimeProperties();
                    RefreshCommands();
                }
            }
        }

        public bool IsEditMode => !IsRuntime;

        public SignalDefinition SelectedEditSignal
        {
            get => _selectedEditSignal;
            set
            {
                if (SetProperty(ref _selectedEditSignal, value))
                    RefreshCommands();
            }
        }

        public SignalDefinition SelectedRuntimeSignal
        {
            get => _selectedRuntimeSignal;
            set
            {
                if (SetProperty(ref _selectedRuntimeSignal, value))
                    RefreshCommands();
            }
        }


        public void ApplyConnectionSettings(SlaveConnectionSettings settings)
        {
            if (settings == null)
                return;

            SelectedComPort = settings.SerialPort;
            BaudRate = settings.BaudRate;
            DataBits = settings.DataBits;
            Parity = settings.Parity;
            StopBits = settings.StopBits;
            LinkMode = settings.LinkLayerMode;
            CommonAddress = settings.CommonAddress;
            LinkAddress = settings.LinkAddress;
            Class1Queue = settings.Class1QueueSize;
            EnableMeasurementStreaming = settings.EnableMeasurementStreaming;
            _nucSlaveController.LoadProject(EditableSignals.Select(CloneForPersistence).ToList(), new NucSlaveSettings
            {
                OperatingMode = settings.OperatingMode,
                PrimaryPortName = settings.SerialPort,
                BackupPortName = settings.BackupSerialPort,
                PrimaryLinkAddress = settings.LinkAddress,
                BackupLinkAddress = settings.BackupLinkAddress > 0 ? settings.BackupLinkAddress : settings.LinkAddress,
                EmitGatewayBaselineOnStart = settings.EmitGatewayBaselineOnStart,
                ShareEventBufferAcrossLinks = settings.ShareEventBufferAcrossLinks,
                BufferInjectionMode = settings.BufferInjectionMode,
                BufferInjectionSignalCount = settings.BufferInjectionSignalCount,
                BufferInjectionBurstSize = settings.BufferInjectionBurstSize,
                BufferInjectionIntervalMs = settings.BufferInjectionIntervalMs
            });
            RaisePropertyChanged(nameof(ConnectionSummary));
            RaiseNucRuntimeProperties();
        }

        public SlaveConnectionSettings BuildConnectionSettings()
        {
            return new SlaveConnectionSettings
            {
                SerialPort = SelectedComPort,
                BaudRate = BaudRate,
                DataBits = DataBits,
                Parity = Parity,
                StopBits = StopBits,
                LinkLayerMode = LinkMode,
                LinkAddressLength = 2,
                LinkAddress = LinkAddress,
                CasduLength = 2,
                CommonAddress = CommonAddress,
                IoaLength = 3,
                OriginatorAddress = 0,
                ResponseTimeoutMs = 300,
                BackgroundPublishIntervalMs = 500,
                RunLoopDelayMs = 20,
                Class1QueueSize = Class1Queue,
                EnableMeasurementStreaming = EnableMeasurementStreaming,
                OperatingMode = _nucSlaveController.Settings.OperatingMode,
                BackupSerialPort = _nucSlaveController.Settings.BackupPortName,
                BackupLinkAddress = _nucSlaveController.Settings.BackupLinkAddress > 0 ? _nucSlaveController.Settings.BackupLinkAddress : _nucSlaveController.Settings.PrimaryLinkAddress,
                EmitGatewayBaselineOnStart = _nucSlaveController.Settings.EmitGatewayBaselineOnStart,
                ShareEventBufferAcrossLinks = _nucSlaveController.Settings.ShareEventBufferAcrossLinks,
                BufferInjectionMode = _nucSlaveController.Settings.BufferInjectionMode,
                BufferInjectionSignalCount = _nucSlaveController.Settings.BufferInjectionSignalCount,
                BufferInjectionBurstSize = _nucSlaveController.Settings.BufferInjectionBurstSize,
                BufferInjectionIntervalMs = _nucSlaveController.Settings.BufferInjectionIntervalMs
            };
        }
        public async Task LoadProjectAsync(string filePath)
        {
            SlaveProjectDefinition project = await _projectStore.LoadAsync(filePath).ConfigureAwait(true);
            ApplyProject(project);
            CurrentFilePath = filePath;
            StatusText = "Project loaded from JSON.";
            AddStatus("CFG", "Loaded project database from " + filePath);
        }

        public async Task SaveProjectAsync(string filePath)
        {
            SlaveProjectDefinition project = BuildProjectDefinition();
            await _projectStore.SaveAsync(filePath, project).ConfigureAwait(true);
            CurrentFilePath = filePath;
            StatusText = "Project saved.";
            AddStatus("CFG", "Saved project database to " + filePath);
        }

        private void NewProject()
        {
            ApplyProject(SlaveProjectDefinition.CreateDefault());
            StatusText = "New project created.";
            AddStatus("CFG", "New project scaffold created.");
        }

        private void AddSignal()
        {
            SignalDefinition signal = new SignalDefinition
            {
                Ioa = NextIoa(),
                Label = "Signal " + (EditableSignals.Count + 1)
            };

            EditableSignals.Add(signal);
            SelectedEditSignal = signal;
            StatusText = "Signal row added.";
            AddStatus("CFG", "Signal row added to database.");
        }

        private void RemoveSelectedSignal()
        {
            if (SelectedEditSignal == null)
                return;

            EditableSignals.Remove(SelectedEditSignal);
            SelectedEditSignal = null;
            StatusText = "Signal row removed.";
            AddStatus("CFG", "Signal row removed from database.");
        }

        private void StartRuntime()
        {
            RuntimeSignals.Clear();
            RuntimeLog.Clear();
            StatusHistory.Clear();
            LinkActivity.Clear();

            foreach (SignalDefinition signal in EditableSignals)
                RuntimeSignals.Add(signal.CloneForRuntime());

            try
            {
                SlaveProjectDefinition project = BuildProjectDefinition();
                _nucSlaveController.LoadProject(RuntimeSignals.Select(CloneForPersistence).ToList(), project.NucSettings);

                if (project.NucSettings != null
                    && project.NucSettings.OperatingMode == SlaveOperatingMode.NucDualLink
                    && !string.IsNullOrWhiteSpace(project.NucSettings.BackupPortName))
                {
                    AttachNucDualLinkHost();
                    _nucDualLinkHost.Start(
                        new SlaveRuntimeConfig
                        {
                            PortName = string.IsNullOrWhiteSpace(project.NucSettings.PrimaryPortName) ? SelectedComPort : project.NucSettings.PrimaryPortName,
                            BaudRate = BaudRate,
                            Parity = Parity,
                            DataBits = DataBits,
                            StopBits = StopBits,
                            CommonAddress = CommonAddress,
                            LinkAddress = project.NucSettings.PrimaryLinkAddress > 0 ? project.NucSettings.PrimaryLinkAddress : LinkAddress,
                            Class1QueueSize = Class1Queue,
                            RunLoopDelayMs = 20,
                            ResponseTimeoutMs = 300,
                            BackgroundPublishIntervalMs = 500,
                            EnableMeasurementStreaming = EnableMeasurementStreaming
                        },
                        new SlaveRuntimeConfig
                        {
                            PortName = project.NucSettings.BackupPortName,
                            BaudRate = BaudRate,
                            Parity = Parity,
                            DataBits = DataBits,
                            StopBits = StopBits,
                            CommonAddress = CommonAddress,
                            LinkAddress = project.NucSettings.BackupLinkAddress > 0 ? project.NucSettings.BackupLinkAddress : LinkAddress,
                            Class1QueueSize = Class1Queue,
                            RunLoopDelayMs = 20,
                            ResponseTimeoutMs = 300,
                            BackgroundPublishIntervalMs = 500,
                            EnableMeasurementStreaming = EnableMeasurementStreaming
                        },
                        RuntimeSignals.Select(CloneForPersistence).ToList());
                }
                else
                {
                    AttachSlaveService();
                    _slaveService.Start(new SlaveRuntimeConfig
                    {
                        PortName = SelectedComPort,
                        BaudRate = BaudRate,
                        Parity = Parity,
                        DataBits = DataBits,
                        StopBits = StopBits,
                        CommonAddress = CommonAddress,
                        LinkAddress = LinkAddress,
                        Class1QueueSize = Class1Queue,
                        RunLoopDelayMs = 20,
                        ResponseTimeoutMs = 300,
                        BackgroundPublishIntervalMs = 500,
                        EnableMeasurementStreaming = EnableMeasurementStreaming
                    }, RuntimeSignals.Select(CloneForPersistence).ToList());

                    _nucSlaveController.MarkLinkConnected(1);
                }

                _runtimeTimer.Start();
                IsRuntime = true;
                IsConnectionSetupExpanded = false;
                StatusText = "Runtime mode active. Compact operator view enabled.";
                AddStatus("RUN", string.Format("Runtime started. Port={0}, Baud={1}, CA={2}, Link={3}, Signals={4}", SelectedComPort ?? "-", BaudRate, CommonAddress, LinkAddress, RuntimeSignals.Count));
                AddStatus("NUC", string.Format("Slave NUC foundation ready. Mode={0}, shared buffer={1}, burst scaffold={2} signals.", _nucSlaveController.Settings.OperatingMode, _nucSlaveController.Settings.ShareEventBufferAcrossLinks ? "ON" : "OFF", _nucSlaveController.Settings.BufferInjectionSignalCount));
                RaiseNucRuntimeProperties();
            }
            catch (Exception ex)
            {
                RuntimeSignals.Clear();
                if (_slaveService != null)
                {
                    _slaveService.Stop();
                    _slaveService.Dispose();
                    _slaveService = null;
                }

                StatusText = "Runtime start failed.";
                AddStatus("ERR", "Failed to start slave runtime: " + ex.Message);
            }
        }

        private void StopRuntime()
        {
            _runtimeTimer.Stop();
            if (_slaveService != null)
            {
                _slaveService.Stop();
                _slaveService.Dispose();
                _slaveService = null;
            }

            if (_nucDualLinkHost != null)
            {
                _nucDualLinkHost.Stop();
                _nucDualLinkHost.Dispose();
                _nucDualLinkHost = null;
            }

            _nucSlaveController.MarkLinkDisconnected(1);
            _nucSlaveController.MarkLinkDisconnected(2);

            RuntimeSignals.Clear();
            IsRuntime = false;
            IsConnectionSetupExpanded = true;
            StatusText = "Runtime stopped. Edit mode restored.";
            AddStatus("RUN", "Runtime stopped and edit mode restored.");
            RaiseNucRuntimeProperties();
        }

        private void RuntimeTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            foreach (SignalDefinition signal in RuntimeSignals)
            {
                if (signal.TryAdvanceAnimation(now))
                {
                    AddStatus("ANIM", string.Format("IOA {0} -> {1} ({2})", signal.Ioa, signal.RuntimeValue, signal.LiveCot));
                    if (_slaveService != null)
                        _slaveService.UpdateSignal(CloneForPersistence(signal));
                    if (_nucDualLinkHost != null)
                        _nucDualLinkHost.UpdateSignal(CloneForPersistence(signal));
                }
            }

            RaiseNucRuntimeProperties();
        }

        private void ApplySelectedCommand(CommandIntent intent)
        {
            SignalDefinition command = SelectedRuntimeSignal;
            if (command == null || !command.IsCommand)
                return;

            SignalDefinition target = RuntimeSignals.FirstOrDefault(item => item.Ioa == command.LinkedStatusIoa);
            if (target == null)
            {
                AddStatus("CMD", string.Format("No linked status IOA for command IOA {0}.", command.Ioa));
                return;
            }

            target.ApplyBoundCommand(intent);
            AddStatus("CMD", string.Format("Command {0} on IOA {1} updated status IOA {2} -> {3} ({4})", intent, command.Ioa, target.Ioa, target.RuntimeValue, target.LiveCot));

            if (_slaveService != null)
                _slaveService.UpdateSignal(CloneForPersistence(target));
            if (_nucDualLinkHost != null)
                _nucDualLinkHost.UpdateSignal(CloneForPersistence(target));
        }

        private void InjectBufferBurst()
        {
            if (_nucDualLinkHost == null)
            {
                AddStatus("NUC", "Buffer injection is only available in NUC dual-link runtime.");
                return;
            }

            int injected = _nucDualLinkHost.InjectBufferBurst();
            AddStatus("NUC", string.Format("Injected {0} buffer event signal(s) into shared NUC store.", injected));
        }

        private void ToggleNucLinkA()
        {
            if (_nucDualLinkHost == null)
                return;

            if (_nucSlaveController.LinkA != null && _nucSlaveController.LinkA.IsConnected)
                _nucDualLinkHost.DisconnectLink(1);
            else
                _nucDualLinkHost.ReconnectLink(1);

            RaiseNucRuntimeProperties();
        }

        private void ToggleNucLinkB()
        {
            if (_nucDualLinkHost == null)
                return;

            if (_nucSlaveController.LinkB != null && _nucSlaveController.LinkB.IsConnected)
                _nucDualLinkHost.DisconnectLink(2);
            else
                _nucDualLinkHost.ReconnectLink(2);

            RaiseNucRuntimeProperties();
        }

        private bool CanSimulateSelectedCommand()
        {
            return IsRuntime && SelectedRuntimeSignal != null && SelectedRuntimeSignal.IsCommand;
        }

        private void ApplyProject(SlaveProjectDefinition project)
        {
            if (project == null)
                project = SlaveProjectDefinition.CreateDefault();

            EditableSignals.Clear();
            ProjectName = project.ProjectName;
            CommonAddress = project.CommonAddress;
            LinkAddress = project.LinkAddress;
            ProjectNotes = project.Notes;
            _nucSlaveController.LoadProject(project.Signals ?? new List<SignalDefinition>(), project.NucSettings ?? NucSlaveSettings.CreateDefault());

            foreach (SignalDefinition signal in project.Signals ?? new List<SignalDefinition>())
                EditableSignals.Add(signal);

            if (EditableSignals.Count == 0)
                AddSignal();

            SelectedEditSignal = EditableSignals.FirstOrDefault();
            RefreshCommands();
        }

        private SlaveProjectDefinition BuildProjectDefinition()
        {
            return new SlaveProjectDefinition
            {
                ProjectName = ProjectName,
                CommonAddress = CommonAddress,
                LinkAddress = LinkAddress,
                Notes = ProjectNotes,
                Signals = EditableSignals.Select(CloneForPersistence).ToList(),
                NucSettings = _nucSlaveController.Settings
            };
        }

        private static SignalDefinition CloneForPersistence(SignalDefinition source)
        {
            return new SignalDefinition
            {
                IsEnabled = source.IsEnabled,
                Ioa = source.Ioa,
                Label = source.Label,
                SignalType = source.SignalType,
                Casdu = source.Casdu,
                SignalClass = source.SignalClass,
                PublishMode = source.PublishMode,
                BackgroundEnabled = source.BackgroundEnabled,
                SpontaneousEnabled = source.SpontaneousEnabled,
                UseTimestamp = source.UseTimestamp,
                Quality = source.Quality,
                DefaultValue = source.DefaultValue,
                RuntimeValue = source.RuntimeValue,
                LiveCot = source.LiveCot,
                LinkedStatusIoa = source.LinkedStatusIoa,
                CommandSemantic = source.CommandSemantic,
                CommandBindingMode = source.CommandBindingMode,
                CommandOperateMode = source.CommandOperateMode,
                CommandDelayMs = source.CommandDelayMs,
                AnalogAnimation = source.AnalogAnimation,
                AnalogFrom = source.AnalogFrom,
                AnalogTo = source.AnalogTo,
                AnalogStep = source.AnalogStep,
                AnimationIntervalMs = source.AnimationIntervalMs,
                AnalogPingPong = source.AnalogPingPong,
                DiscreteAnimation = source.DiscreteAnimation,
                ToggleIntervalSeconds = source.ToggleIntervalSeconds,
                Notes = source.Notes
            };
        }

        private int NextIoa()
        {
            return EditableSignals.Count == 0 ? 1 : EditableSignals.Max(item => item.Ioa) + 1;
        }

        private void RefreshAvailableComPorts()
        {
            string current = SelectedComPort;
            AvailableComPorts.Clear();
            foreach (string port in SerialPort.GetPortNames().OrderBy(name => name))
                AvailableComPorts.Add(port);

            SelectedComPort = AvailableComPorts.Contains(current) ? current : AvailableComPorts.FirstOrDefault() ?? "COM1";
            AddStatus("CFG", "COM port list refreshed.");
        }

        private void AddStatus(string category, string message)
        {
            InvokeOnUi(() =>
            {
                RuntimeLog.Insert(0, CreateLogEntry(category, message));
                StatusHistory.Insert(0, CreateLogEntry(category, message));
                Trim(RuntimeLog, 300);
                Trim(StatusHistory, 200);
                RaiseNucRuntimeProperties();
            });
        }

        private void AddLink(string category, string message)
        {
            InvokeOnUi(() =>
            {
                LinkActivity.Insert(0, CreateLogEntry(category, message));
                Trim(LinkActivity, 200);

                if (string.Equals(category, "TX", StringComparison.OrdinalIgnoreCase) || category.EndsWith(":TX", StringComparison.OrdinalIgnoreCase))
                    PulseTraffic(true);
                else if (string.Equals(category, "RX", StringComparison.OrdinalIgnoreCase) || category.EndsWith(":RX", StringComparison.OrdinalIgnoreCase))
                    PulseTraffic(false);

                RaiseNucRuntimeProperties();
            });
        }

        private RuntimeLogEntry CreateLogEntry(string category, string message)
        {
            return new RuntimeLogEntry
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Category = category,
                Message = message
            };
        }

        private void PulseTraffic(bool isTx)
        {
            if (isTx)
            {
                IsTxActive = true;
                Task.Delay(220).ContinueWith(_ => InvokeOnUi(() => IsTxActive = false));
            }
            else
            {
                IsRxActive = true;
                Task.Delay(220).ContinueWith(_ => InvokeOnUi(() => IsRxActive = false));
            }
        }

        private void AttachSlaveService()
        {
            if (_slaveService != null)
            {
                _slaveService.Stop();
                _slaveService.Dispose();
            }

            _slaveService = new Iec101SlaveService();
            _slaveService.StatusLogged = AddStatus;
            _slaveService.LinkActivityLogged = AddLink;
            _slaveService.RuntimeSignalUpdated = HandleRuntimeSignalUpdated;
        }

        private void AttachNucDualLinkHost()
        {
            if (_nucDualLinkHost != null)
            {
                _nucDualLinkHost.Stop();
                _nucDualLinkHost.Dispose();
            }

            _nucDualLinkHost = new NucDualLinkSlaveHost(_nucSlaveController);
            _nucDualLinkHost.StatusLogged = AddStatus;
            _nucDualLinkHost.LinkActivityLogged = AddLink;
            _nucDualLinkHost.RuntimeSignalUpdated = HandleRuntimeSignalUpdated;
        }

        private void HandleRuntimeSignalUpdated(int ioa, string runtimeValue, string liveCot)
        {
            InvokeOnUi(() =>
            {
                SignalDefinition signal = RuntimeSignals.FirstOrDefault(item => item.Ioa == ioa);
                if (signal == null)
                    return;

                signal.RuntimeValue = runtimeValue;
                signal.LiveCot = liveCot;
                AddStatus("FDBK", string.Format("IOA {0} updated from master command -> {1} ({2})", ioa, runtimeValue, liveCot));
                RaiseNucRuntimeProperties();
            });
        }

        private void RaiseNucRuntimeProperties()
        {
            RaisePropertyChanged(nameof(IsNucDualLinkMode));
            RaisePropertyChanged(nameof(NucRuntimeSummary));
            RaisePropertyChanged(nameof(NucLink1StatusText));
            RaisePropertyChanged(nameof(NucLink2StatusText));
            RaisePropertyChanged(nameof(NucBufferStatusText));
            RaisePropertyChanged(nameof(NucLinkAActionText));
            RaisePropertyChanged(nameof(NucLinkBActionText));
        }

        private static string BuildNucLinkStatusText(string label, NucPortEndpointState endpoint)
        {
            string port = endpoint == null || string.IsNullOrWhiteSpace(endpoint.PortName) ? "-" : endpoint.PortName;
            string lastRx = endpoint != null && endpoint.LastRxUtc.HasValue
                ? string.Format("{0:0.0}s ago", Math.Max(0d, (DateTime.UtcNow - endpoint.LastRxUtc.Value).TotalSeconds))
                : "no RX yet";
            if (endpoint == null)
            {
                return string.Format("Link {0}: COM={1} | no endpoint state", label, port);
            }

            return string.Format("Link {0}: {1} | COM={2} | TX={3} RX={4} | Last RX={5} | Role={6} | State={7}",
                label,
                endpoint.LinkAddress,
                port,
                endpoint.TxCount,
                endpoint.RxCount,
                lastRx,
                endpoint.Role,
                endpoint.State);
        }

        public void ApplyRuntimeSignalChanges(SignalDefinition updatedSignal)
        {
            if (updatedSignal == null)
                return;

            SignalDefinition runtimeSignal = RuntimeSignals.FirstOrDefault(item => item.Ioa == updatedSignal.Ioa);
            if (runtimeSignal == null)
                return;

            runtimeSignal.IsEnabled = updatedSignal.IsEnabled;
            runtimeSignal.RuntimeValue = updatedSignal.RuntimeValue;
            runtimeSignal.LiveCot = updatedSignal.LiveCot;
            runtimeSignal.Quality = updatedSignal.Quality;
            runtimeSignal.AnalogAnimation = updatedSignal.AnalogAnimation;
            runtimeSignal.AnalogFrom = updatedSignal.AnalogFrom;
            runtimeSignal.AnalogTo = updatedSignal.AnalogTo;
            runtimeSignal.AnalogStep = updatedSignal.AnalogStep;
            runtimeSignal.AnimationIntervalMs = updatedSignal.AnimationIntervalMs;
            runtimeSignal.AnalogPingPong = updatedSignal.AnalogPingPong;
            runtimeSignal.DiscreteAnimation = updatedSignal.DiscreteAnimation;
            runtimeSignal.ToggleIntervalSeconds = updatedSignal.ToggleIntervalSeconds;

            if (_slaveService != null)
                _slaveService.UpdateSignal(CloneForPersistence(runtimeSignal));

            if (_nucDualLinkHost != null)
                _nucDualLinkHost.UpdateSignal(CloneForPersistence(runtimeSignal));

            AddStatus("RT", string.Format("Runtime signal IOA {0} updated manually.", runtimeSignal.Ioa));
        }

        private static void Trim<T>(ObservableCollection<T> collection, int maxCount)
        {
            while (collection.Count > maxCount)
                collection.RemoveAt(collection.Count - 1);
        }

        private void InvokeOnUi(Action action)
        {
            if (action == null)
            {
                return;
            }

            Dispatcher dispatcher = _uiDispatcher ?? (Application.Current != null ? Application.Current.Dispatcher : null);
            if (dispatcher == null)
            {
                action();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.BeginInvoke(action, DispatcherPriority.DataBind);
        }

        private void RefreshCommands()
        {
            AddSignalCommand.RaiseCanExecuteChanged();
            RemoveSignalCommand.RaiseCanExecuteChanged();
            NewProjectCommand.RaiseCanExecuteChanged();
            RunCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            RefreshPortsCommand.RaiseCanExecuteChanged();
            SimulateOpenCommand.RaiseCanExecuteChanged();
            SimulateCloseCommand.RaiseCanExecuteChanged();
            SimulateOnCommand.RaiseCanExecuteChanged();
            SimulateOffCommand.RaiseCanExecuteChanged();
            InjectBufferBurstCommand.RaiseCanExecuteChanged();
            ToggleNucLinkACommand.RaiseCanExecuteChanged();
            ToggleNucLinkBCommand.RaiseCanExecuteChanged();
        }
    }
}







