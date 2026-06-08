namespace IEC101MasterTester.Models
{
    public enum NucChannelState
    {
        Disconnected = 0,
        StandbySupervision = 1,
        ConnectedNoResponse = 2,
        Responsive = 3,
        Timeout = 4,
        FaultLatched = 5,
        Recovering = 6,
        Reopening = 7,
    }
}
