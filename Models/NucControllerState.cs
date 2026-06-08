namespace IEC101MasterTester.Models
{
    public enum NucControllerState
    {
        Starting = 0,
        Bootstrapping = 1,
        Healthy = 2,
        Switching = 3,
        Degraded = 4,
        NoAvailableLink = 5,
        Recovering = 6,
    }
}
