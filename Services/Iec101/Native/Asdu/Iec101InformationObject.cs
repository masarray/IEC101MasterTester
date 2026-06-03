using System;

namespace IEC101MasterTester.Services.Iec101.Native.Asdu
{
    public sealed class Iec101InformationObject
    {
        public int ObjectAddress { get; set; }
        public string TypeName { get; set; }
        public string ValueText { get; set; }
        public double? NumericValue { get; set; }
        public Iec101QualityDescriptor Quality { get; set; }
        public DateTime? TimestampUtc { get; set; }
        public byte[] RawBytes { get; set; }
    }
}
