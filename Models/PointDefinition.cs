namespace IEC101MasterTester.Models
{
    public sealed class PointDefinition
    {
        public string PointKey { get; set; }
        public int Ioa { get; set; }
        public int TypeId { get; set; }
        public string Mnemonic { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Bay { get; set; }
        public string ValueKind { get; set; }
        public string IecClass { get; set; }
        public string ExpectedCot { get; set; }
        public bool HasTimestamp { get; set; }
        public string RelatedCommandPointKey { get; set; }
        public string RelatedFeedbackPointKey { get; set; }
        public double? EngineeringMin { get; set; }
        public double? EngineeringMax { get; set; }
        public double? RawMin { get; set; }
        public double? RawMax { get; set; }
        public string Notes { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Mnemonic))
                {
                    return Mnemonic;
                }

                return string.IsNullOrWhiteSpace(Name) ? "IOA " + Ioa : Name;
            }
        }
    }
}
