namespace IEC101MasterTester.Models
{
    public enum NucApplicationImageState
    {
        Empty = 0,
        Bootstrapping = 1,
        Partial = 2,
        Ready = 3,
        Stale = 4,
        Failed = 5,
    }
}
