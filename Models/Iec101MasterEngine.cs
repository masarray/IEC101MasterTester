using System.Runtime.Serialization;

namespace IEC101MasterTester.Models
{
    [DataContract]
    public enum Iec101MasterEngine
    {
        [EnumMember]
        Native = 0,

        [EnumMember]
        DiagnosticNative = 1
    }
}
