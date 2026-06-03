using System.Collections.Generic;

namespace IEC101MasterTester.Services.Iec101.Native.Asdu
{
    public sealed class Iec101Asdu
    {
        public int TypeIdRaw { get; set; }
        public Iec101TypeId TypeId { get; set; }
        public byte VariableStructureQualifier { get; set; }
        public bool IsSequence { get; set; }
        public int ObjectCount { get; set; }
        public int CauseRaw { get; set; }
        public Iec101CauseOfTransmission Cause { get; set; }
        public bool IsTest { get; set; }
        public bool IsNegativeConfirm { get; set; }
        public int OriginatorAddress { get; set; }
        public int CommonAddress { get; set; }
        public byte[] RawBytes { get; set; }
        public List<Iec101InformationObject> Objects { get; private set; }

        public Iec101Asdu()
        {
            Objects = new List<Iec101InformationObject>();
        }
    }
}
