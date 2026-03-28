using System;

namespace IEC101MasterTester.Models
{
    public sealed class NucRedundancyValueEventArgs : EventArgs
    {
        public string ChannelName { get; set; }

        public ValueViewerRow Value { get; set; }
    }
}
