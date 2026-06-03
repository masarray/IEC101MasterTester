using System;
using System.Linq;
using System.Text;
using lib60870.CS101;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101.Native;
using IEC101MasterTester.Services.Iec101.Native.Asdu;
using IEC101MasterTester.Services.Iec101.Native.Frames;

namespace IEC101MasterTester.Services.Iec101
{
    public sealed class LineMonitorFormatter
    {
        public LineMonitorRow FromRawMessage(string direction, byte[] message, int messageSize)
        {
            return FromRawMessage(direction, message, messageSize, null);
        }

        public LineMonitorRow FromRawMessage(string direction, byte[] message, int messageSize, ConnectionSettings settings)
        {
            byte[] payload = message ?? Array.Empty<byte>();
            int length = Math.Max(0, Math.Min(messageSize, payload.Length));

            LineMonitorRow nativeRow = TryCreateNativeRawRow(direction, payload, length, settings);
            if (nativeRow != null)
            {
                return nativeRow;
            }

            string frameType = DetectFrameType(payload, length);

            return new LineMonitorRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = direction,
                FrameType = frameType,
                Summary = BuildRawSummary(frameType, payload, length),
                ControlFc = BuildControlDetails(payload, length),
                ACD = ExtractSecondaryStatusBit(payload, length, 5),
                DFC = ExtractSecondaryStatusBit(payload, length, 4),
                AsduType = TryExtractTypeId(payload, length),
                COT = "-",
                CASDU = "-",
                IOA = TryExtractIoaFromRawMessage(payload, length),
                RawHex = ToHex(payload, length),
                Detail = BuildRawDetail(payload, length)
            };
        }

        private static LineMonitorRow TryCreateNativeRawRow(string direction, byte[] payload, int length, ConnectionSettings settings)
        {
            Iec101ApplicationProfile profile = settings == null
                ? Iec101ApplicationProfile.DefaultPln101()
                : Iec101ApplicationProfile.FromSettings(settings);

            Iec101Frame frame;
            string error;
            if (!Iec101FrameCodec.TryParse(payload, length, profile, out frame, out error))
            {
                if (length <= 0)
                {
                    return null;
                }

                return new LineMonitorRow
                {
                    Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Direction = direction,
                    FrameType = DetectFrameType(payload, length),
                    Summary = "Native decode warning",
                    ControlFc = "-",
                    ACD = "-",
                    DFC = "-",
                    AsduType = "-",
                    COT = "-",
                    CASDU = "-",
                    IOA = "-",
                    RawHex = ToHex(payload, length),
                    Detail = error ?? "Unknown native decode error"
                };
            }

            Iec101Asdu nativeAsdu = null;
            string asduError = null;
            byte[] asduBytes = frame.GetAsduBytesOrEmpty();
            if (asduBytes.Length > 0)
            {
                Iec101AsduCodec.TryParse(asduBytes, profile, out nativeAsdu, out asduError);
            }

            string frameType = ToFrameTypeText(frame.FrameType);
            string linkText = frame.LinkAddress.HasValue ? frame.LinkAddress.Value.ToString() : "-";
            string controlText = frame.Control == null ? "-" : frame.Control.Describe();
            Iec101InformationObject firstObject = nativeAsdu != null && nativeAsdu.Objects.Count > 0 ? nativeAsdu.Objects[0] : null;
            string ioa = firstObject == null ? "-" : firstObject.ObjectAddress.ToString();
            string asduType = nativeAsdu == null ? "-" : nativeAsdu.TypeId.ToString();
            string cot = nativeAsdu == null ? "-" : ToNativeCotText(nativeAsdu);
            string casdu = nativeAsdu == null ? "-" : nativeAsdu.CommonAddress.ToString();

            string summary = BuildNativeSummary(frameType, frame, nativeAsdu, firstObject);
            string detail = BuildNativeDetail(length, linkText, nativeAsdu, firstObject, asduError);

            return new LineMonitorRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = direction,
                FrameType = frameType,
                Summary = summary,
                ControlFc = controlText,
                ACD = frame.Control != null && !frame.Control.IsPrimary ? (frame.Control.Acd ? "1" : "0") : "-",
                DFC = frame.Control != null && !frame.Control.IsPrimary ? (frame.Control.Dfc ? "1" : "0") : "-",
                AsduType = asduType,
                COT = cot,
                CASDU = casdu,
                IOA = ioa,
                RawHex = ToHex(payload, length),
                Detail = detail
            };
        }

        public LineMonitorRow FromAsdu(string direction, ASDU asdu)
        {
            byte[] bytes = asdu.AsByteArray() ?? Array.Empty<byte>();

            string ioa = "-";
            string detail = string.Format("Sequence={0}, Elements={1}", asdu.IsSequence ? "1" : "0", asdu.NumberOfElements);

            try
            {
                if (asdu.NumberOfElements > 0)
                {
                    InformationObject informationObject = asdu.GetElement(0);
                    if (informationObject != null)
                    {
                        ioa = informationObject.ObjectAddress.ToString();
                        detail += ", IOA " + ioa;

                        string commandInfo = TryDescribeCommandInformation(asdu, informationObject);
                        if (!string.IsNullOrWhiteSpace(commandInfo))
                        {
                            detail += ", " + commandInfo;
                        }
                    }
                }
            }
            catch
            {
                // keep formatter safe
            }

            return new LineMonitorRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = direction,
                FrameType = "ASDU",
                Summary = string.Format("{0}, {1} object(s), COT={2}, CA={3}", ToReadableType(asdu.TypeId), asdu.NumberOfElements, ToReadableCot(asdu.Cot), asdu.Ca),
                ControlFc = string.Format("OA={0}, NEG={1}, TEST={2}", asdu.Oa, asdu.IsNegative ? "1" : "0", asdu.IsTest ? "1" : "0"),
                ACD = "-",
                DFC = "-",
                AsduType = asdu.TypeId.ToString(),
                COT = ToReadableCot(asdu.Cot),
                CASDU = asdu.Ca.ToString(),
                IOA = ioa,
                RawHex = ToHex(bytes, bytes.Length),
                Detail = detail
            };
        }

        public LineMonitorRow CreateSystemRow(string direction, string summary, string detail)
        {
            return new LineMonitorRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = direction,
                FrameType = "-",
                Summary = summary,
                ControlFc = "-",
                ACD = "-",
                DFC = "-",
                AsduType = "-",
                COT = "-",
                CASDU = "-",
                IOA = "-",
                RawHex = string.Empty,
                Detail = detail ?? string.Empty
            };
        }

        private static string DetectFrameType(byte[] message, int length)
        {
            if (length <= 0)
            {
                return "Empty";
            }

            switch (message[0])
            {
                case 0x10:
                    return length >= 5 ? "Fixed" : "Fixed?";
                case 0x68:
                    return length >= 6 ? "Variable" : "Variable?";
                case 0xE5:
                    return "Single Char";
                default:
                    return "Unknown";
            }
        }

        private static string ToFrameTypeText(Iec101FrameType frameType)
        {
            switch (frameType)
            {
                case Iec101FrameType.SingleCharacterAck:
                    return "Single Char";
                case Iec101FrameType.Fixed:
                    return "Fixed";
                case Iec101FrameType.Variable:
                    return "Variable";
                default:
                    return "Unknown";
            }
        }

        private static string BuildNativeSummary(string frameType, Iec101Frame frame, Iec101Asdu asdu, Iec101InformationObject firstObject)
        {
            if (frame == null)
            {
                return "No data";
            }

            if (frame.FrameType == Iec101FrameType.SingleCharacterAck)
            {
                return "Single-character ACK";
            }

            if (asdu != null)
            {
                string objectText = asdu.ObjectCount == 1 ? "1 object" : asdu.ObjectCount + " objects";
                string valueText = firstObject == null || string.IsNullOrWhiteSpace(firstObject.ValueText)
                    ? string.Empty
                    : ", " + firstObject.ValueText;
                return string.Format("{0}, {1}, COT={2}, CA={3}{4}", asdu.TypeId, objectText, ToNativeCotText(asdu), asdu.CommonAddress, valueText);
            }

            string linkText = frame.LinkAddress.HasValue ? " to link " + frame.LinkAddress.Value : string.Empty;
            string controlText = frame.Control == null ? "-" : frame.Control.Describe();
            return string.Format("{0} frame{1}, {2}", frameType, linkText, controlText);
        }

        private static string BuildNativeDetail(int length, string linkAddress, Iec101Asdu asdu, Iec101InformationObject firstObject, string asduError)
        {
            StringBuilder detail = new StringBuilder();
            detail.Append("NativeDecode=1");
            detail.Append(", Length=").Append(length);
            detail.Append(", LinkAddress=").Append(linkAddress);

            if (asdu != null)
            {
                detail.Append(", VSQ=0x").Append(asdu.VariableStructureQualifier.ToString("X2"));
                detail.Append(", Sequence=").Append(asdu.IsSequence ? 1 : 0);
                detail.Append(", OA=").Append(asdu.OriginatorAddress);
                detail.Append(", TEST=").Append(asdu.IsTest ? 1 : 0);
                detail.Append(", NEG=").Append(asdu.IsNegativeConfirm ? 1 : 0);
            }

            if (firstObject != null)
            {
                detail.Append(", IOA ").Append(firstObject.ObjectAddress);
                if (firstObject.Quality != null)
                {
                    detail.Append(", Quality=").Append(firstObject.Quality.ToOperatorText());
                }

                if (firstObject.TimestampUtc.HasValue)
                {
                    detail.Append(", TimestampUtc=").Append(firstObject.TimestampUtc.Value.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                }
            }

            if (!string.IsNullOrWhiteSpace(asduError))
            {
                detail.Append(", ASDUWarning=").Append(asduError);
            }

            return detail.ToString();
        }

        private static string ToNativeCotText(Iec101Asdu asdu)
        {
            if (asdu == null)
            {
                return "-";
            }

            return asdu.Cause == Iec101CauseOfTransmission.Unknown
                ? "COT" + asdu.CauseRaw
                : asdu.Cause.ToString();
        }

        private static string BuildRawSummary(string frameType, byte[] message, int length)
        {
            if (frameType == "Empty")
            {
                return "No data";
            }

            if (frameType == "Single Char")
            {
                return "Single-character ACK";
            }

            int? address = TryExtractLinkAddress(message, length);
            string control = BuildControlDetails(message, length);
            return address.HasValue
                ? string.Format("{0} frame to link {1}, {2}", frameType, address.Value, control)
                : string.Format("{0} frame, {1}", frameType, control);
        }

        private static string BuildRawDetail(byte[] message, int length)
        {
            if (length <= 0)
            {
                return string.Empty;
            }

            string detail = string.Format("Length={0}", length);
            int? address = TryExtractLinkAddress(message, length);
            if (address.HasValue)
            {
                detail += ", LinkAddress=" + address.Value;
            }

            string ioa = TryExtractIoaFromRawMessage(message, length);
            if (!string.IsNullOrWhiteSpace(ioa) && ioa != "-")
            {
                detail += ", IOA " + ioa;
            }

            return detail;
        }

        private static string BuildControlDetails(byte[] message, int length)
        {
            int? controlIndex = TryGetControlIndex(message, length);
            if (!controlIndex.HasValue)
            {
                return "-";
            }

            byte control = message[controlIndex.Value];
            bool prm = (control & 0x40) != 0;
            bool dir = (control & 0x80) != 0;
            int fc = control & 0x0F;

            if (prm)
            {
                bool fcb = (control & 0x20) != 0;
                bool fcv = (control & 0x10) != 0;
                return string.Format(
                    "0x{0:X2} PRM=1 DIR={1} FCB={2} FCV={3} {4}",
                    control,
                    dir ? 1 : 0,
                    fcb ? 1 : 0,
                    fcv ? 1 : 0,
                    DescribePrimaryFunction(fc));
            }

            bool acd = (control & 0x20) != 0;
            bool dfc = (control & 0x10) != 0;
            return string.Format(
                "0x{0:X2} PRM=0 DIR={1} ACD={2} DFC={3} {4}",
                control,
                dir ? 1 : 0,
                acd ? 1 : 0,
                dfc ? 1 : 0,
                DescribeSecondaryFunction(fc));
        }

        private static string ExtractSecondaryStatusBit(byte[] message, int length, int bit)
        {
            int? controlIndex = TryGetControlIndex(message, length);
            if (!controlIndex.HasValue)
            {
                return "-";
            }

            byte control = message[controlIndex.Value];
            bool prm = (control & 0x40) != 0;
            if (prm)
            {
                return "-";
            }

            return ((control >> bit) & 0x01) == 1 ? "1" : "0";
        }

        private static int? TryGetControlIndex(byte[] message, int length)
        {
            if (length < 2)
            {
                return null;
            }

            if (message[0] == 0x68 && length >= 5)
            {
                return 4;
            }

            return 1;
        }

        private static int? TryExtractLinkAddress(byte[] message, int length)
        {
            if (length < 3)
            {
                return null;
            }

            if (message[0] == 0x10)
            {
                return message[2];
            }

            if (message[0] == 0x68 && length >= 7)
            {
                if (LooksLikeTypeId(message[6]) && !LooksLikeTypeId(message[5]))
                {
                    return message[5];
                }

                return message[5] | (message[6] << 8);
            }

            return null;
        }

        private static string TryExtractTypeId(byte[] message, int length)
        {
            if (message == null || length < 8 || message[0] != 0x68)
            {
                return "-";
            }

            int typeIndex = 6;
            if (!LooksLikeTypeId(message[typeIndex]) && length > 7 && LooksLikeTypeId(message[7]))
            {
                typeIndex = 7;
            }

            if (typeIndex >= length - 2)
            {
                return "-";
            }

            byte rawType = message[typeIndex];
            if (Enum.IsDefined(typeof(TypeID), (int)rawType))
            {
                return ((TypeID)rawType).ToString();
            }

            return "0x" + rawType.ToString("X2");
        }

        private static string TryExtractIoaFromRawMessage(byte[] message, int length)
        {
            // Raw frame parsing is unreliable because link address size varies.
            // IOA should be taken from ASDU parsing instead.
            return "-";
        }

        private static string TryDescribeCommandInformation(ASDU asdu, InformationObject informationObject)
        {
            if (asdu == null || informationObject == null)
            {
                return string.Empty;
            }

            try
            {
                if (asdu.TypeId == TypeID.C_SC_NA_1)
                {
                    SingleCommand cmd = informationObject as SingleCommand;
                    if (cmd != null)
                    {
                        return string.Format("ON={0}, Select={1}", cmd.State ? 1 : 0, cmd.Select ? 1 : 0);
                    }
                }

                if (asdu.TypeId == TypeID.C_DC_NA_1)
                {
                    DoubleCommand cmd = informationObject as DoubleCommand;
                    if (cmd != null)
                    {
                        string state = cmd.State == DoubleCommand.ON ? "CLOSE" :
                                       cmd.State == DoubleCommand.OFF ? "OPEN" : cmd.State.ToString();
                        return string.Format("State={0}, Select={1}", state, cmd.Select ? 1 : 0);
                    }
                }

                if (asdu.TypeId == TypeID.C_RC_NA_1)
                {
                    StepCommand cmd = informationObject as StepCommand;
                    if (cmd != null)
                    {
                        string state = cmd.State == StepCommandValue.HIGHER ? "RAISE" :
                                       cmd.State == StepCommandValue.LOWER ? "LOWER" : cmd.State.ToString();
                        return string.Format("State={0}, Select={1}", state, cmd.Select ? 1 : 0);
                    }
                }

                if (asdu.TypeId == TypeID.C_SE_NA_1)
                {
                    SetpointCommandNormalized cmd = informationObject as SetpointCommandNormalized;
                    if (cmd != null)
                    {
                        return string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "Value={0:0.###}, Select={1}",
                            cmd.NormalizedValue,
                            cmd.QOS.Select ? 1 : 0);
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static bool LooksLikeTypeId(byte value)
        {
            return Enum.IsDefined(typeof(TypeID), (int)value);
        }

        private static string DescribePrimaryFunction(int fc)
        {
            switch (fc)
            {
                case 0: return "Reset remote link";
                case 1: return "Reset user process";
                case 2: return "Test function for link";
                case 3: return "User data confirmed";
                case 4: return "User data no reply";
                case 8: return "Reset FCB";
                case 9: return "Request status of link";
                case 10: return "Request user data class 1";
                case 11: return "Request user data class 2";
                default: return "FC=" + fc;
            }
        }

        private static string DescribeSecondaryFunction(int fc)
        {
            switch (fc)
            {
                case 0: return "ACK";
                case 1: return "NACK";
                case 8: return "User data";
                case 9: return "No data available";
                case 11: return "Link status";
                default: return "FC=" + fc;
            }
        }

        private static string ToReadableType(TypeID type)
        {
            switch (type)
            {
                case TypeID.M_SP_NA_1: return "Single-point";
                case TypeID.M_DP_NA_1: return "Double-point";
                case TypeID.M_ME_NA_1: return "Measured normalized";
                case TypeID.M_ME_NB_1: return "Measured scaled";
                case TypeID.M_ME_NC_1: return "Measured short";
                case TypeID.M_ME_TE_1: return "Measured scaled CP56Time2a";
                case TypeID.M_ME_TF_1: return "Measured short CP56Time2a";
                case TypeID.M_SP_TB_1: return "Single-point CP56Time2a";
                case TypeID.M_IT_NA_1: return "Integrated totals";
                case TypeID.C_IC_NA_1: return "General interrogation command";
                case TypeID.C_CS_NA_1: return "Clock sync command";
                case TypeID.C_SC_NA_1: return "Single command";
                case TypeID.C_DC_NA_1: return "Double command";
                case TypeID.C_RC_NA_1: return "Regulating command";
                default: return type.ToString();
            }
        }

        private static string ToReadableCot(CauseOfTransmission cot)
        {
            return cot.ToString().Replace('_', ' ');
        }

        private static string ToHex(byte[] data, int length)
        {
            if (data == null || length <= 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(length * 3);
            foreach (string value in data.Take(length).Select(b => b.ToString("X2")))
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(value);
            }

            return builder.ToString();
        }
    }
}
