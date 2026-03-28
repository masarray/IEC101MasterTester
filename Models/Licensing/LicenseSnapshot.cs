using System;

namespace IEC101MasterTester.Models.Licensing
{
    public sealed class LicenseSnapshot
    {
        public LicenseState LicenseState { get; set; }
        public DemoAlertMode DemoAlertMode { get; set; }
        public string HardwareId { get; set; }
        public string InstallId { get; set; }
        public DateTime FirstRunUtc { get; set; }
        public DateTime LastRunUtc { get; set; }
        public int DaysElapsed { get; set; }
        public int DaysRemaining { get; set; }
        public bool IsTamperDetected { get; set; }
        public string TamperReason { get; set; }
        public bool IsLicensed { get; set; }
        public bool IsTrialActive { get; set; }
        public bool IsExpired { get; set; }
        public bool CanContinueInDemo { get; set; }
        public bool IsPermanentDemoLocked { get; set; }
        public bool IsReminderDue { get; set; }
        public DateTime? LastReminderUtc { get; set; }
    }
}
