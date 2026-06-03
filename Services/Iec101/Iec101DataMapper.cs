using System;
using lib60870;
using lib60870.CS101;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101.Native.Asdu;
using IEC101MasterTester.Services.Profiles;

namespace IEC101MasterTester.Services.Iec101
{
    public sealed class Iec101DataMapper
    {
        public ValueViewerRow Map(ASDU asdu, InformationObject informationObject)
        {
            if (asdu == null || informationObject == null)
            {
                return null;
            }

            DateTime? eventTimestampUtc = null;
            bool hasProtocolTimestamp = false;
            string sourceType = GetSourceType(asdu.Cot);

            if (informationObject is SinglePointWithCP56Time2a singlePointWithTime)
            {
                eventTimestampUtc = ToUtc(singlePointWithTime.Timestamp);
                hasProtocolTimestamp = eventTimestampUtc.HasValue;
                return CreateRow(asdu, singlePointWithTime.ObjectAddress, "Single Point", singlePointWithTime.Value ? "ON" : "OFF", FormatQuality(singlePointWithTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is SinglePointWithCP24Time2a singlePointWithShortTime)
            {
                eventTimestampUtc = singlePointWithShortTime.Timestamp == null ? (DateTime?)null : DateTime.UtcNow;
                hasProtocolTimestamp = true;
                return CreateRow(asdu, singlePointWithShortTime.ObjectAddress, "Single Point", singlePointWithShortTime.Value ? "ON" : "OFF", FormatQuality(singlePointWithShortTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is SinglePointInformation singlePoint)
            {
                return CreateRow(asdu, singlePoint.ObjectAddress, "Single Point", singlePoint.Value ? "ON" : "OFF", FormatQuality(singlePoint.Quality), null, false, sourceType);
            }

            if (informationObject is DoublePointWithCP56Time2a doublePointWithTime)
            {
                eventTimestampUtc = ToUtc(doublePointWithTime.Timestamp);
                hasProtocolTimestamp = eventTimestampUtc.HasValue;
                return CreateRow(asdu, doublePointWithTime.ObjectAddress, "Double Point", FormatDoublePointValue(doublePointWithTime.Value), FormatQuality(doublePointWithTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is DoublePointWithCP24Time2a doublePointWithShortTime)
            {
                eventTimestampUtc = doublePointWithShortTime.Timestamp == null ? (DateTime?)null : DateTime.UtcNow;
                hasProtocolTimestamp = true;
                return CreateRow(asdu, doublePointWithShortTime.ObjectAddress, "Double Point", FormatDoublePointValue(doublePointWithShortTime.Value), FormatQuality(doublePointWithShortTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is DoublePointInformation doublePoint)
            {
                return CreateRow(asdu, doublePoint.ObjectAddress, "Double Point", FormatDoublePointValue(doublePoint.Value), FormatQuality(doublePoint.Quality), null, false, sourceType);
            }

            if (informationObject is MeasuredValueNormalized normalized)
            {
                return CreateRow(asdu, normalized.ObjectAddress, "Measured Normalized", normalized.NormalizedValue.ToString("0.###"), FormatQuality(normalized.Quality), null, false, sourceType);
            }

            if (informationObject is MeasuredValueNormalizedWithCP56Time2a normalizedWithTime)
            {
                eventTimestampUtc = ToUtc(normalizedWithTime.Timestamp);
                hasProtocolTimestamp = eventTimestampUtc.HasValue;
                return CreateRow(asdu, normalizedWithTime.ObjectAddress, "Measured Normalized", normalizedWithTime.NormalizedValue.ToString("0.###"), FormatQuality(normalizedWithTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is MeasuredValueNormalizedWithCP24Time2a normalizedWithShortTime)
            {
                eventTimestampUtc = normalizedWithShortTime.Timestamp == null ? (DateTime?)null : DateTime.UtcNow;
                hasProtocolTimestamp = true;
                return CreateRow(asdu, normalizedWithShortTime.ObjectAddress, "Measured Normalized", normalizedWithShortTime.NormalizedValue.ToString("0.###"), FormatQuality(normalizedWithShortTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is MeasuredValueScaled scaled)
            {
                return CreateRow(asdu, scaled.ObjectAddress, "Measured Scaled", scaled.ScaledValue.Value.ToString(), FormatQuality(scaled.Quality), null, false, sourceType);
            }

            if (informationObject is MeasuredValueScaledWithCP56Time2a scaledWithTime)
            {
                eventTimestampUtc = ToUtc(scaledWithTime.Timestamp);
                hasProtocolTimestamp = eventTimestampUtc.HasValue;
                return CreateRow(asdu, scaledWithTime.ObjectAddress, "Measured Scaled", scaledWithTime.ScaledValue.Value.ToString(), FormatQuality(scaledWithTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is MeasuredValueScaledWithCP24Time2a scaledWithShortTime)
            {
                eventTimestampUtc = scaledWithShortTime.Timestamp == null ? (DateTime?)null : DateTime.UtcNow;
                hasProtocolTimestamp = true;
                return CreateRow(asdu, scaledWithShortTime.ObjectAddress, "Measured Scaled", scaledWithShortTime.ScaledValue.Value.ToString(), FormatQuality(scaledWithShortTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is MeasuredValueShort shortValue)
            {
                return CreateRow(asdu, shortValue.ObjectAddress, "Measured Short", shortValue.Value.ToString("0.###"), FormatQuality(shortValue.Quality), null, false, sourceType);
            }

            if (informationObject is MeasuredValueShortWithCP56Time2a shortWithTime)
            {
                eventTimestampUtc = ToUtc(shortWithTime.Timestamp);
                hasProtocolTimestamp = eventTimestampUtc.HasValue;
                return CreateRow(asdu, shortWithTime.ObjectAddress, "Measured Short", shortWithTime.Value.ToString("0.###"), FormatQuality(shortWithTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is MeasuredValueShortWithCP24Time2a shortWithShortTime)
            {
                eventTimestampUtc = shortWithShortTime.Timestamp == null ? (DateTime?)null : DateTime.UtcNow;
                hasProtocolTimestamp = true;
                return CreateRow(asdu, shortWithShortTime.ObjectAddress, "Measured Short", shortWithShortTime.Value.ToString("0.###"), FormatQuality(shortWithShortTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is StepPositionWithCP56Time2a stepWithTime)
            {
                eventTimestampUtc = ToUtc(stepWithTime.Timestamp);
                hasProtocolTimestamp = eventTimestampUtc.HasValue;
                return CreateRow(asdu, stepWithTime.ObjectAddress, "Step Position", stepWithTime.Value.ToString(), FormatQuality(stepWithTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is StepPositionWithCP24Time2a stepWithShortTime)
            {
                eventTimestampUtc = stepWithShortTime.Timestamp == null ? (DateTime?)null : DateTime.UtcNow;
                hasProtocolTimestamp = true;
                return CreateRow(asdu, stepWithShortTime.ObjectAddress, "Step Position", stepWithShortTime.Value.ToString(), FormatQuality(stepWithShortTime.Quality), eventTimestampUtc, hasProtocolTimestamp, sourceType);
            }

            if (informationObject is StepPositionInformation step)
            {
                return CreateRow(asdu, step.ObjectAddress, "Step Position", step.Value.ToString(), FormatQuality(step.Quality), null, false, sourceType);
            }

            if (informationObject is IntegratedTotals totals)
            {
                return CreateRow(asdu, totals.ObjectAddress, "Integrated Total", totals.BCR.Value.ToString(), FormatCounterQuality(totals.BCR), null, false, sourceType);
            }

            return CreateRow(asdu, informationObject.ObjectAddress, informationObject.Type.ToString(), informationObject.ToString(), "Other", null, false, sourceType);
        }

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

        private static ValueViewerRow CreateRow(ASDU asdu, int ioa, string type, string value, string quality, DateTime? eventTimestampUtc, bool hasProtocolTimestamp, string sourceType)
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
                TypeId = asdu.TypeId.ToString(),
                TypeIdRaw = (int)asdu.TypeId,
                Casdu = asdu.Ca.ToString(),
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
                Cot = ToShortCot(asdu.Cot),
                CotRaw = (int)asdu.Cot,
                TrafficClass = "Unknown",
                PointKey = pointDefinition != null ? pointDefinition.PointKey : OfficialPointProfiles.TryGetPointKey(ioa)
            };
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

        private static DateTime? ToUtc(CP56Time2a timestamp)
        {
            if (timestamp == null)
            {
                return null;
            }

            try
            {
                return timestamp.GetDateTime().ToUniversalTime();
            }
            catch
            {
                return null;
            }
        }

        private static string GetSourceType(CauseOfTransmission cot)
        {
            switch (cot)
            {
                case CauseOfTransmission.SPONTANEOUS:
                    return "SPONT";
                case CauseOfTransmission.INTERROGATED_BY_STATION:
                    return "GI";
                case CauseOfTransmission.BACKGROUND_SCAN:
                    return "C2";
                case CauseOfTransmission.PERIODIC:
                    return "C1";
                default:
                    return "UNKNOWN";
            }
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

        private static string FormatQuality(QualityDescriptor quality)
        {
            if (quality == null)
            {
                return "Good";
            }

            if (quality.Invalid)
            {
                return "Invalid";
            }

            if (quality.Blocked)
            {
                return "Blocked";
            }

            if (quality.Substituted)
            {
                return "Subst";
            }

            if (quality.NonTopical)
            {
                return "Old";
            }

            if (quality.Overflow)
            {
                return "Over";
            }

            return "Good";
        }

        private static string FormatCounterQuality(BinaryCounterReading reading)
        {
            if (reading.Invalid)
            {
                return "Invalid";
            }

            if (reading.Adjusted)
            {
                return "Adj";
            }

            if (reading.Carry)
            {
                return "Carry";
            }

            return "Good";
        }

        private static string FormatDoublePointValue(DoublePointValue value)
        {
            switch (value)
            {
                case DoublePointValue.INTERMEDIATE:
                    return "INVALID 0";
                case DoublePointValue.OFF:
                    return "OPEN";
                case DoublePointValue.ON:
                    return "CLOSE";
                case DoublePointValue.INDETERMINATE:
                    return "INVALID 1";
                default:
                    return value.ToString().ToUpperInvariant();
            }
        }

        private static string FormatTimestamp(CP56Time2a timestamp)
        {
            if (timestamp == null)
            {
                return "-";
            }

            try
            {
                return timestamp.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
            catch
            {
                return timestamp.ToString();
            }
        }

        private static string FormatTimestamp(CP24Time2a timestamp)
        {
            if (timestamp == null)
            {
                return "-";
            }

            return timestamp.ToString();
        }

        private static string ToShortCot(CauseOfTransmission cot)
        {
            switch (cot)
            {
                case CauseOfTransmission.PERIODIC:
                    return "Periodic";
                case CauseOfTransmission.BACKGROUND_SCAN:
                    return "BgScan";
                case CauseOfTransmission.SPONTANEOUS:
                    return "Spont";
                case CauseOfTransmission.INTERROGATED_BY_STATION:
                    return "GI";
                case CauseOfTransmission.ACTIVATION:
                    return "Act";
                case CauseOfTransmission.ACTIVATION_CON:
                    return "ActCon";
                case CauseOfTransmission.ACTIVATION_TERMINATION:
                    return "ActTerm";
                case CauseOfTransmission.REQUEST:
                    return "Req";
                case CauseOfTransmission.INITIALIZED:
                    return "Init";
                default:
                    return cot.ToString();
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

