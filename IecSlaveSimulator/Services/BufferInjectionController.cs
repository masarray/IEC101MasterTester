using System;
using System.Collections.Generic;
using System.Linq;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class BufferInjectionController
    {
        private readonly SharedSignalStore _signalStore;
        private readonly SharedEventBuffer _eventBuffer;

        public BufferInjectionController(SharedSignalStore signalStore, SharedEventBuffer eventBuffer)
        {
            _signalStore = signalStore ?? throw new ArgumentNullException(nameof(signalStore));
            _eventBuffer = eventBuffer ?? throw new ArgumentNullException(nameof(eventBuffer));
        }

        public int InjectBurst(int signalCount, int startIoa = 9500000)
        {
            List<SignalDefinition> candidates = _signalStore.Snapshot()
                .Where(IsSupportedBufferSignal)
                .OrderBy(signal => signal.Ioa)
                .ToList();

            if (candidates.Count == 0)
            {
                return 0;
            }

            int safeCount = signalCount <= 0 ? 640 : signalCount;
            DateTime baseTimeUtc = DateTime.UtcNow.AddMinutes(-1);
            Dictionary<int, string> nextValueByIoa = candidates.ToDictionary(
                signal => signal.Ioa,
                GetInitialInjectedValue);
            int injected = 0;
            for (int i = 0; i < safeCount; i++)
            {
                SignalDefinition seed = candidates[i % candidates.Count];
                SignalDefinition signal = CloneSignal(seed);
                string nextValue = nextValueByIoa[seed.Ioa];
                signal.RuntimeValue = nextValue;
                signal.LiveCot = "Spont";
                DateTime eventTimestampUtc = baseTimeUtc.AddMilliseconds(i * 100d);

                _signalStore.Upsert(signal);
                _eventBuffer.Enqueue(new SharedBufferEvent
                {
                    Ioa = signal.Ioa,
                    Value = signal.RuntimeValue,
                    Cot = "Spont",
                    TimestampUtc = eventTimestampUtc,
                    Source = "BufferInjection",
                    SignalType = signal.SignalType,
                    Quality = signal.Quality,
                    UseTimestamp = signal.UseTimestamp,
                    Casdu = signal.Casdu
                });
                nextValueByIoa[seed.Ioa] = ToggleBinaryValue(nextValue);
                injected++;
            }

            return injected;
        }

        private static bool IsSupportedBufferSignal(SignalDefinition signal)
        {
            if (signal == null || !signal.IsEnabled || signal.IsMeasurement || signal.IsCommand)
            {
                return false;
            }

            if (signal.Ioa == 8388714 || signal.Ioa == 8388715)
            {
                return false;
            }

            string label = signal.Label ?? string.Empty;
            string notes = signal.Notes ?? string.Empty;
            if (label.IndexOf("L1FT", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("L2FT", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("Main Link Fault", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("Backup Link Fault", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("Gateway", StringComparison.OrdinalIgnoreCase) >= 0
                || notes.IndexOf("GatewayMainLinkFault", StringComparison.OrdinalIgnoreCase) >= 0
                || notes.IndexOf("GatewayBackupLinkFault", StringComparison.OrdinalIgnoreCase) >= 0
                || notes.IndexOf("GatewayIedFaulty", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return signal.SignalType == SlaveSignalType.SinglePoint
                || signal.SignalType == SlaveSignalType.DoublePoint;
        }

        private static string GetInitialInjectedValue(SignalDefinition signal)
        {
            string current = (signal.RuntimeValue ?? signal.DefaultValue ?? "OFF").Trim().ToUpperInvariant();
            return ToggleBinaryValue(current);
        }

        private static string ToggleBinaryValue(string current)
        {
            return string.Equals(current, "ON", StringComparison.OrdinalIgnoreCase) ? "OFF" : "ON";
        }

        private static SignalDefinition CloneSignal(SignalDefinition source)
        {
            return new SignalDefinition
            {
                IsEnabled = source.IsEnabled,
                Ioa = source.Ioa,
                Label = source.Label,
                SignalType = source.SignalType,
                Casdu = source.Casdu,
                SignalClass = source.SignalClass,
                PublishMode = source.PublishMode,
                BackgroundEnabled = source.BackgroundEnabled,
                SpontaneousEnabled = source.SpontaneousEnabled,
                UseTimestamp = source.UseTimestamp,
                Quality = source.Quality,
                DefaultValue = source.DefaultValue,
                RuntimeValue = source.RuntimeValue,
                LiveCot = source.LiveCot,
                LinkedStatusIoa = source.LinkedStatusIoa,
                CommandSemantic = source.CommandSemantic,
                CommandBindingMode = source.CommandBindingMode,
                CommandOperateMode = source.CommandOperateMode,
                CommandDelayMs = source.CommandDelayMs,
                AnalogAnimation = source.AnalogAnimation,
                AnalogFrom = source.AnalogFrom,
                AnalogTo = source.AnalogTo,
                AnalogStep = source.AnalogStep,
                AnimationIntervalMs = source.AnimationIntervalMs,
                AnalogPingPong = source.AnalogPingPong,
                DiscreteAnimation = source.DiscreteAnimation,
                ToggleIntervalSeconds = source.ToggleIntervalSeconds,
                Notes = source.Notes
            };
        }
    }
}
