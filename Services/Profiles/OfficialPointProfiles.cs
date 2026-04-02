using IEC101MasterTester.Models;
using System.Collections.Generic;

namespace IEC101MasterTester.Services.Profiles
{
    public static class OfficialPointProfiles
    {
        public static readonly OfficialPointProfile LegacyProfile = new OfficialPointProfile("LegacyProfile", new PointDefinition[0]);
        public static readonly OfficialPointProfile PlnPusertif101Profile = new OfficialPointProfile("PlnPusertif101Profile", CreatePlnPoints());
        public static readonly OfficialPointProfile PlnPusertif104Profile = new OfficialPointProfile("PlnPusertif104Profile", CreatePlnPoints());

        public static OfficialPointProfile ActiveProfile => PlnPusertif101Profile;

        public static bool TryGetPointByIoa(int ioa, out PointDefinition point)
        {
            if (PlnPusertif101Profile.TryGetByIoa(ioa, out point))
            {
                return true;
            }

            if (PlnPusertif104Profile.TryGetByIoa(ioa, out point))
            {
                return true;
            }

            return ActiveProfile.TryGetByIoa(ioa, out point);
        }

        public static string GetDisplayNameOrDefault(int ioa, string fallbackName)
        {
            PointDefinition point;
            if (TryGetPointByIoa(ioa, out point) && point != null)
            {
                return point.DisplayName;
            }

            return string.IsNullOrWhiteSpace(fallbackName) ? "IOA " + ioa : fallbackName;
        }

        public static string TryGetPointKey(int ioa)
        {
            PointDefinition point;
            if (TryGetPointByIoa(ioa, out point) && point != null)
            {
                return point.PointKey;
            }

            return null;
        }

        public static int? TryGetRelatedCommandIoa(int feedbackIoa)
        {
            PointDefinition feedbackPoint;
            if (!TryGetPointByIoa(feedbackIoa, out feedbackPoint))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(feedbackPoint.RelatedCommandPointKey))
            {
                return null;
            }

            PointDefinition commandPoint;
            if (PlnPusertif101Profile.TryGetByPointKey(feedbackPoint.RelatedCommandPointKey, out commandPoint))
            {
                return commandPoint.Ioa;
            }

            if (PlnPusertif104Profile.TryGetByPointKey(feedbackPoint.RelatedCommandPointKey, out commandPoint))
            {
                return commandPoint.Ioa;
            }

            return null;
        }

        public static int? TryGetRelatedFeedbackIoa(int commandIoa)
        {
            PointDefinition commandPoint;
            if (!TryGetPointByIoa(commandIoa, out commandPoint))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(commandPoint.RelatedFeedbackPointKey))
            {
                return null;
            }

            PointDefinition feedbackPoint;
            if (PlnPusertif101Profile.TryGetByPointKey(commandPoint.RelatedFeedbackPointKey, out feedbackPoint))
            {
                return feedbackPoint.Ioa;
            }

            if (PlnPusertif104Profile.TryGetByPointKey(commandPoint.RelatedFeedbackPointKey, out feedbackPoint))
            {
                return feedbackPoint.Ioa;
            }

            return null;
        }

        public static int? TryGetDefaultCommandIoa(string family)
        {
            PointDefinition point;

            switch (family)
            {
                case "Double":
                    if (PlnPusertif101Profile.TryGetByPointKey("FeederCbCommand", out point))
                    {
                        return point.Ioa;
                    }

                    break;
                case "Regulating":
                    if (PlnPusertif101Profile.TryGetByPointKey("TapChangerRaiseLowerCommand", out point))
                    {
                        return point.Ioa;
                    }

                    break;
            }

            return null;
        }

        private static IEnumerable<PointDefinition> CreatePlnPoints()
        {
            return new[]
            {
                Create("GatewayMpuVsStatus", 8388754, 30, null, "Main Protection Unit / Voltage Status", "TSS", "Binary", "Class 1", "Spont", true, null, null),
                Create("GatewayMainLinkFault", 8388714, 30, "L1FT", "Main Link Fault", "MLK", "Binary", "Class 1", "Spont", true, null, null),
                Create("GatewayBackupLinkFault", 8388715, 30, "L2FT", "Backup Link Fault", "MLK", "Binary", "Class 1", "Spont", true, null, null),
                Create("GatewayMpu1Trip", 8388716, 30, null, "Main Protection Unit 1 Trip", "MLK", "Binary", "Class 1", "Spont", true, null, null),
                Create("GatewayMpu2Trip", 8388717, 30, null, "Main Protection Unit 2 Trip", "MLK", "Binary", "Class 1", "Spont", true, null, null),
                Create("GatewayIedFaulty", 8388725, 30, "IEDF", "IED Faulty", "MLK", "Binary", "Class 1", "Spont", true, null, null),
                Create("FeederCbStatus", 16712689, 31, null, "Feeder CB1 Closed / Opened", "TSD", "Binary", "Class 1", "Spont", true, "FeederCbCommand", null),
                Create("KopelCbStatus", 16712686, 31, null, "Kopel CB2 Closed / Opened", "TSD", "Binary", "Class 1", "Spont", true, "KopelCbCommand", null),
                Create("TrafoCbStatus", 16712704, 31, null, "Trafo CB Closed / Opened", "TSD", "Binary", "Class 1", "Spont", true, "TrafoCbCommand", null),
                Create("FeederLocalRemote", 16712694, 31, null, "Feeder LR1 Local / Remote", "TSD", "Binary", "Class 1", "Spont", true, null, null),
                Create("KopelLocalRemote", 16712701, 31, null, "Kopel LR2 Local / Remote", "TSD", "Binary", "Class 1", "Spont", true, null, null),
                Create("TrafoLocalRemote", 16712708, 31, null, "Trafo LR Local / Remote", "TSD", "Binary", "Class 1", "Spont", true, null, null),
                Create("TapChangerLocalRemote", 16712709, 31, "LRC", "Tap Changer Local / Remote", "TSD", "Binary", "Class 1", "Spont", true, null, null),
                Create("TapChangerAutoManual", 16712710, 31, "TCC", "Tap Changer Auto / Manual", "TSD", "Binary", "Class 1", "Spont", true, null, null),
                Create("TrafoSogiTapPosition", 790448, 32, null, "Tap Position Indication", "TPI", "Analog", "Class 2", "Spont", true, null, "TapChangerRaiseLowerCommand"),
                Create("FeederP1", 790446, 13, null, "Feeder Active Power P1", "TM", "Analog", "Class 2", "Spont", true, null, null),
                Create("FeederQ1", 790447, 11, null, "Feeder Reactive Power Q1", "TM", "Analog", "Class 2", "Spont", true, null, null),
                Create("KopelP2", 790438, 13, null, "Kopel Active Power P2", "TM", "Analog", "Class 2", "Spont", true, null, null),
                Create("KopelQ2", 790439, 11, null, "Kopel Reactive Power Q2", "TM", "Analog", "Class 2", "Spont", true, null, null),
                Create("TrafoP", 790442, 13, null, "Trafo Active Power", "TM", "Analog", "Class 2", "Spont", true, null, null),
                Create("TrafoQ", 790443, 11, null, "Trafo Reactive Power", "TM", "Analog", "Class 2", "Spont", true, null, null),
                Create("RealPowerSettingMeasured", 790449, 9, "POAQ", "Real Power Setting Measured", "RCA", "Analog", "Class 2", "Spont", true, null, "RealPowerSetPointCommand"),
                Create("FeederCbCommand", 68542, 46, null, "Feeder CB1 Double Command", "RCD", "Command", "Class 1", "Act", false, null, "FeederCbStatus"),
                Create("KopelCbCommand", 68539, 46, null, "Kopel CB2 Double Command", "RCD", "Command", "Class 1", "Act", false, null, "KopelCbStatus"),
                Create("TrafoCbCommand", 68550, 46, null, "Trafo CB Double Command", "RCD", "Command", "Class 1", "Act", false, null, "TrafoCbStatus"),
                Create("TapChangerRaiseLowerCommand", 74537, 47, null, "Tap Changer Raise / Lower", "RCD", "Command", "Class 1", "Act", false, null, "TrafoSogiTapPosition"),
                Create("RealPowerSetPointCommand", 70537, 48, "POOP", "Real Power Set Point Command", "RCA", "Command", "Class 1", "Act", false, null, "RealPowerSettingMeasured")
            };
        }

        private static PointDefinition Create(
            string pointKey,
            int ioa,
            int typeId,
            string mnemonic,
            string name,
            string category,
            string valueKind,
            string iecClass,
            string expectedCot,
            bool hasTimestamp,
            string relatedCommandPointKey,
            string relatedFeedbackPointKey)
        {
            return new PointDefinition
            {
                PointKey = pointKey,
                Ioa = ioa,
                TypeId = typeId,
                Mnemonic = mnemonic,
                Name = name,
                Category = category,
                ValueKind = valueKind,
                IecClass = iecClass,
                ExpectedCot = expectedCot,
                HasTimestamp = hasTimestamp,
                RelatedCommandPointKey = relatedCommandPointKey,
                RelatedFeedbackPointKey = relatedFeedbackPointKey
            };
        }
    }
}
