namespace IEC101MasterTester.Models
{
    public enum NucControllerState
    {
        Starting = 0,
        Healthy = 1,
        Switching = 2,
        Degraded = 3,
        NoAvailableLink = 4,
    }
}
