using System.Runtime.Serialization;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public sealed class RuntimeLogEntry
    {
        [DataMember(Order = 1)]
        public string Time { get; set; }

        [DataMember(Order = 2)]
        public string Category { get; set; }

        [DataMember(Order = 3)]
        public string Message { get; set; }
    }
}
