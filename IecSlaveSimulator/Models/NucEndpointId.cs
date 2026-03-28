using System.Runtime.Serialization;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public enum NucEndpointId
    {
        [EnumMember] None = 0,
        [EnumMember] LinkA = 1,
        [EnumMember] LinkB = 2
    }
}
