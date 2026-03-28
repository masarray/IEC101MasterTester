using System;
using System.Runtime.Serialization;

namespace IEC101MasterTester.Models.Licensing
{
    [DataContract]
    public sealed class PersistedLicensePayload
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string InstallId { get; set; }

        [DataMember(Order = 3)]
        public string HardwareId { get; set; }

        [DataMember(Order = 4)]
        public DateTime FirstRunUtc { get; set; }

        [DataMember(Order = 5)]
        public DateTime LastRunUtc { get; set; }

        [DataMember(Order = 6)]
        public DateTime? LastReminderUtc { get; set; }

        [DataMember(Order = 7)]
        public LicenseState LicenseState { get; set; }

        [DataMember(Order = 8)]
        public string ActivatedLicenseKey { get; set; }

        [DataMember(Order = 9)]
        public bool TamperFlag { get; set; }

        [DataMember(Order = 10)]
        public string TamperReason { get; set; }

        [DataMember(Order = 11)]
        public string Signature { get; set; }
    }
}
