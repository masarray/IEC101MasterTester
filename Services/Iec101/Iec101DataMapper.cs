using System;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101.Native.Asdu;
using IEC101MasterTester.Services.Profiles;

namespace IEC101MasterTester.Services.Iec101
{
    public sealed class Iec101DataMapper
    {
        public ValueViewerRow Map(Iec101Asdu asdu, Iec101InformationObject informationObject)
        {
            if (asdu == null || informationObject == null)
            {
                return null;
            }

            string type = ToNativeDisplayType(asdu.TypeId);
            string value = string.IsNullOrWhiteSpace(informationObject.ValueText) ? "Unknown" : informationObject.ValueText;
            string quality = informationObject.Quality == null ? "Good" : informationObject.Quality.ToOperatorText();
            bool hasProtocolTimestamp = informationObject.TimestampUtc.HasValue;

            return CreateNativeRow(
                asdu,
                informationObject.ObjectAddress,
                type,
                value,
                quality,
                informationObject.TimestampUtc,
                hasProtocolTimestamp,
                GetNativeSourceType(asdu.Cause));
        }

        private static ValueViewerRow CreateNativeRow(Iec101Asdu asdu, int ioa, string type, string value, string quality, DateTime? eventTimestampUtc, bool hasProtocolTimestamp, string sourceType)
        {
            PointDefinition pointDefinition;
            OfficialPointProfiles.TryGetPointByIoa(ioa, out pointDefinition);

            string timestampText = hasProtocolTimestamp && eventTimestampUtc.HasValue
                ? eventTimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff")
                : "-";

            return new ValueViewerRow
            {
                IOA = ioa,
                Name = OfficialPointProfiles.GetDisplayNameOrDefault(ioa, null),
                Type = type,
                TypeId = asdu.TypeId == Iec101TypeId.Unknown ? "0x" + asdu.TypeIdRaw.ToString("X2") : asdu.TypeId.ToString(),
                TypeIdRaw = asdu.TypeIdRaw,
                Casdu = asdu.CommonAddress.ToString(),
                Value = value,
                Quality = quality,
                Timestamp = timestampText,
                ReceiveTimestampUtc = DateTime.UtcNow,
                ReceiveTimestampText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EventTimestampUtc = hasProtocolTimestamp ? eventTimestampUtc : null,
                SnapshotTimestampUtc = hasProtocolTimestamp ? (DateTime?)null : DateTime.UtcNow,
                HasProtocolTimestamp = hasProtocolTimestamp,
                SourceType = sourceType,
                Acd = "-",
                Cot = ToNativeShortCot(asdu.Cause, asdu.CauseRaw),
                CotRaw = asdu.CauseRaw,
                TrafficClass = "Unknown",
                PointKey = pointDefinition != null ? pointDefinition.PointKey : OfficialPointProfiles.TryGetPointKey(ioa)
            };
        }

        private static string GetNativeSourceType(Iec101CauseOfTransmission cot)
        {
            switch (cot)
            {
                case Iec101CauseOfTransmission.Spontaneous:
                    return "SPONT";
                case Iec101CauseOfTransmission.InterrogatedByStation:
                    return "GI";
                case Iec101CauseOfTransmission.BackgroundScan:
                    return "C2";
                case Iec101CauseOfTransmission.Periodic:
                    return "C1";
                default:
                    return "UNKNOWN";
            }
        }

        private static string ToNativeShortCot(Iec101CauseOfTransmission cot, int raw)
        {
            switch (cot)
            {
                case Iec101CauseOfTransmission.Periodic:
                    return "Periodic";
                case Iec101CauseOfTransmission.BackgroundScan:
                    return "BgScan";
                case Iec101CauseOfTransmission.Spontaneous:
                    return "Spont";
                case Iec101CauseOfTransmission.InterrogatedByStation:
                    return "GI";
                case Iec101CauseOfTransmission.Activation:
                    return "Act";
                case Iec101CauseOfTransmission.ActivationCon:
                    return "ActCon";
                case Iec101CauseOfTransmission.ActivationTermination:
                    return "ActTerm";
                case Iec101CauseOfTransmission.Request:
                    return "Req";
                case Iec101CauseOfTransmission.Initialized:
                    return "Init";
                default:
                    return "COT" + raw;
            }
        }

        private static string ToNativeDisplayType(Iec101TypeId typeId)
        {
            switch (typeId)
            {
                case Iec101TypeId.M_SP_NA_1:
                case Iec101TypeId.M_SP_TA_1:
                case Iec101TypeId.M_SP_TB_1:
                    return "Single Point";
                case Iec101TypeId.M_DP_NA_1:
                case Iec101TypeId.M_DP_TA_1:
                case Iec101TypeId.M_DP_TB_1:
                    return "Double Point";
                case Iec101TypeId.M_ME_NA_1:
                case Iec101TypeId.M_ME_TA_1:
                case Iec101TypeId.M_ME_TD_1:
                    return "Measured Normalized";
                case Iec101TypeId.M_ME_NB_1:
                case Iec101TypeId.M_ME_TB_1:
                case Iec101TypeId.M_ME_TE_1:
                    return "Measured Scaled";
                case Iec101TypeId.M_ME_NC_1:
                case Iec101TypeId.M_ME_TC_1:
                case Iec101TypeId.M_ME_TF_1:
                    return "Measured Short";
                case Iec101TypeId.M_ST_NA_1:
                case Iec101TypeId.M_ST_TB_1:
                    return "Step Position";
                case Iec101TypeId.M_IT_NA_1:
                case Iec101TypeId.M_IT_TB_1:
                    return "Integrated Total";
                default:
                    return typeId == Iec101TypeId.Unknown ? "Unknown" : typeId.ToString();
            }
        }
    }
}

