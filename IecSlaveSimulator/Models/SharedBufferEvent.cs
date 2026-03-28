using System;

namespace IecSlaveSimulator.Models
{
    public sealed class SharedBufferEvent
    {
        public int Ioa { get; set; }

        public string Value { get; set; }

        public string Cot { get; set; }

        public DateTime TimestampUtc { get; set; }

        public string Source { get; set; }

        public SlaveSignalType SignalType { get; set; }

        public string Quality { get; set; }

        public bool UseTimestamp { get; set; }

        public int Casdu { get; set; }

        public long SequenceNumber { get; set; }
    }
}
