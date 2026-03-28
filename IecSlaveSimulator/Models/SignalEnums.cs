using System.Runtime.Serialization;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public enum SlaveSignalType
    {
        [EnumMember] SinglePoint,
        [EnumMember] DoublePoint,
        [EnumMember] MeasuredNormalized,
        [EnumMember] MeasuredScaled,
        [EnumMember] MeasuredShort,
        [EnumMember] StepPosition,
        [EnumMember] CommandSingle,
        [EnumMember] CommandDouble,
        [EnumMember] CommandSetpointNormalized,
        [EnumMember] CommandSetpointScaled,
        [EnumMember] CommandSetpointShort
    }

    [DataContract]
    public enum SignalPublishMode
    {
        [EnumMember] BackgroundScan,
        [EnumMember] Spontaneous,
        [EnumMember] BackgroundAndSpontaneous,
        [EnumMember] GiOnly,
        [EnumMember] CommandFeedback
    }

    [DataContract]
    public enum SignalClass
    {
        [EnumMember] Class1,
        [EnumMember] Class2
    }

    [DataContract]
    public enum AnalogAnimationKind
    {
        [EnumMember] None,
        [EnumMember] RampLoop,
        [EnumMember] RampPingPong
    }

    [DataContract]
    public enum DiscreteAnimationKind
    {
        [EnumMember] None,
        [EnumMember] Toggle
    }

    [DataContract]
    public enum CommandSemantic
    {
        [EnumMember] None,
        [EnumMember] OpenClose,
        [EnumMember] OnOff,
        [EnumMember] RaiseLower
    }

    [DataContract]
    public enum CommandBindingMode
    {
        [EnumMember] None,
        [EnumMember] Spontaneous,
        [EnumMember] CommandFeedback,
        [EnumMember] BackgroundOnly
    }

    [DataContract]
    public enum CommandOperateMode
    {
        [EnumMember] DirectOperate,
        [EnumMember] SelectBeforeOperate,
        [EnumMember] Both
    }

    public enum CommandIntent
    {
        Open,
        Close,
        On,
        Off,
        Raise,
        Lower
    }
}
