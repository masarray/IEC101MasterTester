using System.Collections.Generic;

namespace IEC101MasterTester.Models.Export
{
    public sealed class EventLogExportMetadata
    {
        public string Title { get; set; }
        public string ModuleName { get; set; }
        public string SourceWindow { get; set; }
        public string SessionStartedText { get; set; }
        public string ContextSummary { get; set; }
        public string FilterSummary { get; set; }
        public string ExportedAtText { get; set; }
        public IList<KeyValuePair<string, string>> SummaryRows { get; set; }
    }
}
