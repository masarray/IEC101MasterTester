using IEC101MasterTester.Models.Licensing;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IEC101MasterTester.Services.Licensing
{
    public sealed class LicenseManager
    {
        private readonly LicenseStore _licenseStore;
        private readonly HardwareFingerprintService _hardwareFingerprintService;
        private readonly TrialPolicyEvaluator _trialPolicyEvaluator;
        private readonly IActivationKeyValidator _activationKeyValidator;

        public LicenseManager(
            LicenseStore licenseStore,
            HardwareFingerprintService hardwareFingerprintService,
            TrialPolicyEvaluator trialPolicyEvaluator,
            IActivationKeyValidator activationKeyValidator)
        {
            _licenseStore = licenseStore;
            _hardwareFingerprintService = hardwareFingerprintService;
            _trialPolicyEvaluator = trialPolicyEvaluator;
            _activationKeyValidator = activationKeyValidator;
        }

        public LicenseSnapshot CurrentSnapshot { get; private set; }
        public string RegistryLocation => _licenseStore.RegistryLocation;
        public string ProgramDataLocation => _licenseStore.ProgramDataLocation;
        public string LocalAppDataLocation => _licenseStore.LocalAppDataLocation;
        public bool IsLicensed => CurrentSnapshot?.IsLicensed == true;
        public bool IsTrialActive => CurrentSnapshot?.IsTrialActive == true;
        public bool IsExpired => CurrentSnapshot?.IsExpired == true;
        public bool IsPermanentDemoLocked => CurrentSnapshot?.IsPermanentDemoLocked == true;
        public bool CanExport => IsLicensed;
        public bool CanRunLongDurationAvailability => IsLicensed;
        public bool CanUseAdvancedRedundancyTools => IsLicensed;
        public bool CanUseAdvancedReports => IsLicensed;
        public bool CanUseUnlimitedSession => IsLicensed;

        public Task<LicenseSnapshot> InitializeAsync()
        {
            string hardwareId = _hardwareFingerprintService.GenerateHardwareId();
            DateTime nowUtc = DateTime.UtcNow;
            var records = _licenseStore.LoadAll();
            string tamperReason;
            PersistedLicensePayload payload = _licenseStore.Reconcile(records, hardwareId, out tamperReason);

            if (payload == null)
            {
                payload = CreateInitialPayload(hardwareId, nowUtc);
            }

            if (string.IsNullOrWhiteSpace(tamperReason) && CanRecoverLegacyPermanentLock(payload))
            {
                payload.TamperFlag = false;
                payload.TamperReason = null;
                if (payload.LicenseState == LicenseState.PermanentDemoLocked)
                {
                    payload.LicenseState = LicenseState.Trial;
                }
            }

            if (!string.IsNullOrWhiteSpace(tamperReason))
            {
                payload.TamperFlag = true;
                payload.TamperReason = tamperReason;
                payload.LicenseState = LicenseState.PermanentDemoLocked;
            }

            if (nowUtc < payload.LastRunUtc - TrialPolicyEvaluator.ClockRollbackTolerance)
            {
                payload.TamperFlag = true;
                payload.TamperReason = "Clock rollback detected.";
                payload.LicenseState = LicenseState.PermanentDemoLocked;
            }

            payload.HardwareId = hardwareId;
            payload.LastRunUtc = nowUtc;
            CurrentSnapshot = _trialPolicyEvaluator.Evaluate(payload, nowUtc);
            _licenseStore.SaveAll(payload);
            return Task.FromResult(CurrentSnapshot);
        }

        public bool ValidateActivationKey(string activationKey, out string reason)
        {
            return _activationKeyValidator.ValidateActivationKey(activationKey, CurrentSnapshot?.HardwareId, out reason);
        }

        public async Task<bool> ActivateAsync(string activationKey)
        {
            string reason;
            if (!ValidateActivationKey(activationKey, out reason))
            {
                return await Task.FromResult(false);
            }

            DateTime nowUtc = DateTime.UtcNow;
            PersistedLicensePayload payload = CreateOrCloneCurrentPayload();
            payload.ActivatedLicenseKey = activationKey;
            payload.LicenseState = LicenseState.Licensed;
            payload.TamperFlag = false;
            payload.TamperReason = null;
            payload.LastRunUtc = nowUtc;
            _licenseStore.SaveAll(payload);
            CurrentSnapshot = _trialPolicyEvaluator.Evaluate(payload, nowUtc);
            return true;
        }

        public async Task MarkReminderShownAsync(DateTime reminderUtc)
        {
            PersistedLicensePayload payload = CreateOrCloneCurrentPayload();
            payload.LastReminderUtc = reminderUtc;
            payload.LastRunUtc = reminderUtc;
            _licenseStore.SaveAll(payload);
            CurrentSnapshot = _trialPolicyEvaluator.Evaluate(payload, reminderUtc);
            await Task.CompletedTask;
        }

        public LicenseManagerPhase2Hook CreatePhase2Hook()
        {
            return new LicenseManagerPhase2Hook(CurrentSnapshot, ValidateActivationKey, ActivateAsync);
        }

        private PersistedLicensePayload CreateInitialPayload(string hardwareId, DateTime nowUtc)
        {
            return new PersistedLicensePayload
            {
                SchemaVersion = 1,
                InstallId = Guid.NewGuid().ToString("N"),
                HardwareId = hardwareId,
                FirstRunUtc = nowUtc,
                LastRunUtc = nowUtc,
                LastReminderUtc = null,
                LicenseState = LicenseState.Trial,
                ActivatedLicenseKey = null,
                TamperFlag = false,
                TamperReason = null
            };
        }

        private static bool CanRecoverLegacyPermanentLock(PersistedLicensePayload payload)
        {
            if (payload == null
                || !payload.TamperFlag
                || payload.LicenseState != LicenseState.PermanentDemoLocked
                || string.IsNullOrWhiteSpace(payload.TamperReason))
            {
                return false;
            }

            string reason = payload.TamperReason.Trim();
            return reason.IndexOf("One or more license stores are missing.", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("License install identity mismatch detected.", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("License hardware identity mismatch detected.", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("License store copies are inconsistent.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private PersistedLicensePayload CreateOrCloneCurrentPayload()
        {
            if (CurrentSnapshot == null)
            {
                return CreateInitialPayload(_hardwareFingerprintService.GenerateHardwareId(), DateTime.UtcNow);
            }

            return new PersistedLicensePayload
            {
                SchemaVersion = 1,
                InstallId = CurrentSnapshot.InstallId,
                HardwareId = CurrentSnapshot.HardwareId,
                FirstRunUtc = CurrentSnapshot.FirstRunUtc,
                LastRunUtc = CurrentSnapshot.LastRunUtc,
                LastReminderUtc = CurrentSnapshot.LastReminderUtc,
                LicenseState = CurrentSnapshot.LicenseState,
                ActivatedLicenseKey = null,
                TamperFlag = CurrentSnapshot.IsTamperDetected,
                TamperReason = CurrentSnapshot.TamperReason
            };
        }
    }

    public sealed class LicenseManagerPhase2Hook
    {
        public delegate bool ActivationValidationDelegate(string activationKey, out string reason);

        public LicenseManagerPhase2Hook(LicenseSnapshot snapshot, ActivationValidationDelegate validateActivationKey, Func<string, Task<bool>> activateAsync)
        {
            Snapshot = snapshot;
            ValidateActivationKey = validateActivationKey;
            ActivateAsync = activateAsync;
        }

        public LicenseSnapshot Snapshot { get; }
        public ActivationValidationDelegate ValidateActivationKey { get; }
        public Func<string, Task<bool>> ActivateAsync { get; }
    }
}
