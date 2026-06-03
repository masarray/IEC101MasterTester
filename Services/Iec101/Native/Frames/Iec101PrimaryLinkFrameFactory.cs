namespace IEC101MasterTester.Services.Iec101.Native.Frames
{
    public static class Iec101PrimaryLinkFrameFactory
    {
        public static byte[] ResetRemoteLink(int linkAddress, Iec101ApplicationProfile profile)
        {
            return Iec101FrameCodec.EncodeFixed(BuildPrimaryControl(0, false, false), linkAddress, profile);
        }

        public static byte[] ResetFcb(int linkAddress, Iec101ApplicationProfile profile)
        {
            return Iec101FrameCodec.EncodeFixed(BuildPrimaryControl(8, false, false), linkAddress, profile);
        }

        public static byte[] TestLink(int linkAddress, Iec101ApplicationProfile profile)
        {
            return Iec101FrameCodec.EncodeFixed(BuildPrimaryControl(2, false, false), linkAddress, profile);
        }

        public static byte[] RequestLinkStatus(int linkAddress, Iec101ApplicationProfile profile)
        {
            return Iec101FrameCodec.EncodeFixed(BuildPrimaryControl(9, false, false), linkAddress, profile);
        }

        public static byte[] RequestClass1Data(int linkAddress, bool fcb, Iec101ApplicationProfile profile)
        {
            return Iec101FrameCodec.EncodeFixed(BuildPrimaryControl(10, fcb, true), linkAddress, profile);
        }

        public static byte[] RequestClass2Data(int linkAddress, bool fcb, Iec101ApplicationProfile profile)
        {
            return Iec101FrameCodec.EncodeFixed(BuildPrimaryControl(11, fcb, true), linkAddress, profile);
        }

        public static byte[] SendUserDataConfirmed(int linkAddress, bool fcb, byte[] asduBytes, Iec101ApplicationProfile profile)
        {
            return Iec101FrameCodec.EncodeVariable(BuildPrimaryControl(3, fcb, true), linkAddress, asduBytes, profile);
        }

        public static byte[] SendUserDataNoReply(int linkAddress, byte[] asduBytes, Iec101ApplicationProfile profile)
        {
            return Iec101FrameCodec.EncodeVariable(BuildPrimaryControl(4, false, false), linkAddress, asduBytes, profile);
        }

        private static byte BuildPrimaryControl(int functionCode, bool fcb, bool fcv)
        {
            int control = 0x40 | (functionCode & 0x0F);
            if (fcb)
            {
                control |= 0x20;
            }

            if (fcv)
            {
                control |= 0x10;
            }

            return (byte)control;
        }
    }
}
