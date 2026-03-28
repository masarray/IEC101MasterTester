namespace IEC101MasterTester.Models
{
    public sealed class FindingRow
    {
        public string Time { get; set; }

        public string Severity { get; set; }

        public string Category { get; set; }   // NEW (Protocol / Command / Link)

        public string RuleCode { get; set; }

        public string Title { get; set; }

        public string Detail { get; set; }

        public string IOA { get; set; }

        public string Type { get; set; }

        public string ExpectedClass { get; set; }

        public string ActualClass { get; set; }
    }
}
