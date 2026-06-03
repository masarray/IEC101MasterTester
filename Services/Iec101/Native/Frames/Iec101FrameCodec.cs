using System;

namespace IEC101MasterTester.Services.Iec101.Native.Frames
{
    public static class Iec101FrameCodec
    {
        public const byte SingleCharacterAck = 0xE5;
        public const byte FixedStart = 0x10;
        public const byte VariableStart = 0x68;
        public const byte End = 0x16;

        public static bool TryParse(byte[] buffer, int length, Iec101ApplicationProfile profile, out Iec101Frame frame, out string error)
        {
            frame = null;
            error = null;

            if (buffer == null || length <= 0)
            {
                error = "No bytes available.";
                return false;
            }

            int available = Math.Min(length, buffer.Length);
            byte first = buffer[0];
            if (first == SingleCharacterAck)
            {
                frame = Iec101Frame.SingleCharacterAck();
                return true;
            }

            int linkAddressLength = GetLinkAddressLength(profile);
            if (first == FixedStart)
            {
                return TryParseFixed(buffer, available, linkAddressLength, out frame, out error);
            }

            if (first == VariableStart)
            {
                return TryParseVariable(buffer, available, linkAddressLength, out frame, out error);
            }

            error = string.Format("Unknown frame start 0x{0:X2}.", first);
            return false;
        }

        public static byte[] EncodeFixed(byte control, int linkAddress, Iec101ApplicationProfile profile)
        {
            int linkAddressLength = GetLinkAddressLength(profile);
            byte[] frame = new byte[4 + linkAddressLength];
            frame[0] = FixedStart;
            frame[1] = control;
            WriteUnsignedLittleEndian(frame, 2, linkAddressLength, linkAddress);
            frame[2 + linkAddressLength] = Checksum(frame, 1, 1 + linkAddressLength);
            frame[3 + linkAddressLength] = End;
            return frame;
        }

        public static byte[] EncodeVariable(byte control, int linkAddress, byte[] asduBytes, Iec101ApplicationProfile profile)
        {
            int linkAddressLength = GetLinkAddressLength(profile);
            byte[] asdu = asduBytes ?? new byte[0];
            int dataLength = 1 + linkAddressLength + asdu.Length;
            if (dataLength > 255)
            {
                throw new ArgumentOutOfRangeException("asduBytes", "IEC-101 FT1.2 variable frame data length cannot exceed 255 bytes.");
            }

            byte[] frame = new byte[6 + dataLength];
            frame[0] = VariableStart;
            frame[1] = (byte)dataLength;
            frame[2] = (byte)dataLength;
            frame[3] = VariableStart;
            frame[4] = control;
            WriteUnsignedLittleEndian(frame, 5, linkAddressLength, linkAddress);
            Buffer.BlockCopy(asdu, 0, frame, 5 + linkAddressLength, asdu.Length);
            frame[4 + dataLength] = Checksum(frame, 4, dataLength);
            frame[5 + dataLength] = End;
            return frame;
        }

        public static byte Checksum(byte[] bytes, int offset, int count)
        {
            int sum = 0;
            for (int index = 0; index < count; index++)
            {
                sum += bytes[offset + index];
            }

            return (byte)(sum & 0xFF);
        }

        private static bool TryParseFixed(byte[] buffer, int available, int linkAddressLength, out Iec101Frame frame, out string error)
        {
            frame = null;
            error = null;
            int expectedLength = 4 + linkAddressLength;
            if (available < expectedLength)
            {
                error = string.Format("Fixed frame is incomplete. Expected {0} bytes, got {1}.", expectedLength, available);
                return false;
            }

            if (buffer[expectedLength - 1] != End)
            {
                error = "Fixed frame end byte is invalid.";
                return false;
            }

            byte expectedChecksum = Checksum(buffer, 1, 1 + linkAddressLength);
            byte actualChecksum = buffer[2 + linkAddressLength];
            frame = new Iec101Frame
            {
                FrameType = Iec101FrameType.Fixed,
                Control = Iec101ControlField.Parse(buffer[1]),
                LinkAddress = ReadUnsignedLittleEndian(buffer, 2, linkAddressLength),
                AsduBytes = new byte[0],
                RawBytes = Copy(buffer, expectedLength),
                ChecksumValid = expectedChecksum == actualChecksum
            };

            if (!frame.ChecksumValid)
            {
                error = string.Format("Fixed frame checksum mismatch. Expected 0x{0:X2}, got 0x{1:X2}.", expectedChecksum, actualChecksum);
                return false;
            }

            return true;
        }

        private static bool TryParseVariable(byte[] buffer, int available, int linkAddressLength, out Iec101Frame frame, out string error)
        {
            frame = null;
            error = null;
            if (available < 6)
            {
                error = "Variable frame is incomplete.";
                return false;
            }

            int dataLength = buffer[1];
            if (buffer[2] != dataLength || buffer[3] != VariableStart)
            {
                error = "Variable frame repeated length/start bytes are invalid.";
                return false;
            }

            int expectedLength = 6 + dataLength;
            if (available < expectedLength)
            {
                error = string.Format("Variable frame is incomplete. Expected {0} bytes, got {1}.", expectedLength, available);
                return false;
            }

            if (buffer[expectedLength - 1] != End)
            {
                error = "Variable frame end byte is invalid.";
                return false;
            }

            if (dataLength < 1 + linkAddressLength)
            {
                error = "Variable frame data length is shorter than control + link address.";
                return false;
            }

            byte expectedChecksum = Checksum(buffer, 4, dataLength);
            byte actualChecksum = buffer[4 + dataLength];
            int asduLength = dataLength - 1 - linkAddressLength;
            byte[] asduBytes = new byte[asduLength];
            if (asduLength > 0)
            {
                Buffer.BlockCopy(buffer, 5 + linkAddressLength, asduBytes, 0, asduLength);
            }

            frame = new Iec101Frame
            {
                FrameType = Iec101FrameType.Variable,
                Control = Iec101ControlField.Parse(buffer[4]),
                LinkAddress = ReadUnsignedLittleEndian(buffer, 5, linkAddressLength),
                AsduBytes = asduBytes,
                RawBytes = Copy(buffer, expectedLength),
                ChecksumValid = expectedChecksum == actualChecksum
            };

            if (!frame.ChecksumValid)
            {
                error = string.Format("Variable frame checksum mismatch. Expected 0x{0:X2}, got 0x{1:X2}.", expectedChecksum, actualChecksum);
                return false;
            }

            return true;
        }

        private static int GetLinkAddressLength(Iec101ApplicationProfile profile)
        {
            if (profile == null)
            {
                return 2;
            }

            return profile.LinkAddressLength <= 1 ? 1 : 2;
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

        private static void WriteUnsignedLittleEndian(byte[] bytes, int offset, int count, int value)
        {
            for (int index = 0; index < count; index++)
            {
                bytes[offset + index] = (byte)((value >> (8 * index)) & 0xFF);
            }
        }

        private static byte[] Copy(byte[] bytes, int count)
        {
            byte[] copy = new byte[count];
            Buffer.BlockCopy(bytes, 0, copy, 0, count);
            return copy;
        }
    }
}
