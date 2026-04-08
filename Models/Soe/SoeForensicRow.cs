using System;

namespace IEC101MasterTester.Models.Soe
{
    public sealed class SoeForensicRow
    {
        public DateTime RecvTimeUtc { get; set; }
        public DateTime? SourceTimeUtc { get; set; }
        public int? DeltaMs { get; set; }
        public string Channel { get; set; }
        public int CA { get; set; }
        public int IOA { get; set; }
        public int TypeId { get; set; }
        public string TypeIdText { get; set; }
        public string CotText { get; set; }
        public int CotRaw { get; set; }
        public string SignalName { get; set; }
        public string ValueText { get; set; }
        public string QualityText { get; set; }
        public string Origin { get; set; }
        public string DeliveryContext { get; set; }
        public string ClassContext { get; set; }
    }
}
