using System.Collections.Generic;
using System;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class SharedOutstationCore
    {
        private readonly SharedSignalStore _signalStore;
        private readonly SharedEventBuffer _eventBuffer;
        private readonly BufferInjectionController _bufferInjectionController;

        public SharedOutstationCore()
        {
            _signalStore = new SharedSignalStore();
            _eventBuffer = new SharedEventBuffer();
            _bufferInjectionController = new BufferInjectionController(_signalStore, _eventBuffer);
        }

        public SharedSignalStore SignalStore => _signalStore;

        public SharedEventBuffer EventBuffer => _eventBuffer;

        public void Load(IReadOnlyList<SignalDefinition> signals, bool emitGatewayBaseline)
        {
            _signalStore.Load(signals);
            _eventBuffer.Clear();
            if (emitGatewayBaseline)
            {
                _signalStore.EmitGatewayBaseline();
            }
        }

        public int InjectBufferBurst(int signalCount)
        {
            return _bufferInjectionController.InjectBurst(signalCount);
        }

        public bool ApplyLinkFaultState(bool link1Faulted, bool link2Faulted, string source = "SYNC")
        {
            SignalDefinition main = _signalStore.Get(8388714);
            SignalDefinition backup = _signalStore.Get(8388715);
            string prevMainValue = main == null ? "-" : main.RuntimeValue ?? "-";
            string prevBackupValue = backup == null ? "-" : backup.RuntimeValue ?? "-";
            System.Diagnostics.Trace.WriteLine(string.Format(
                "[FAULT-APPLY] incoming L1={0} L2={1} prevL1={2} prevL2={3} source={4} ts={5:o}",
                link1Faulted,
                link2Faulted,
                prevMainValue,
                prevBackupValue,
                string.IsNullOrWhiteSpace(source) ? "SYNC" : source,
                DateTime.UtcNow));
            bool changed = _signalStore.ApplyLinkFaultState(link1Faulted, link2Faulted);
            if (!changed)
            {
                return false;
            }

            string nextMainValue = link1Faulted ? "ON" : "OFF";
            string nextBackupValue = link2Faulted ? "ON" : "OFF";
            if (!string.Equals(prevMainValue, nextMainValue, StringComparison.OrdinalIgnoreCase))
            {
                EnqueueFaultEvent(8388714, link1Faulted, "Main");
            }

            if (!string.Equals(prevBackupValue, nextBackupValue, StringComparison.OrdinalIgnoreCase))
            {
                EnqueueFaultEvent(8388715, link2Faulted, "Backup");
            }
            return true;
        }

        private void EnqueueFaultEvent(int ioa, bool isFaulted, string source)
        {
            SignalDefinition signal = _signalStore.Get(ioa);
            if (signal == null)
            {
                return;
            }

            _eventBuffer.Enqueue(new SharedBufferEvent
            {
                Ioa = ioa,
                Value = isFaulted ? "ON" : "OFF",
                Cot = "Spont",
                TimestampUtc = DateTime.UtcNow,
                Source = source ?? "Fault",
                SignalType = signal.SignalType,
                Quality = signal.Quality,
                UseTimestamp = signal.UseTimestamp,
                Casdu = signal.Casdu
            });
        }
    }
}
