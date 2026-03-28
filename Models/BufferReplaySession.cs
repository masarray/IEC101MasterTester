namespace IEC101MasterTester.Models
{
    public sealed class BufferReplaySession
    {
        public string SessionId { get; set; }
        public string DisconnectedAtText { get; set; }
        public string ReconnectedAtText { get; set; }
        public int BufferedEventCount { get; set; }
        public int ReplayEventCount { get; set; }
        public int MissingEventCount { get; set; }
        public int DuplicateEventCount { get; set; }
        public int FifoViolationCount { get; set; }
        public int SampleCheckCount { get; set; }
        public int SampleTimestampViolationCount { get; set; }
        public bool MeetsMinimum600Events { get; set; }
        public string FinalVerdict { get; set; }
    }
}
