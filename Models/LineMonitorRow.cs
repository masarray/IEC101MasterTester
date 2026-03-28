namespace IEC101MasterTester.Models
{
    public sealed class LineMonitorRow
    {
        public string Time { get; set; }
        public string Direction { get; set; }
        public string FrameType { get; set; }
        public string Summary { get; set; }

        public string ControlFc { get; set; }
        public string ACD { get; set; }
        public string DFC { get; set; }

        public string AsduType { get; set; }
        public string COT { get; set; }
        public string CASDU { get; set; }

        public string IOA { get; set; }     // NEW (important for analyzer)

        public string RawHex { get; set; }
        public string Detail { get; set; }

        public string DataClass { get; set; }
    }
}