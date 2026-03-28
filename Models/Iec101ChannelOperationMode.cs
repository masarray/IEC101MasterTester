using System.Runtime.Serialization;

namespace IEC101MasterTester.Models
{
    [DataContract]
    public enum Iec101ChannelOperationMode
    {
        [EnumMember]
        FullActive = 0,

        [EnumMember]
        StandbySupervision = 1,
    }
}
