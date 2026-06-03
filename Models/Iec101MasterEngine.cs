using System.Runtime.Serialization;

namespace IEC101MasterTester.Models
{
    [DataContract]
    public enum Iec101MasterEngine
    {
        [EnumMember]
        Lib60870 = 0,

        [EnumMember]
        NativeExperimental = 1
    }
}
