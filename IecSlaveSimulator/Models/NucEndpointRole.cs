using System.Runtime.Serialization;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public enum NucEndpointRole
    {
        [EnumMember] None,
        [EnumMember] Active,
        [EnumMember] Standby
    }
}
