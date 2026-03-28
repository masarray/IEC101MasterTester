using IEC101MasterTester.Models.Licensing;
using System;

namespace IEC101MasterTester.Services.Licensing
{
    public sealed class TrialPolicyEvaluator
    {
        public const int TrialDays = 30;
        public static readonly TimeSpan ReminderInterval = TimeSpan.FromMinutes(20);
        public static readonly TimeSpan ClockRollbackTolerance = TimeSpan.FromMinutes(5);

        public LicenseSnapshot Evaluate(PersistedLicensePayload payload, DateTime nowUtc)
        {
            int daysElapsed = Math.Max(0, (int)Math.Floor((nowUtc - payload.FirstRunUtc).TotalDays));
            int daysRemaining = Math.Max(0, TrialDays - daysElapsed);
            bool isLicensed = payload.LicenseState == LicenseState.Licensed && !payload.TamperFlag;
            bool isExpired = !isLicensed && daysElapsed >= TrialDays;
            bool isTrialActive = !isLicensed && !payload.TamperFlag && !isExpired;
            bool isPermanentLocked = payload.TamperFlag || payload.LicenseState == LicenseState.PermanentDemoLocked;
            DemoAlertMode alertMode = isPermanentLocked
                ? DemoAlertMode.PermanentLocked
                : (isExpired ? DemoAlertMode.Expired : DemoAlertMode.Reminder);

            LicenseState state = isPermanentLocked
                ? LicenseState.PermanentDemoLocked
                : (isLicensed ? LicenseState.Licensed : (isExpired ? LicenseState.DemoExpired : LicenseState.Trial));

            bool isReminderDue = !isLicensed && !isPermanentLocked
                && (!payload.LastReminderUtc.HasValue || nowUtc - payload.LastReminderUtc.Value >= ReminderInterval);

            return new LicenseSnapshot
            {
                LicenseState = state,
                DemoAlertMode = alertMode,
                HardwareId = payload.HardwareId,
                InstallId = payload.InstallId,
                FirstRunUtc = payload.FirstRunUtc,
                LastRunUtc = payload.LastRunUtc,
                DaysElapsed = daysElapsed,
                DaysRemaining = daysRemaining,
                IsTamperDetected = payload.TamperFlag,
                TamperReason = payload.TamperReason,
                IsLicensed = isLicensed,
                IsTrialActive = isTrialActive,
                IsExpired = isExpired,
                CanContinueInDemo = !isPermanentLocked,
                IsPermanentDemoLocked = isPermanentLocked,
                IsReminderDue = isReminderDue,
                LastReminderUtc = payload.LastReminderUtc
            };
        }
    }
}
