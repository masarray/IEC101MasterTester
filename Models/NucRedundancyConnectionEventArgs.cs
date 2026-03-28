using System;

namespace IEC101MasterTester.Models
{
    public sealed class NucRedundancyConnectionEventArgs : EventArgs
    {
        public string ChannelName { get; set; }

        public ConnectionStatusInfo Status { get; set; }
    }
}
