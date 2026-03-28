using System.Runtime.Serialization;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public sealed class NucSlaveSettings
    {
        [DataMember(Order = 1)]
        public SlaveOperatingMode OperatingMode { get; set; }

        [DataMember(Order = 2)]
        public string PrimaryPortName { get; set; }

        [DataMember(Order = 3)]
        public string BackupPortName { get; set; }

        [DataMember(Order = 4)]
        public int PrimaryLinkAddress { get; set; }

        [DataMember(Order = 5)]
        public int BackupLinkAddress { get; set; }

        [DataMember(Order = 6)]
        public bool EmitGatewayBaselineOnStart { get; set; }

        [DataMember(Order = 7)]
        public bool ShareEventBufferAcrossLinks { get; set; }

        [DataMember(Order = 8)]
        public BufferInjectionMode BufferInjectionMode { get; set; }

        [DataMember(Order = 9)]
        public int BufferInjectionSignalCount { get; set; }

        [DataMember(Order = 10)]
        public int BufferInjectionBurstSize { get; set; }

        [DataMember(Order = 11)]
        public int BufferInjectionIntervalMs { get; set; }

        public static NucSlaveSettings CreateDefault()
        {
            return new NucSlaveSettings
            {
                OperatingMode = SlaveOperatingMode.SingleLink,
                PrimaryPortName = string.Empty,
                BackupPortName = string.Empty,
                PrimaryLinkAddress = 1,
                BackupLinkAddress = 1,
                EmitGatewayBaselineOnStart = true,
                ShareEventBufferAcrossLinks = true,
                BufferInjectionMode = BufferInjectionMode.Disabled,
                BufferInjectionSignalCount = 640,
                BufferInjectionBurstSize = 64,
                BufferInjectionIntervalMs = 250
            };
        }
    }
}
