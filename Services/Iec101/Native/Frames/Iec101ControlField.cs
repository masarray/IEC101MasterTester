namespace IEC101MasterTester.Services.Iec101.Native.Frames
{
    public sealed class Iec101ControlField
    {
        public byte Raw { get; private set; }
        public bool DirectionBit { get; private set; }
        public bool IsPrimary { get; private set; }
        public bool Fcb { get; private set; }
        public bool Fcv { get; private set; }
        public bool Acd { get; private set; }
        public bool Dfc { get; private set; }
        public int FunctionCode { get; private set; }

        public static Iec101ControlField Parse(byte value)
        {
            bool isPrimary = (value & 0x40) != 0;
            return new Iec101ControlField
            {
                Raw = value,
                DirectionBit = (value & 0x80) != 0,
                IsPrimary = isPrimary,
                Fcb = isPrimary && (value & 0x20) != 0,
                Fcv = isPrimary && (value & 0x10) != 0,
                Acd = !isPrimary && (value & 0x20) != 0,
                Dfc = !isPrimary && (value & 0x10) != 0,
                FunctionCode = value & 0x0F
            };
        }

        public string Describe()
        {
            return IsPrimary
                ? string.Format("0x{0:X2} PRM=1 DIR={1} FCB={2} FCV={3} {4}", Raw, DirectionBit ? 1 : 0, Fcb ? 1 : 0, Fcv ? 1 : 0, DescribePrimaryFunction(FunctionCode))
                : string.Format("0x{0:X2} PRM=0 DIR={1} ACD={2} DFC={3} {4}", Raw, DirectionBit ? 1 : 0, Acd ? 1 : 0, Dfc ? 1 : 0, DescribeSecondaryFunction(FunctionCode));
        }

        private static string DescribePrimaryFunction(int functionCode)
        {
            switch (functionCode)
            {
                case 0: return "Reset remote link";
                case 1: return "Reset user process";
                case 2: return "Test function for link";
                case 3: return "User data confirmed";
                case 4: return "User data no reply";
                case 8: return "Reset FCB";
                case 9: return "Request link status";
                case 10: return "Request Class 1 data";
                case 11: return "Request Class 2 data";
                default: return "Primary FC" + functionCode;
            }
        }

        private static string DescribeSecondaryFunction(int functionCode)
        {
            switch (functionCode)
            {
                case 0: return "ACK";
                case 1: return "NACK";
                case 8: return "User data";
                case 9: return "NACK requested data";
                case 11: return "Link status";
                case 14: return "Link service not functioning";
                case 15: return "Link service not implemented";
                default: return "Secondary FC" + functionCode;
            }
        }
    }
}
