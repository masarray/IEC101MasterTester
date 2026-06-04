using System;

namespace IEC101MasterTester.Models
{
    public sealed class ProtocolEvidence
    {
        public long Sequence { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public string Engine { get; set; }
        public string Direction { get; set; }
        public string FrameType { get; set; }
        public string Control { get; set; }
        public string ACD { get; set; }
        public string DFC { get; set; }
        public string TypeId { get; set; }
        public string COT { get; set; }
        public string CASDU { get; set; }
        public string IOA { get; set; }
        public int LinkAddressLength { get; set; }
        public int LinkAddress { get; set; }
        public int CasduLength { get; set; }
        public int CasduAddress { get; set; }
        public int IoaLength { get; set; }
        public byte[] RawFrame { get; set; }
        public string DecodeStatus { get; set; }
        public string DecodeDetail { get; set; }
    }
}
