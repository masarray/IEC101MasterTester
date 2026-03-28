using System.Runtime.Serialization;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public sealed class SlaveConnectionSettings
    {
        [DataMember(Order = 1)] public string SerialPort { get; set; }
        [DataMember(Order = 2)] public int BaudRate { get; set; }
        [DataMember(Order = 3)] public int DataBits { get; set; }
        [DataMember(Order = 4)] public string Parity { get; set; }
        [DataMember(Order = 5)] public string StopBits { get; set; }
        [DataMember(Order = 6)] public string LinkLayerMode { get; set; }
        [DataMember(Order = 7)] public int LinkAddressLength { get; set; }
        [DataMember(Order = 8)] public int LinkAddress { get; set; }
        [DataMember(Order = 9)] public int CasduLength { get; set; }
        [DataMember(Order = 10)] public int CommonAddress { get; set; }
        [DataMember(Order = 11)] public int IoaLength { get; set; }
        [DataMember(Order = 12)] public int OriginatorAddress { get; set; }
        [DataMember(Order = 13)] public int ResponseTimeoutMs { get; set; }
        [DataMember(Order = 14)] public int BackgroundPublishIntervalMs { get; set; }
        [DataMember(Order = 15)] public int RunLoopDelayMs { get; set; }
        [DataMember(Order = 16)] public int Class1QueueSize { get; set; }
        [DataMember(Order = 17)] public bool EnableMeasurementStreaming { get; set; }
        [DataMember(Order = 18)] public SlaveOperatingMode OperatingMode { get; set; }
        [DataMember(Order = 19)] public string BackupSerialPort { get; set; }
        [DataMember(Order = 20)] public int BackupLinkAddress { get; set; }
        [DataMember(Order = 21)] public bool EmitGatewayBaselineOnStart { get; set; }
        [DataMember(Order = 22)] public bool ShareEventBufferAcrossLinks { get; set; }
        [DataMember(Order = 23)] public BufferInjectionMode BufferInjectionMode { get; set; }
        [DataMember(Order = 24)] public int BufferInjectionSignalCount { get; set; }
        [DataMember(Order = 25)] public int BufferInjectionBurstSize { get; set; }
        [DataMember(Order = 26)] public int BufferInjectionIntervalMs { get; set; }

        public static SlaveConnectionSettings CreateDefault()
        {
            return new SlaveConnectionSettings
            {
                SerialPort = "COM1",
                BaudRate = 9600,
                DataBits = 8,
                Parity = "Even",
                StopBits = "One",
                LinkLayerMode = "Unbalanced",
                LinkAddressLength = 2,
                LinkAddress = 1,
                CasduLength = 2,
                CommonAddress = 1,
                IoaLength = 3,
                OriginatorAddress = 0,
                ResponseTimeoutMs = 300,
                BackgroundPublishIntervalMs = 500,
                RunLoopDelayMs = 20,
                Class1QueueSize = 2048,
                EnableMeasurementStreaming = true,
                OperatingMode = SlaveOperatingMode.SingleLink,
                BackupSerialPort = string.Empty,
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
