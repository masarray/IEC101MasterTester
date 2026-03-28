using System;

namespace IEC101MasterTester.Models
{
    public sealed class NucRedundancyLineMonitorEventArgs : EventArgs
    {
        public string ChannelName { get; set; }

        public LineMonitorRow Record { get; set; }
    }
}
