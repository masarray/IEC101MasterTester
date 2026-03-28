namespace IEC101MasterTester.Models
{
    public enum SlaveAvailabilityState
    {
        Disconnected,
        Connecting,
        TransportUp,
        LinkResponsive,
        ApplicationResponsive,
        NoApplicationData,
        Silent,
        Degraded
    }
}
