using System.Collections.Generic;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Models.Export
{
    public sealed class EventLogExportRequest
    {
        public string OutputPath { get; set; }
        public EventLogExportMetadata Metadata { get; set; }
        public IList<EventLogRow> Rows { get; set; }
    }
}
