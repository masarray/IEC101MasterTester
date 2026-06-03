using System.Runtime.Serialization;

namespace IEC101MasterTester.Models
{
    [DataContract]
    public sealed class ConnectionSettings
    {
        [DataMember(Order = 1)]
        public string SerialPort { get; set; }

        [DataMember(Order = 2)]
        public int BaudRate { get; set; }

        [DataMember(Order = 3)]
        public int DataBits { get; set; }

        [DataMember(Order = 4)]
        public string Parity { get; set; }

        [DataMember(Order = 5)]
        public string StopBits { get; set; }

        [DataMember(Order = 6)]
        public string LinkLayerMode { get; set; }

        [DataMember(Order = 7)]
        public int LinkAddressLength { get; set; }

        [DataMember(Order = 8)]
        public int LinkAddress { get; set; }

        [DataMember(Order = 9)]
        public int CasduLength { get; set; }

        [DataMember(Order = 10)]
        public int CasduAddress { get; set; }

        [DataMember(Order = 11)]
        public int IoaLength { get; set; }

        [DataMember(Order = 12)]
        public int OriginatorAddress { get; set; }

        [DataMember(Order = 13)]
        public int ResponseTimeoutMs { get; set; }

        [DataMember(Order = 14)]
        public int LinkStatusTimeoutMs { get; set; }

        [DataMember(Order = 15)]
        public int PollIntervalMs { get; set; }

        [DataMember(Order = 16)]
        public bool UseGeneralInterrogationOnConnect { get; set; }

        [DataMember(Order = 17)]
        public bool UseClockSyncOnConnect { get; set; }

        [DataMember(Order = 18)]
        public bool UseSingleCharAck { get; set; }

        [DataMember(Order = 19)]
        public int RunLoopDelayMs { get; set; }

        [DataMember(Order = 20)]
        public int Class1PollIntervalMs { get; set; }

        [DataMember(Order = 21)]
        public int BusyBackoffMs { get; set; }

        [DataMember(Order = 22)]
        public int GiStartupDelayMs { get; set; }

        [DataMember(Order = 23)]
        public Iec101ChannelOperationMode ChannelOperationMode { get; set; }

        [DataMember(Order = 24)]
        public Iec101MasterEngine MasterEngine { get; set; }

        public string SerialSummary
        {
            get
            {
                return string.Format("{0}, {1} bps, {2}{3}{4}, {5}", SerialPort, BaudRate, DataBits, Parity, StopBits, LinkLayerMode);
            }
        }

        public ConnectionSettings Clone()
        {
            return new ConnectionSettings
            {
                SerialPort = SerialPort,
                BaudRate = BaudRate,
                DataBits = DataBits,
                Parity = Parity,
                StopBits = StopBits,
                LinkLayerMode = LinkLayerMode,
                LinkAddressLength = LinkAddressLength,
                LinkAddress = LinkAddress,
                CasduLength = CasduLength,
                CasduAddress = CasduAddress,
                IoaLength = IoaLength,
                OriginatorAddress = OriginatorAddress,
                ResponseTimeoutMs = ResponseTimeoutMs,
                LinkStatusTimeoutMs = LinkStatusTimeoutMs,
                PollIntervalMs = PollIntervalMs,
                UseGeneralInterrogationOnConnect = UseGeneralInterrogationOnConnect,
                UseClockSyncOnConnect = UseClockSyncOnConnect,
                UseSingleCharAck = UseSingleCharAck,
                RunLoopDelayMs = RunLoopDelayMs,
                Class1PollIntervalMs = Class1PollIntervalMs,
                BusyBackoffMs = BusyBackoffMs,
                GiStartupDelayMs = GiStartupDelayMs,
                ChannelOperationMode = ChannelOperationMode,
                MasterEngine = MasterEngine
            };
        }

        public static ConnectionSettings CreateDefault()
        {
            return new ConnectionSettings
            {
                SerialPort = "COM12",
                BaudRate = 1200,
                DataBits = 8,
                Parity = "Even",
                StopBits = "One",
                LinkLayerMode = "Unbalanced",
                LinkAddressLength = 2,
                LinkAddress = 105,
                CasduLength = 2,
                CasduAddress = 105,
                IoaLength = 3,
                OriginatorAddress = 0,
                ResponseTimeoutMs = 1343,
                LinkStatusTimeoutMs = 5000,
                PollIntervalMs = 100,
                UseGeneralInterrogationOnConnect = true,
                UseClockSyncOnConnect = false,
                UseSingleCharAck = false,
                RunLoopDelayMs = 100,
                Class1PollIntervalMs = 100,
                BusyBackoffMs = 150,
                GiStartupDelayMs = 800,
                ChannelOperationMode = Iec101ChannelOperationMode.FullActive,
                MasterEngine = Iec101MasterEngine.Lib60870
            };
        }
    }
}
