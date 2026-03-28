using System;

namespace IEC101MasterTester.Models
{
    public sealed class NucChannelSnapshot
    {
        public string ChannelName { get; set; }

        public NucChannelRole Role { get; set; }

        public NucChannelState State { get; set; }

        public bool Connected { get; set; }

        public int RxCount { get; set; }

        public int TxCount { get; set; }

        public DateTime? LastResponseUtc { get; set; }

        public DateTime? LastActivityUtc { get; set; }

        public DateTime? LastTimeoutUtc { get; set; }

        public int SupervisionTickCount { get; set; }

        public int SupervisionTxObservedCount { get; set; }

        public int SupervisionResponseObservedCount { get; set; }

        public DateTime? LastSupervisionTickUtc { get; set; }

        public DateTime? LastSupervisionTxObservedUtc { get; set; }

        public DateTime? LastSupervisionResponseUtc { get; set; }

        public string StatusText { get; set; }

        public string DetailText { get; set; }
    }
}
