using System.Collections.Generic;
using System.Runtime.Serialization;

namespace IecSlaveSimulator.Models
{
    [DataContract]
    public sealed class SlaveProjectDefinition
    {
        [DataMember(Order = 1)]
        public string ProjectName { get; set; }

        [DataMember(Order = 2)]
        public int CommonAddress { get; set; }

        [DataMember(Order = 3)]
        public int LinkAddress { get; set; }

        [DataMember(Order = 4)]
        public string Notes { get; set; }

        [DataMember(Order = 5)]
        public List<SignalDefinition> Signals { get; set; }

        [DataMember(Order = 6)]
        public NucSlaveSettings NucSettings { get; set; }

        public static SlaveProjectDefinition CreateDefault()
        {
            return new SlaveProjectDefinition
            {
                ProjectName = "IEC-101 PLN Pusertif Stage 1",
                CommonAddress = 105,
                LinkAddress = 105,
                Notes = "Stage 1 PLN Pusertif baseline: gateway redundancy points, CB/LR status, analog telemetry, and command-feedback pairs.",
                Signals = CreatePlnPusertifStage1Signals(),
                NucSettings = NucSlaveSettings.CreateDefault()
            };
        }

        public static List<SignalDefinition> CreateBufferInjectionSignals(int startIoa, int count)
        {
            List<SignalDefinition> signals = new List<SignalDefinition>();
            int safeCount = count <= 0 ? 640 : count;
            int ioa = startIoa <= 0 ? 9500000 : startIoa;

            for (int i = 0; i < safeCount; i++)
            {
                signals.Add(new SignalDefinition
                {
                    Ioa = ioa + i,
                    Label = "Buffered Event " + (i + 1),
                    SignalType = SlaveSignalType.SinglePoint,
                    PublishMode = SignalPublishMode.Spontaneous,
                    BackgroundEnabled = false,
                    SpontaneousEnabled = true,
                    DefaultValue = (i % 2 == 0) ? "OFF" : "ON",
                    RuntimeValue = (i % 2 == 0) ? "ON" : "OFF",
                    UseTimestamp = true,
                    SignalClass = SignalClass.Class1,
                    Notes = "NUC buffer injection scaffold"
                });
            }

            return signals;
        }

        private static List<SignalDefinition> CreatePlnPusertifStage1Signals()
        {
            return new List<SignalDefinition>
            {
                CreateBinary(8388754, "MPU / Voltage Status", SignalPublishMode.Spontaneous, "ON"),
                CreateBinary(8388714, "L1FT Main Link Fault", SignalPublishMode.Spontaneous, "ON"),
                CreateBinary(8388715, "L2FT Backup Link Fault", SignalPublishMode.Spontaneous, "ON"),
                CreateBinary(8388716, "MPU 1 Trip", SignalPublishMode.Spontaneous, "ON"),
                CreateBinary(8388717, "MPU 2 Trip", SignalPublishMode.Spontaneous, "ON"),
                CreateBinary(8388725, "IEDF IED Faulty", SignalPublishMode.Spontaneous, "ON"),

                CreateDoubleStatus(16712689, "Feeder CB1 Status", "ON"),
                CreateDoubleStatus(16712686, "Kopel CB2 Status", "ON"),
                CreateDoubleStatus(16712704, "Trafo CB Status", "ON"),
                CreateDoubleStatus(16712694, "Feeder LR1 Local / Remote", "ON"),
                CreateDoubleStatus(16712701, "Kopel LR2 Local / Remote", "ON"),
                CreateDoubleStatus(16712708, "Trafo LR Local / Remote", "ON"),
                CreateDoubleStatus(16712709, "Tap Changer Local / Remote", "ON"),
                CreateDoubleStatus(16712710, "Tap Changer Auto / Manual", "ON"),

                CreateAnalog(790448, "Tap Position Indication", "7", 0d, 15d, 1d, SlaveSignalType.StepPosition),
                CreateAnalog(790446, "Feeder Active Power P1", "12.5", 11.8d, 13.4d, 0.1d, SlaveSignalType.MeasuredShort, SignalPublishMode.Spontaneous),
                CreateAnalog(790447, "Feeder Reactive Power Q1", "4.2", 3.8d, 4.7d, 1d, SlaveSignalType.MeasuredScaled, SignalPublishMode.Spontaneous),
                CreateAnalog(790438, "Kopel Active Power P2", "10.1", 9.5d, 10.8d, 0.08d, SlaveSignalType.MeasuredShort, SignalPublishMode.Spontaneous),
                CreateAnalog(790439, "Kopel Reactive Power Q2", "3.7", 3.2d, 4.2d, 1d, SlaveSignalType.MeasuredScaled, SignalPublishMode.Spontaneous),
                CreateAnalog(790442, "Trafo Active Power", "21.2", 20.4d, 22.1d, 0.12d, SlaveSignalType.MeasuredShort, SignalPublishMode.Spontaneous),
                CreateAnalog(790443, "Trafo Reactive Power", "5.9", 5.3d, 6.4d, 1d, SlaveSignalType.MeasuredScaled, SignalPublishMode.Spontaneous),
                CreateAnalog(790449, "Real Power Setting Measured", "0.15", 0d, 1d, 0.01d, SlaveSignalType.MeasuredNormalized, SignalPublishMode.Spontaneous),

                CreateDoubleCommand(68542, "Feeder CB1 Double Command", 16712689),
                CreateDoubleCommand(68539, "Kopel CB2 Double Command", 16712686),
                CreateDoubleCommand(68550, "Trafo CB Double Command", 16712704),
                CreateRaiseLowerCommand(74537, "Tap Changer Raise / Lower", 790448),
                CreateSetpointCommand(70537, "Real Power Set Point Command", 790449)
            };
        }

        private static SignalDefinition CreateBinary(int ioa, string label, SignalPublishMode publishMode, string defaultValue)
        {
            return new SignalDefinition
            {
                Ioa = ioa,
                Label = label,
                SignalType = SlaveSignalType.SinglePoint,
                PublishMode = publishMode,
                BackgroundEnabled = publishMode == SignalPublishMode.BackgroundScan || publishMode == SignalPublishMode.BackgroundAndSpontaneous,
                SpontaneousEnabled = publishMode == SignalPublishMode.Spontaneous || publishMode == SignalPublishMode.BackgroundAndSpontaneous,
                DefaultValue = defaultValue,
                RuntimeValue = defaultValue,
                UseTimestamp = true,
                SignalClass = SignalClass.Class1,
                Notes = "PLN Pusertif Stage 1 binary point"
            };
        }

        private static SignalDefinition CreateDoubleStatus(int ioa, string label, string defaultValue)
        {
            return new SignalDefinition
            {
                Ioa = ioa,
                Label = label,
                SignalType = SlaveSignalType.DoublePoint,
                PublishMode = SignalPublishMode.Spontaneous,
                BackgroundEnabled = false,
                SpontaneousEnabled = true,
                DefaultValue = defaultValue,
                RuntimeValue = defaultValue,
                UseTimestamp = true,
                SignalClass = SignalClass.Class1,
                Notes = "PLN Pusertif Stage 1 double-point status"
            };
        }

        private static SignalDefinition CreateAnalog(
            int ioa,
            string label,
            string defaultValue,
            double from,
            double to,
            double step,
            SlaveSignalType type = SlaveSignalType.MeasuredShort,
            SignalPublishMode publishMode = SignalPublishMode.BackgroundScan)
        {
            return new SignalDefinition
            {
                Ioa = ioa,
                Label = label,
                SignalType = type,
                PublishMode = publishMode,
                BackgroundEnabled = publishMode == SignalPublishMode.BackgroundScan || publishMode == SignalPublishMode.BackgroundAndSpontaneous,
                SpontaneousEnabled = publishMode == SignalPublishMode.Spontaneous || publishMode == SignalPublishMode.BackgroundAndSpontaneous,
                DefaultValue = defaultValue,
                RuntimeValue = defaultValue,
                AnalogAnimation = AnalogAnimationKind.RampPingPong,
                AnalogFrom = from,
                AnalogTo = to,
                AnalogStep = step,
                AnimationIntervalMs = 2000,
                SignalClass = SignalClass.Class2,
                Notes = "PLN Pusertif Stage 1 analog point"
            };
        }

        private static SignalDefinition CreateDoubleCommand(int ioa, string label, int linkedStatusIoa)
        {
            return new SignalDefinition
            {
                Ioa = ioa,
                Label = label,
                SignalType = SlaveSignalType.CommandDouble,
                PublishMode = SignalPublishMode.CommandFeedback,
                BackgroundEnabled = false,
                SpontaneousEnabled = false,
                DefaultValue = "-",
                RuntimeValue = "-",
                LinkedStatusIoa = linkedStatusIoa,
                CommandSemantic = CommandSemantic.OpenClose,
                CommandBindingMode = CommandBindingMode.Spontaneous,
                CommandOperateMode = CommandOperateMode.Both,
                SignalClass = SignalClass.Class1,
                Notes = "PLN Pusertif Stage 1 command-feedback pair"
            };
        }

        private static SignalDefinition CreateRaiseLowerCommand(int ioa, string label, int linkedStatusIoa)
        {
            return new SignalDefinition
            {
                Ioa = ioa,
                Label = label,
                SignalType = SlaveSignalType.CommandDouble,
                PublishMode = SignalPublishMode.CommandFeedback,
                BackgroundEnabled = false,
                SpontaneousEnabled = false,
                DefaultValue = "-",
                RuntimeValue = "-",
                LinkedStatusIoa = linkedStatusIoa,
                CommandSemantic = CommandSemantic.RaiseLower,
                CommandBindingMode = CommandBindingMode.CommandFeedback,
                CommandOperateMode = CommandOperateMode.Both,
                SignalClass = SignalClass.Class1,
                Notes = "PLN Pusertif Stage 1 regulating command"
            };
        }

        private static SignalDefinition CreateSetpointCommand(int ioa, string label, int linkedStatusIoa)
        {
            return new SignalDefinition
            {
                Ioa = ioa,
                Label = label,
                SignalType = SlaveSignalType.CommandSetpointNormalized,
                PublishMode = SignalPublishMode.CommandFeedback,
                BackgroundEnabled = false,
                SpontaneousEnabled = false,
                DefaultValue = "-",
                RuntimeValue = "-",
                LinkedStatusIoa = linkedStatusIoa,
                CommandSemantic = CommandSemantic.None,
                CommandBindingMode = CommandBindingMode.CommandFeedback,
                CommandOperateMode = CommandOperateMode.Both,
                SignalClass = SignalClass.Class1,
                Notes = "PLN Pusertif Stage 1 setpoint command"
            };
        }
    }
}


