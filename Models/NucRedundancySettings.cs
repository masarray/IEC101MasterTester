using System.Runtime.Serialization;

namespace IEC101MasterTester.Models
{
    [DataContract]
    public sealed class NucRedundancySettings
    {
        [DataMember(Order = 1)]
        public ConnectionSettings BaseConnectionSettings { get; set; }

        [DataMember(Order = 2)]
        public string PrimarySerialPort { get; set; }

        [DataMember(Order = 3)]
        public string BackupSerialPort { get; set; }

        [DataMember(Order = 4)]
        public string RedundancyMode { get; set; }

        [DataMember(Order = 5)]
        public string GiPolicy { get; set; }
    }
}
