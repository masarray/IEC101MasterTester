namespace IEC101MasterTester.Models
{
    public sealed class NucRedundancySessionState
    {
        public bool IsActive { get; set; }

        public string StatusText { get; set; }

        public string DetailText { get; set; }

        public NucRedundancySettings Settings { get; set; }

        public string PrimaryStatusText { get; set; }

        public string BackupStatusText { get; set; }

        public string ActiveChannel { get; set; }

        public string ControllerState { get; set; }

        public string PrimaryRole { get; set; }

        public string BackupRole { get; set; }

        public string PrimaryChannelState { get; set; }

        public string BackupChannelState { get; set; }

        public int PrimaryRxCount { get; set; }

        public int PrimaryTxCount { get; set; }

        public int BackupRxCount { get; set; }

        public int BackupTxCount { get; set; }

        public int PrimarySupervisionTickCount { get; set; }

        public int PrimarySupervisionTxObservedCount { get; set; }

        public int PrimarySupervisionResponseObservedCount { get; set; }

        public int BackupSupervisionTickCount { get; set; }

        public int BackupSupervisionTxObservedCount { get; set; }

        public int BackupSupervisionResponseObservedCount { get; set; }

        public string PrimaryLastActivityUtcText { get; set; }

        public string BackupLastActivityUtcText { get; set; }

        public string PrimaryLastResponseUtcText { get; set; }

        public string BackupLastResponseUtcText { get; set; }
    }
}
