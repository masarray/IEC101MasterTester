namespace IEC101MasterTester.Services.Iec101.Native.Asdu
{
    public sealed class Iec101QualityDescriptor
    {
        public byte Raw { get; private set; }
        public bool Overflow { get; private set; }
        public bool Blocked { get; private set; }
        public bool Substituted { get; private set; }
        public bool NonTopical { get; private set; }
        public bool Invalid { get; private set; }

        public static Iec101QualityDescriptor FromByte(byte raw)
        {
            return new Iec101QualityDescriptor
            {
                Raw = raw,
                Overflow = (raw & 0x01) != 0,
                Blocked = (raw & 0x10) != 0,
                Substituted = (raw & 0x20) != 0,
                NonTopical = (raw & 0x40) != 0,
                Invalid = (raw & 0x80) != 0
            };
        }

        public string ToOperatorText()
        {
            if (Invalid) return "Invalid";
            if (Blocked) return "Blocked";
            if (Substituted) return "Subst";
            if (NonTopical) return "Old";
            if (Overflow) return "Over";
            return "Good";
        }
    }
}
