using System;
using System.Globalization;
using System.Runtime.Serialization;
using IecSlaveSimulator.ViewModels;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public sealed class SignalDefinition : ViewModelBase
    {
        private bool _isEnabled;
        private int _ioa;
        private string _label;
        private SlaveSignalType _signalType;
        private int _casdu;
        private SignalClass _signalClass;
        private SignalPublishMode _publishMode;
        private bool _backgroundEnabled;
        private bool _spontaneousEnabled;
        private bool _useTimestamp;
        private string _quality;
        private string _defaultValue;
        private string _runtimeValue;
        private string _liveCot;
        private int _linkedStatusIoa;
        private CommandSemantic _commandSemantic;
        private CommandBindingMode _commandBindingMode;
        private CommandOperateMode _commandOperateMode;
        private int _commandDelayMs;
        private AnalogAnimationKind _analogAnimation;
        private double _analogFrom;
        private double _analogTo;
        private double _analogStep;
        private int _animationIntervalMs;
        private bool _analogPingPong;
        private DiscreteAnimationKind _discreteAnimation;
        private int _toggleIntervalSeconds;
        private string _notes;

        [IgnoreDataMember]
        private DateTime _nextAnimationAt;

        [IgnoreDataMember]
        private bool _animationAscending;

        public SignalDefinition()
        {
            _isEnabled = true;
            _label = "New Signal";
            _signalType = SlaveSignalType.SinglePoint;
            _casdu = 1;
            _signalClass = SignalClass.Class2;
            _publishMode = SignalPublishMode.BackgroundScan;
            _backgroundEnabled = true;
            _spontaneousEnabled = false;
            _quality = "Good";
            _defaultValue = "OFF";
            _runtimeValue = "OFF";
            _liveCot = "BgScan";
            _commandSemantic = CommandSemantic.None;
            _commandBindingMode = CommandBindingMode.Spontaneous;
            _commandOperateMode = CommandOperateMode.Both;
            _commandDelayMs = 100;
            _analogAnimation = AnalogAnimationKind.None;
            _analogFrom = 19.71d;
            _analogTo = 20.29d;
            _analogStep = 0.01d;
            _animationIntervalMs = 1000;
            _discreteAnimation = DiscreteAnimationKind.None;
            _toggleIntervalSeconds = 5;
            _notes = string.Empty;
        }

        [DataMember(Order = 1)] public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
        [DataMember(Order = 2)] public int Ioa { get => _ioa; set => SetProperty(ref _ioa, value); }
        [DataMember(Order = 3)] public string Label { get => _label; set => SetProperty(ref _label, value); }
        [DataMember(Order = 4)]
        public SlaveSignalType SignalType
        {
            get => _signalType;
            set
            {
                if (SetProperty(ref _signalType, value))
                {
                    NormalizeTypeSpecificFields();
                    RaiseTypeSpecificPropertiesChanged();
                }
            }
        }
        [DataMember(Order = 5)] public int Casdu { get => _casdu; set => SetProperty(ref _casdu, value); }
        [DataMember(Order = 6)] public SignalClass SignalClass { get => _signalClass; set => SetProperty(ref _signalClass, value); }
        [DataMember(Order = 7)]
        public SignalPublishMode PublishMode
        {
            get => _publishMode;
            set
            {
                if (SetProperty(ref _publishMode, value))
                {
                    ApplyPublishMode(value);
                    RaisePropertyChanged(nameof(PublishSummary));
                }
            }
        }
        [DataMember(Order = 8)] public bool BackgroundEnabled { get => _backgroundEnabled; set => SetProperty(ref _backgroundEnabled, value); }
        [DataMember(Order = 9)] public bool SpontaneousEnabled { get => _spontaneousEnabled; set => SetProperty(ref _spontaneousEnabled, value); }
        [DataMember(Order = 10)] public bool UseTimestamp { get => _useTimestamp; set => SetProperty(ref _useTimestamp, value); }
        [DataMember(Order = 11)] public string Quality { get => _quality; set => SetProperty(ref _quality, value); }
        [DataMember(Order = 12)] public string DefaultValue { get => _defaultValue; set => SetProperty(ref _defaultValue, value); }
        [DataMember(Order = 13)] public string RuntimeValue { get => _runtimeValue; set => SetProperty(ref _runtimeValue, value); }
        [DataMember(Order = 14)] public string LiveCot { get => _liveCot; set => SetProperty(ref _liveCot, value); }
        [DataMember(Order = 15)] public int LinkedStatusIoa { get => _linkedStatusIoa; set => SetProperty(ref _linkedStatusIoa, value); }
        [DataMember(Order = 16)] public CommandSemantic CommandSemantic { get => _commandSemantic; set => SetProperty(ref _commandSemantic, value); }
        [DataMember(Order = 17)] public CommandBindingMode CommandBindingMode { get => _commandBindingMode; set => SetProperty(ref _commandBindingMode, value); }
        [DataMember(Order = 18)] public CommandOperateMode CommandOperateMode { get => _commandOperateMode; set => SetProperty(ref _commandOperateMode, value); }
        [DataMember(Order = 19)] public int CommandDelayMs { get => _commandDelayMs; set => SetProperty(ref _commandDelayMs, value); }
        [DataMember(Order = 20)] public AnalogAnimationKind AnalogAnimation { get => _analogAnimation; set => SetProperty(ref _analogAnimation, value); }
        [DataMember(Order = 21)] public double AnalogFrom { get => _analogFrom; set => SetProperty(ref _analogFrom, value); }
        [DataMember(Order = 22)] public double AnalogTo { get => _analogTo; set => SetProperty(ref _analogTo, value); }
        [DataMember(Order = 23)] public double AnalogStep { get => _analogStep; set => SetProperty(ref _analogStep, value); }
        [DataMember(Order = 24)] public int AnimationIntervalMs { get => _animationIntervalMs; set => SetProperty(ref _animationIntervalMs, value); }
        [DataMember(Order = 25)] public bool AnalogPingPong { get => _analogPingPong; set => SetProperty(ref _analogPingPong, value); }
        [DataMember(Order = 26)] public DiscreteAnimationKind DiscreteAnimation { get => _discreteAnimation; set => SetProperty(ref _discreteAnimation, value); }
        [DataMember(Order = 27)] public int ToggleIntervalSeconds { get => _toggleIntervalSeconds; set => SetProperty(ref _toggleIntervalSeconds, value); }
        [DataMember(Order = 28)] public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

        [IgnoreDataMember] public string TypeLabel => SignalType.ToString();
        [IgnoreDataMember] public bool IsMeasurement => SignalType == SlaveSignalType.MeasuredNormalized || SignalType == SlaveSignalType.MeasuredScaled || SignalType == SlaveSignalType.MeasuredShort || SignalType == SlaveSignalType.StepPosition;
        [IgnoreDataMember] public bool IsDiscrete => SignalType == SlaveSignalType.SinglePoint || SignalType == SlaveSignalType.DoublePoint;
        [IgnoreDataMember] public bool IsCommand => SignalType == SlaveSignalType.CommandSingle || SignalType == SlaveSignalType.CommandDouble || SignalType == SlaveSignalType.CommandSetpointNormalized || SignalType == SlaveSignalType.CommandSetpointScaled || SignalType == SlaveSignalType.CommandSetpointShort;
        [IgnoreDataMember] public bool SupportsAnalogFields => IsMeasurement;
        [IgnoreDataMember] public bool SupportsDiscreteFields => IsDiscrete;
        [IgnoreDataMember] public bool SupportsCommandFields => IsCommand;
        [IgnoreDataMember] public string CommandOperateModeLabel => GetCommandOperateModeLabel(CommandOperateMode);
        [IgnoreDataMember] public string PublishSummary => string.Format("Bg={0}, Spont={1}", BackgroundEnabled ? "On" : "Off", SpontaneousEnabled ? "On" : "Off");

        public SignalDefinition CloneForRuntime()
        {
            SignalDefinition clone = new SignalDefinition
            {
                IsEnabled = IsEnabled,
                Ioa = Ioa,
                Label = Label,
                SignalType = SignalType,
                Casdu = Casdu,
                SignalClass = SignalClass,
                PublishMode = PublishMode,
                BackgroundEnabled = BackgroundEnabled,
                SpontaneousEnabled = SpontaneousEnabled,
                UseTimestamp = UseTimestamp,
                Quality = Quality,
                DefaultValue = DefaultValue,
                RuntimeValue = string.IsNullOrWhiteSpace(RuntimeValue) ? DefaultValue : RuntimeValue,
                LiveCot = ResolveInitialCot(),
                LinkedStatusIoa = LinkedStatusIoa,
                CommandSemantic = CommandSemantic,
                CommandBindingMode = CommandBindingMode,
                CommandOperateMode = CommandOperateMode,
                CommandDelayMs = CommandDelayMs,
                AnalogAnimation = AnalogAnimation,
                AnalogFrom = AnalogFrom,
                AnalogTo = AnalogTo,
                AnalogStep = AnalogStep,
                AnimationIntervalMs = AnimationIntervalMs,
                AnalogPingPong = AnalogPingPong,
                DiscreteAnimation = DiscreteAnimation,
                ToggleIntervalSeconds = ToggleIntervalSeconds,
                Notes = Notes
            };

            clone.InitializeRuntimeAnimationState(DateTime.Now);
            return clone;
        }

        public void InitializeRuntimeAnimationState(DateTime now)
        {
            _animationAscending = true;
            _nextAnimationAt = now.AddMilliseconds(Math.Max(100, AnimationIntervalMs));

            if (IsMeasurement && string.IsNullOrWhiteSpace(RuntimeValue))
            {
                RuntimeValue = AnalogFrom.ToString("0.###", CultureInfo.InvariantCulture);
            }
            else if (IsDiscrete && string.IsNullOrWhiteSpace(RuntimeValue))
            {
                RuntimeValue = DefaultValue;
            }
        }

        public bool TryAdvanceAnimation(DateTime now)
        {
            if (!IsEnabled || now < _nextAnimationAt)
            {
                return false;
            }

            bool changed = false;

            if (IsMeasurement && AnalogAnimation != AnalogAnimationKind.None)
            {
                double current = ParseDouble(RuntimeValue, AnalogFrom);
                double step = Math.Abs(AnalogStep) <= 0.0001d ? 0.01d : Math.Abs(AnalogStep);
                double next = _animationAscending ? current + step : current - step;

                if (AnalogAnimation == AnalogAnimationKind.RampLoop)
                {
                    if (next > AnalogTo)
                    {
                        next = AnalogFrom;
                    }
                }
                else
                {
                    if (next >= AnalogTo)
                    {
                        next = AnalogTo;
                        _animationAscending = false;
                    }
                    else if (next <= AnalogFrom)
                    {
                        next = AnalogFrom;
                        _animationAscending = true;
                    }
                }

                RuntimeValue = next.ToString("0.###", CultureInfo.InvariantCulture);
                changed = true;
            }
            else if (IsDiscrete && DiscreteAnimation == DiscreteAnimationKind.Toggle)
            {
                RuntimeValue = string.Equals(RuntimeValue, "ON", StringComparison.OrdinalIgnoreCase) ? "OFF" : "ON";
                changed = true;
            }

            _nextAnimationAt = now.AddMilliseconds(Math.Max(100, ResolveAnimationIntervalMs()));

            if (changed)
            {
                LiveCot = ResolveAnimationCot();
            }

            return changed;
        }

        public void ApplyBoundCommand(CommandIntent intent)
        {
            if (IsMeasurement)
            {
                return;
            }

            switch (intent)
            {
                case CommandIntent.Open:
                case CommandIntent.Off:
                case CommandIntent.Lower:
                    RuntimeValue = "OFF";
                    break;
                case CommandIntent.Close:
                case CommandIntent.On:
                case CommandIntent.Raise:
                    RuntimeValue = "ON";
                    break;
            }

            LiveCot = ResolveBindingCot();
        }

        public string ResolveInitialCot()
        {
            switch (PublishMode)
            {
                case SignalPublishMode.Spontaneous:
                    return "Spont";
                case SignalPublishMode.GiOnly:
                    return "GI";
                case SignalPublishMode.CommandFeedback:
                    return "CmdFb";
                default:
                    return "BgScan";
            }
        }

        public string ResolveAnimationCot()
        {
            if (SpontaneousEnabled)
            {
                return "Spont";
            }

            return ResolveInitialCot();
        }

        public string ResolveBindingCot()
        {
            switch (CommandBindingMode)
            {
                case CommandBindingMode.CommandFeedback:
                    return "CmdFb";
                case CommandBindingMode.BackgroundOnly:
                    return "BgScan";
                default:
                    return "Spont";
            }
        }

        private void NormalizeTypeSpecificFields()
        {
            if (!SupportsAnalogFields)
            {
                AnalogAnimation = AnalogAnimationKind.None;
            }

            if (!SupportsDiscreteFields)
            {
                DiscreteAnimation = DiscreteAnimationKind.None;
                ToggleIntervalSeconds = 5;
            }

            if (!SupportsCommandFields)
            {
                LinkedStatusIoa = 0;
                CommandSemantic = CommandSemantic.None;
            }
        }

        private void ApplyPublishMode(SignalPublishMode mode)
        {
            switch (mode)
            {
                case SignalPublishMode.BackgroundScan:
                    BackgroundEnabled = true;
                    SpontaneousEnabled = false;
                    break;
                case SignalPublishMode.Spontaneous:
                    BackgroundEnabled = false;
                    SpontaneousEnabled = true;
                    break;
                case SignalPublishMode.BackgroundAndSpontaneous:
                    BackgroundEnabled = true;
                    SpontaneousEnabled = true;
                    break;
                case SignalPublishMode.GiOnly:
                case SignalPublishMode.CommandFeedback:
                    BackgroundEnabled = false;
                    SpontaneousEnabled = false;
                    break;
            }
        }

        private void RaiseTypeSpecificPropertiesChanged()
        {
            RaisePropertyChanged(nameof(TypeLabel));
            RaisePropertyChanged(nameof(IsMeasurement));
            RaisePropertyChanged(nameof(IsDiscrete));
            RaisePropertyChanged(nameof(IsCommand));
            RaisePropertyChanged(nameof(SupportsAnalogFields));
            RaisePropertyChanged(nameof(SupportsDiscreteFields));
            RaisePropertyChanged(nameof(SupportsCommandFields));
            RaisePropertyChanged(nameof(CommandOperateModeLabel));
        }

        private int ResolveAnimationIntervalMs()
        {
            if (IsDiscrete && DiscreteAnimation == DiscreteAnimationKind.Toggle)
            {
                return Math.Max(100, ToggleIntervalSeconds * 1000);
            }

            return Math.Max(100, AnimationIntervalMs);
        }

        private static double ParseDouble(string value, double fallback)
        {
            double parsed;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static string GetCommandOperateModeLabel(CommandOperateMode mode)
        {
            switch (mode)
            {
                case CommandOperateMode.DirectOperate:
                    return "DO";
                case CommandOperateMode.SelectBeforeOperate:
                    return "SBO";
                default:
                    return "DO+SBO";
            }
        }
    }
}


