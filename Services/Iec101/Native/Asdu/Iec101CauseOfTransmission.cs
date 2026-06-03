namespace IEC101MasterTester.Services.Iec101.Native.Asdu
{
    public enum Iec101CauseOfTransmission
    {
        Unknown = 0,
        Periodic = 1,
        BackgroundScan = 2,
        Spontaneous = 3,
        Initialized = 4,
        Request = 5,
        Activation = 6,
        ActivationCon = 7,
        Deactivation = 8,
        DeactivationCon = 9,
        ActivationTermination = 10,
        InterrogatedByStation = 20
    }
}
