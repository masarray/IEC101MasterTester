using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Iec101.Native
{
    public sealed class Iec101ApplicationProfile
    {
        public int LinkAddressLength { get; private set; }
        public int CotLength { get; private set; }
        public int CasduLength { get; private set; }
        public int IoaLength { get; private set; }
        public int OriginatorAddress { get; private set; }

        public static Iec101ApplicationProfile FromSettings(ConnectionSettings settings)
        {
            return new Iec101ApplicationProfile
            {
                LinkAddressLength = Clamp(settings == null ? 2 : settings.LinkAddressLength, 1, 2),
                CotLength = 2,
                CasduLength = Clamp(settings == null ? 2 : settings.CasduLength, 1, 2),
                IoaLength = Clamp(settings == null ? 3 : settings.IoaLength, 1, 3),
                OriginatorAddress = settings == null ? 0 : Clamp(settings.OriginatorAddress, 0, 255)
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
