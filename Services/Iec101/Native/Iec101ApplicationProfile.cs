namespace IEC101MasterTester.Services.Iec101.Native
{
    public sealed class Iec101ApplicationProfile
    {
        public int LinkAddressLength { get; private set; }
        public int CotLength { get; private set; }
        public int CasduLength { get; private set; }
        public int IoaLength { get; private set; }
        public int OriginatorAddress { get; private set; }

        public static Iec101ApplicationProfile FromValues(int linkAddressLength, int casduLength, int ioaLength, int originatorAddress)
        {
            return new Iec101ApplicationProfile
            {
                LinkAddressLength = Clamp(linkAddressLength, 1, 2),
                CotLength = 2,
                CasduLength = Clamp(casduLength, 1, 2),
                IoaLength = Clamp(ioaLength, 1, 3),
                OriginatorAddress = Clamp(originatorAddress, 0, 255)
            };
        }

        public static Iec101ApplicationProfile DefaultPln101()
        {
            return new Iec101ApplicationProfile
            {
                LinkAddressLength = 2,
                CotLength = 2,
                CasduLength = 2,
                IoaLength = 3,
                OriginatorAddress = 0
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
