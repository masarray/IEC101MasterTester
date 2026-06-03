using System;

namespace IEC101MasterTester.Services.Iec101.Native.Frames
{
    public sealed class Iec101Frame
    {
        public Iec101FrameType FrameType { get; set; }
        public Iec101ControlField Control { get; set; }
        public int? LinkAddress { get; set; }
        public byte[] AsduBytes { get; set; }
        public byte[] RawBytes { get; set; }
        public bool ChecksumValid { get; set; }

        public bool HasSecondaryAcd
        {
            get { return Control != null && !Control.IsPrimary && Control.Acd; }
        }

        public static Iec101Frame SingleCharacterAck()
        {
            return new Iec101Frame
            {
                FrameType = Iec101FrameType.SingleCharacterAck,
                RawBytes = new byte[] { 0xE5 },
                AsduBytes = new byte[0],
                ChecksumValid = true
            };
        }

        public byte[] GetAsduBytesOrEmpty()
        {
            return AsduBytes == null ? new byte[0] : (byte[])AsduBytes.Clone();
        }

        public byte[] GetRawBytesOrEmpty()
        {
            return RawBytes == null ? new byte[0] : (byte[])RawBytes.Clone();
        }
    }
}
