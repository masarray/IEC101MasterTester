using System;
using System.Collections.Generic;

namespace IEC101MasterTester.Services.Iec101.Native.Asdu
{
    public static class Iec101AsduCodec
    {
        public static bool TryParse(byte[] bytes, Iec101ApplicationProfile profile, out Iec101Asdu asdu, out string error)
        {
            asdu = null;
            error = null;
            if (bytes == null)
            {
                error = "ASDU bytes are null.";
                return false;
            }

            Iec101ApplicationProfile effectiveProfile = profile ?? Iec101ApplicationProfile.DefaultPln101();
            int minimumHeaderLength = 2 + effectiveProfile.CotLength + effectiveProfile.CasduLength;
            if (bytes.Length < minimumHeaderLength)
            {
                error = string.Format("ASDU is too short. Expected at least {0} bytes, got {1}.", minimumHeaderLength, bytes.Length);
                return false;
            }

            int offset = 0;
            int typeIdRaw = bytes[offset++];
            byte vsq = bytes[offset++];
            int causeByte = bytes[offset++];
            int originator = 0;
            if (effectiveProfile.CotLength > 1)
            {
                originator = bytes[offset++];
            }

            int commonAddress = ReadUnsignedLittleEndian(bytes, offset, effectiveProfile.CasduLength);
            offset += effectiveProfile.CasduLength;

            asdu = new Iec101Asdu
            {
                TypeIdRaw = typeIdRaw,
                TypeId = ToTypeId(typeIdRaw),
                VariableStructureQualifier = vsq,
                IsSequence = (vsq & 0x80) != 0,
                ObjectCount = vsq & 0x7F,
                CauseRaw = causeByte & 0x3F,
                Cause = ToCause(causeByte & 0x3F),
                IsNegativeConfirm = (causeByte & 0x40) != 0,
                IsTest = (causeByte & 0x80) != 0,
                OriginatorAddress = originator,
                CommonAddress = commonAddress,
                RawBytes = Copy(bytes)
            };

            TryParseObjects(bytes, offset, effectiveProfile, asdu);
            return true;
        }

        public static byte[] EncodeInterrogationCommand(int casdu, int ioa, byte qualifier, Iec101ApplicationProfile profile)
        {
            return EncodeSingleObjectAsdu(Iec101TypeId.C_IC_NA_1, Iec101CauseOfTransmission.Activation, casdu, ioa, new byte[] { qualifier }, profile);
        }

        public static byte[] EncodeSingleCommand(int casdu, int ioa, bool state, bool select, int quality, Iec101ApplicationProfile profile)
        {
            int sco = (state ? 0x01 : 0x00) | (select ? 0x80 : 0x00) | ((quality & 0x1F) << 2);
            return EncodeSingleObjectAsdu(Iec101TypeId.C_SC_NA_1, Iec101CauseOfTransmission.Activation, casdu, ioa, new byte[] { (byte)sco }, profile);
        }

        public static byte[] EncodeDoubleCommand(int casdu, int ioa, bool on, bool select, int quality, Iec101ApplicationProfile profile)
        {
            int dco = (on ? 0x02 : 0x01) | (select ? 0x80 : 0x00) | ((quality & 0x1F) << 2);
            return EncodeSingleObjectAsdu(Iec101TypeId.C_DC_NA_1, Iec101CauseOfTransmission.Activation, casdu, ioa, new byte[] { (byte)dco }, profile);
        }

        public static byte[] EncodeStepCommand(int casdu, int ioa, bool raise, bool select, int quality, Iec101ApplicationProfile profile)
        {
            int rco = (raise ? 0x01 : 0x02) | (select ? 0x80 : 0x00) | ((quality & 0x1F) << 2);
            return EncodeSingleObjectAsdu(Iec101TypeId.C_RC_NA_1, Iec101CauseOfTransmission.Activation, casdu, ioa, new byte[] { (byte)rco }, profile);
        }

        public static byte[] EncodeSetpointNormalizedCommand(int casdu, int ioa, float normalizedValue, bool select, int quality, Iec101ApplicationProfile profile)
        {
            float clamped = Math.Max(-1.0f, Math.Min(1.0f, normalizedValue));
            short raw = (short)Math.Round(clamped * 32767.0f);
            byte qos = (byte)((select ? 0x80 : 0x00) | (quality & 0x7F));
            return EncodeSingleObjectAsdu(
                Iec101TypeId.C_SE_NA_1,
                Iec101CauseOfTransmission.Activation,
                casdu,
                ioa,
                new byte[] { (byte)(raw & 0xFF), (byte)((raw >> 8) & 0xFF), qos },
                profile);
        }

        public static byte[] EncodeClockSyncCommand(int casdu, DateTime timestampUtc, Iec101ApplicationProfile profile)
        {
            DateTime utc = timestampUtc.Kind == DateTimeKind.Utc ? timestampUtc : timestampUtc.ToUniversalTime();
            byte[] cp56 = EncodeCp56Time2a(utc);
            return EncodeSingleObjectAsdu(Iec101TypeId.C_CS_NA_1, Iec101CauseOfTransmission.Activation, casdu, 0, cp56, profile);
        }

        public static byte[] EncodeInformationObjectAsdu(Iec101TypeId typeId, Iec101CauseOfTransmission cot, bool negative, int casdu, int ioa, byte[] payload, Iec101ApplicationProfile profile)
        {
            return EncodeSingleObjectAsdu(typeId, cot, negative, casdu, ioa, payload, profile);
        }

        public static byte[] EncodeCp56Time(DateTime timestampUtc)
        {
            return EncodeCp56Time2a(timestampUtc);
        }

        private static byte[] EncodeSingleObjectAsdu(Iec101TypeId typeId, Iec101CauseOfTransmission cot, int casdu, int ioa, byte[] payload, Iec101ApplicationProfile profile)
        {
            return EncodeSingleObjectAsdu(typeId, cot, false, casdu, ioa, payload, profile);
        }

        private static byte[] EncodeSingleObjectAsdu(Iec101TypeId typeId, Iec101CauseOfTransmission cot, bool negative, int casdu, int ioa, byte[] payload, Iec101ApplicationProfile profile)
        {
            Iec101ApplicationProfile effectiveProfile = profile ?? Iec101ApplicationProfile.DefaultPln101();
            byte[] value = payload ?? new byte[0];
            List<byte> bytes = new List<byte>();
            bytes.Add((byte)typeId);
            bytes.Add(0x01);
            int causeByte = ((int)cot & 0x3F) | (negative ? 0x40 : 0x00);
            bytes.Add((byte)causeByte);
            if (effectiveProfile.CotLength > 1)
            {
                bytes.Add((byte)(effectiveProfile.OriginatorAddress & 0xFF));
            }

            WriteUnsignedLittleEndian(bytes, effectiveProfile.CasduLength, casdu);
            WriteUnsignedLittleEndian(bytes, effectiveProfile.IoaLength, ioa);
            bytes.AddRange(value);
            return bytes.ToArray();
        }

        private static void TryParseObjects(byte[] bytes, int offset, Iec101ApplicationProfile profile, Iec101Asdu asdu)
        {
            int valueLength = GetValueLength(asdu.TypeId);
            if (valueLength < 0 || asdu.ObjectCount <= 0)
            {
                return;
            }

            int currentOffset = offset;
            int baseAddress = 0;
            for (int index = 0; index < asdu.ObjectCount; index++)
            {
                if (!asdu.IsSequence || index == 0)
                {
                    if (currentOffset + profile.IoaLength > bytes.Length)
                    {
                        return;
                    }

                    baseAddress = ReadUnsignedLittleEndian(bytes, currentOffset, profile.IoaLength);
                    currentOffset += profile.IoaLength;
                }

                if (currentOffset + valueLength > bytes.Length)
                {
                    return;
                }

                byte[] rawObject = new byte[(asdu.IsSequence && index > 0 ? 0 : profile.IoaLength) + valueLength];
                int rawStart = asdu.IsSequence && index > 0 ? currentOffset : currentOffset - profile.IoaLength;
                Buffer.BlockCopy(bytes, rawStart, rawObject, 0, rawObject.Length);

                Iec101InformationObject informationObject = DecodeObject(
                    asdu.TypeId,
                    asdu.IsSequence ? baseAddress + index : baseAddress,
                    bytes,
                    currentOffset,
                    valueLength,
                    rawObject);

                asdu.Objects.Add(informationObject);
                currentOffset += valueLength;
            }
        }

        private static Iec101InformationObject DecodeObject(Iec101TypeId typeId, int ioa, byte[] bytes, int offset, int valueLength, byte[] rawObject)
        {
            Iec101InformationObject obj = new Iec101InformationObject
            {
                ObjectAddress = ioa,
                TypeName = typeId.ToString(),
                RawBytes = rawObject,
                ValueText = "Unknown"
            };

            switch (typeId)
            {
                case Iec101TypeId.M_SP_NA_1:
                case Iec101TypeId.M_SP_TA_1:
                case Iec101TypeId.M_SP_TB_1:
                    DecodeSinglePoint(obj, bytes[offset]);
                    break;
                case Iec101TypeId.M_DP_NA_1:
                case Iec101TypeId.M_DP_TA_1:
                case Iec101TypeId.M_DP_TB_1:
                    DecodeDoublePoint(obj, bytes[offset]);
                    break;
                case Iec101TypeId.M_ME_NA_1:
                case Iec101TypeId.M_ME_TA_1:
                case Iec101TypeId.M_ME_TD_1:
                    DecodeNormalizedMeasured(obj, bytes, offset);
                    break;
                case Iec101TypeId.M_ME_NB_1:
                case Iec101TypeId.M_ME_TB_1:
                case Iec101TypeId.M_ME_TE_1:
                    DecodeScaledMeasured(obj, bytes, offset);
                    break;
                case Iec101TypeId.M_ME_NC_1:
                case Iec101TypeId.M_ME_TC_1:
                case Iec101TypeId.M_ME_TF_1:
                    DecodeShortFloatMeasured(obj, bytes, offset);
                    break;
                case Iec101TypeId.M_IT_NA_1:
                case Iec101TypeId.M_IT_TB_1:
                    DecodeIntegratedTotal(obj, bytes, offset);
                    break;
                case Iec101TypeId.C_SC_NA_1:
                    DecodeSingleCommand(obj, bytes[offset]);
                    break;
                case Iec101TypeId.C_DC_NA_1:
                    DecodeDoubleCommand(obj, bytes[offset]);
                    break;
                case Iec101TypeId.C_RC_NA_1:
                    DecodeStepCommand(obj, bytes[offset]);
                    break;
                case Iec101TypeId.C_SE_NA_1:
                    DecodeSetpointNormalized(obj, bytes, offset);
                    break;
                case Iec101TypeId.C_IC_NA_1:
                    obj.ValueText = "QOI=" + bytes[offset];
                    break;
                default:
                    obj.ValueText = ToHex(bytes, offset, valueLength);
                    break;
            }

            obj.TimestampUtc = TryDecodeTimestampUtc(typeId, bytes, offset);
            return obj;
        }

        private static void DecodeSinglePoint(Iec101InformationObject obj, byte siq)
        {
            obj.ValueText = (siq & 0x01) != 0 ? "ON" : "OFF";
            obj.NumericValue = (siq & 0x01) != 0 ? 1 : 0;
            obj.Quality = Iec101QualityDescriptor.FromByte((byte)(siq & 0xF0));
        }

        private static void DecodeDoublePoint(Iec101InformationObject obj, byte diq)
        {
            int value = diq & 0x03;
            obj.NumericValue = value;
            obj.ValueText = value == 1 ? "OFF" : value == 2 ? "ON" : "Intermediate";
            obj.Quality = Iec101QualityDescriptor.FromByte((byte)(diq & 0xF0));
        }

        private static void DecodeNormalizedMeasured(Iec101InformationObject obj, byte[] bytes, int offset)
        {
            short raw = ToInt16(bytes, offset);
            double value = raw / 32768.0d;
            obj.NumericValue = value;
            obj.ValueText = value.ToString("0.###");
            obj.Quality = Iec101QualityDescriptor.FromByte(bytes[offset + 2]);
        }

        private static void DecodeScaledMeasured(Iec101InformationObject obj, byte[] bytes, int offset)
        {
            short value = ToInt16(bytes, offset);
            obj.NumericValue = value;
            obj.ValueText = value.ToString();
            obj.Quality = Iec101QualityDescriptor.FromByte(bytes[offset + 2]);
        }

        private static void DecodeShortFloatMeasured(Iec101InformationObject obj, byte[] bytes, int offset)
        {
            float value = BitConverter.ToSingle(bytes, offset);
            obj.NumericValue = value;
            obj.ValueText = value.ToString("0.###");
            obj.Quality = Iec101QualityDescriptor.FromByte(bytes[offset + 4]);
        }

        private static void DecodeIntegratedTotal(Iec101InformationObject obj, byte[] bytes, int offset)
        {
            int value = BitConverter.ToInt32(bytes, offset);
            obj.NumericValue = value;
            obj.ValueText = value.ToString();
            obj.Quality = Iec101QualityDescriptor.FromByte(bytes[offset + 4]);
        }

        private static void DecodeSingleCommand(Iec101InformationObject obj, byte sco)
        {
            obj.ValueText = (sco & 0x01) != 0 ? "ON" : "OFF";
            obj.NumericValue = (sco & 0x01) != 0 ? 1 : 0;
            obj.Select = (sco & 0x80) != 0;
            obj.CommandQualifierRaw = (sco >> 2) & 0x1F;
        }

        private static void DecodeDoubleCommand(Iec101InformationObject obj, byte dco)
        {
            int state = dco & 0x03;
            obj.NumericValue = state;
            obj.ValueText = state == 2 ? "ON" : state == 1 ? "OFF" : "Intermediate";
            obj.Select = (dco & 0x80) != 0;
            obj.CommandQualifierRaw = (dco >> 2) & 0x1F;
        }

        private static void DecodeStepCommand(Iec101InformationObject obj, byte rco)
        {
            int state = rco & 0x03;
            obj.NumericValue = state;
            obj.ValueText = state == 1 ? "RAISE" : state == 2 ? "LOWER" : "Intermediate";
            obj.Select = (rco & 0x80) != 0;
            obj.CommandQualifierRaw = (rco >> 2) & 0x1F;
        }

        private static void DecodeSetpointNormalized(Iec101InformationObject obj, byte[] bytes, int offset)
        {
            short raw = ToInt16(bytes, offset);
            double value = raw / 32768.0d;
            obj.NumericValue = value;
            obj.ValueText = value.ToString("0.###");
            if (offset + 2 < bytes.Length)
            {
                obj.Select = (bytes[offset + 2] & 0x80) != 0;
                obj.CommandQualifierRaw = bytes[offset + 2] & 0x7F;
            }
        }

        private static DateTime? TryDecodeTimestampUtc(Iec101TypeId typeId, byte[] bytes, int offset)
        {
            int timestampOffset = -1;
            bool cp56 = false;
            switch (typeId)
            {
                case Iec101TypeId.M_SP_TA_1:
                case Iec101TypeId.M_DP_TA_1:
                    timestampOffset = offset + 1;
                    break;
                case Iec101TypeId.M_ME_TA_1:
                case Iec101TypeId.M_ME_TB_1:
                    timestampOffset = offset + 3;
                    break;
                case Iec101TypeId.M_ME_TC_1:
                    timestampOffset = offset + 5;
                    break;
                case Iec101TypeId.M_SP_TB_1:
                case Iec101TypeId.M_DP_TB_1:
                    timestampOffset = offset + 1;
                    cp56 = true;
                    break;
                case Iec101TypeId.M_ME_TD_1:
                case Iec101TypeId.M_ME_TE_1:
                    timestampOffset = offset + 3;
                    cp56 = true;
                    break;
                case Iec101TypeId.M_ME_TF_1:
                    timestampOffset = offset + 5;
                    cp56 = true;
                    break;
                case Iec101TypeId.M_IT_TB_1:
                    timestampOffset = offset + 5;
                    cp56 = true;
                    break;
            }

            if (timestampOffset < 0)
            {
                return null;
            }

            return cp56 ? DecodeCp56(bytes, timestampOffset) : DecodeCp24(bytes, timestampOffset);
        }

        private static DateTime? DecodeCp24(byte[] bytes, int offset)
        {
            if (offset + 3 > bytes.Length)
            {
                return null;
            }

            int millis = bytes[offset] | (bytes[offset + 1] << 8);
            int minute = bytes[offset + 2] & 0x3F;
            DateTime now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, minute, 0, DateTimeKind.Utc).AddMilliseconds(millis);
        }

        private static DateTime? DecodeCp56(byte[] bytes, int offset)
        {
            if (offset + 7 > bytes.Length)
            {
                return null;
            }

            int millis = bytes[offset] | (bytes[offset + 1] << 8);
            int minute = bytes[offset + 2] & 0x3F;
            int hour = bytes[offset + 3] & 0x1F;
            int day = bytes[offset + 4] & 0x1F;
            int month = bytes[offset + 5] & 0x0F;
            int year = 2000 + (bytes[offset + 6] & 0x7F);
            if (month < 1 || day < 1 || hour > 23 || minute > 59)
            {
                return null;
            }

            try
            {
                return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc).AddMilliseconds(millis);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] EncodeCp56Time2a(DateTime timestampUtc)
        {
            DateTime utc = timestampUtc.Kind == DateTimeKind.Utc ? timestampUtc : timestampUtc.ToUniversalTime();
            int milliseconds = utc.Second * 1000 + utc.Millisecond;
            return new byte[]
            {
                (byte)(milliseconds & 0xFF),
                (byte)((milliseconds >> 8) & 0xFF),
                (byte)(utc.Minute & 0x3F),
                (byte)(utc.Hour & 0x1F),
                (byte)(utc.Day & 0x1F),
                (byte)(utc.Month & 0x0F),
                (byte)((utc.Year - 2000) & 0x7F)
            };
        }

        private static int GetValueLength(Iec101TypeId typeId)
        {
            switch (typeId)
            {
                case Iec101TypeId.M_SP_NA_1:
                case Iec101TypeId.M_DP_NA_1:
                case Iec101TypeId.M_EI_NA_1:
                case Iec101TypeId.C_SC_NA_1:
                case Iec101TypeId.C_DC_NA_1:
                case Iec101TypeId.C_RC_NA_1:
                case Iec101TypeId.C_IC_NA_1:
                    return 1;
                case Iec101TypeId.M_ST_NA_1:
                    return 2;
                case Iec101TypeId.M_ME_NA_1:
                case Iec101TypeId.M_ME_NB_1:
                case Iec101TypeId.C_SE_NA_1:
                    return 3;
                case Iec101TypeId.M_SP_TA_1:
                case Iec101TypeId.M_DP_TA_1:
                    return 4;
                case Iec101TypeId.M_ME_NC_1:
                case Iec101TypeId.M_IT_NA_1:
                    return 5;
                case Iec101TypeId.M_ME_TA_1:
                case Iec101TypeId.M_ME_TB_1:
                    return 6;
                case Iec101TypeId.M_SP_TB_1:
                case Iec101TypeId.M_DP_TB_1:
                    return 8;
                case Iec101TypeId.M_ST_TB_1:
                    return 9;
                case Iec101TypeId.M_ME_TD_1:
                case Iec101TypeId.M_ME_TE_1:
                    return 10;
                case Iec101TypeId.M_ME_TF_1:
                case Iec101TypeId.M_IT_TB_1:
                    return 12;
                default:
                    return -1;
            }
        }

        private static Iec101TypeId ToTypeId(int typeId)
        {
            return Enum.IsDefined(typeof(Iec101TypeId), typeId) ? (Iec101TypeId)typeId : Iec101TypeId.Unknown;
        }

        private static Iec101CauseOfTransmission ToCause(int cause)
        {
            return Enum.IsDefined(typeof(Iec101CauseOfTransmission), cause) ? (Iec101CauseOfTransmission)cause : Iec101CauseOfTransmission.Unknown;
        }

        private static int ReadUnsignedLittleEndian(byte[] bytes, int offset, int count)
        {
            int value = 0;
            for (int index = 0; index < count; index++)
            {
                value |= bytes[offset + index] << (8 * index);
            }

            return value;
        }

        private static void WriteUnsignedLittleEndian(List<byte> bytes, int count, int value)
        {
            for (int index = 0; index < count; index++)
            {
                bytes.Add((byte)((value >> (8 * index)) & 0xFF));
            }
        }

        private static short ToInt16(byte[] bytes, int offset)
        {
            return (short)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static byte[] Copy(byte[] bytes)
        {
            byte[] copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return copy;
        }

        private static string ToHex(byte[] bytes, int offset, int count)
        {
            char[] chars = new char[count * 3];
            int charIndex = 0;
            for (int index = 0; index < count; index++)
            {
                if (index > 0)
                {
                    chars[charIndex++] = ' ';
                }

                byte value = bytes[offset + index];
                chars[charIndex++] = GetHexNibble(value >> 4);
                chars[charIndex++] = GetHexNibble(value & 0x0F);
            }

            return new string(chars, 0, charIndex);
        }

        private static char GetHexNibble(int value)
        {
            return (char)(value < 10 ? '0' + value : 'A' + value - 10);
        }
    }
}
