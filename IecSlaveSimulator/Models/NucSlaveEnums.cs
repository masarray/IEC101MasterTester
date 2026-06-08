using System.Runtime.Serialization;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public enum SlaveOperatingMode
    {
        [EnumMember] SingleLink,
        [EnumMember] NucDualLink
    }

    [DataContract]
    public enum NucSlaveLinkState
    {
        [EnumMember] Disconnected,
        [EnumMember] StandbyReady,
        [EnumMember] ActivePolling,
        [EnumMember] Timeout,
        [EnumMember] Faulted,
        [EnumMember] Recovering
    }

    [DataContract]
    public enum BufferInjectionMode
    {
        [EnumMember] Disabled,
        [EnumMember] BurstOnce,
        [EnumMember] PacedBurst,
        [EnumMember] ContinuousStress
    }
}
