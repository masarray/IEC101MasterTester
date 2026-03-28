using System;
using System.Collections.Generic;
using System.Linq;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class SharedSignalStore
    {
        private readonly object _sync = new object();
        private readonly Dictionary<int, SignalDefinition> _signals = new Dictionary<int, SignalDefinition>();

        public void Load(IEnumerable<SignalDefinition> signals)
        {
            lock (_sync)
            {
                _signals.Clear();
                foreach (SignalDefinition signal in signals ?? Enumerable.Empty<SignalDefinition>())
                {
                    SignalDefinition clone = signal.CloneForRuntime();
                    _signals[clone.Ioa] = clone;
                }
            }
        }

        public IReadOnlyList<SignalDefinition> Snapshot()
        {
            lock (_sync)
            {
                return _signals.Values.Select(CloneForStore).ToList();
            }
        }

        public SignalDefinition Get(int ioa)
        {
            lock (_sync)
            {
                SignalDefinition signal;
                return _signals.TryGetValue(ioa, out signal) ? CloneForStore(signal) : null;
            }
        }

        public void Upsert(SignalDefinition signal)
        {
            if (signal == null)
            {
                return;
            }

            lock (_sync)
            {
                _signals[signal.Ioa] = CloneForStore(signal);
            }
        }

        public void UpdateRuntimeValue(int ioa, string value, string cot)
        {
            lock (_sync)
            {
                SignalDefinition signal;
                if (!_signals.TryGetValue(ioa, out signal))
                {
                    return;
                }

                signal.RuntimeValue = value;
                signal.LiveCot = cot;
            }
        }

        public bool SetGatewayFaultPoint(int ioa, bool isFaulted, string source = "SYNC")
        {
            lock (_sync)
            {
                SignalDefinition signal;
                if (!_signals.TryGetValue(ioa, out signal))
                {
                    return false;
                }

                string nextValue = isFaulted ? "ON" : "OFF";
                if (string.Equals(signal.RuntimeValue, nextValue, System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string oldValue = signal.RuntimeValue ?? "-";
                signal.RuntimeValue = nextValue;
                signal.LiveCot = "Spont";
                signal.BackgroundEnabled = false;
                signal.SpontaneousEnabled = true;
                System.Diagnostics.Trace.WriteLine(string.Format(
                    "[FAULT-POINT] IOA={0} {1}->{2} source={3} ts={4:o}",
                    ioa,
                    oldValue,
                    nextValue,
                    string.IsNullOrWhiteSpace(source) ? "SYNC" : source,
                    DateTime.UtcNow));
                return true;
            }
        }

        public bool ApplyLinkFaultState(bool link1Faulted, bool link2Faulted, string source = "SYNC")
        {
            bool changed = false;
            changed |= SetGatewayFaultPoint(8388714, link1Faulted, source);
            changed |= SetGatewayFaultPoint(8388715, link2Faulted, source);
            return changed;
        }

        public void EmitGatewayBaseline()
        {
            lock (_sync)
            {
                EnsurePointValue(8388714, "OFF");
                EnsurePointValue(8388715, "OFF");
                EnsurePointValue(8388725, "OFF");
            }
        }

        private void EnsurePointValue(int ioa, string value)
        {
            SignalDefinition signal;
            if (_signals.TryGetValue(ioa, out signal))
            {
                signal.RuntimeValue = value;
                signal.LiveCot = "Spont";
                signal.BackgroundEnabled = false;
                signal.SpontaneousEnabled = true;
            }
        }

        private static SignalDefinition CloneForStore(SignalDefinition source)
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



