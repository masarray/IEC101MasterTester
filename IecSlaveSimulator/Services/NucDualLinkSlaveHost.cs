using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class NucDualLinkSlaveHost : IDisposable
    {
        private readonly NucSlaveController _controller;
        private readonly Iec101SlaveService _primaryService;
        private readonly Iec101SlaveService _backupService;
        private Timer _healthTimer;
        private bool _disposed;
        private SlaveRuntimeConfig _primaryConfig;
        private SlaveRuntimeConfig _backupConfig;
        private readonly Dictionary<int, string> _lastPublishedGatewayValues = new Dictionary<int, string>();
        private NucEndpointId _lastObservedActiveEndpoint = NucEndpointId.None;

        public NucDualLinkSlaveHost(NucSlaveController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _primaryService = new Iec101SlaveService();
            _backupService = new Iec101SlaveService();
        }

        public Action<string, string> StatusLogged { get; set; }
        public Action<string, string> LinkActivityLogged { get; set; }
        public Action<int, string, string> RuntimeSignalUpdated { get; set; }

        public void Start(
            SlaveRuntimeConfig primaryConfig,
            SlaveRuntimeConfig backupConfig,
            IEnumerable<SignalDefinition> runtimeSignals)
        {
            _primaryConfig = CloneConfig(primaryConfig);
            _backupConfig = CloneConfig(backupConfig);
            IReadOnlyList<SignalDefinition> sharedSignals = (runtimeSignals ?? Enumerable.Empty<SignalDefinition>())
                .Select(CloneSignal)
                .ToList();

            _controller.LoadProject(sharedSignals, _controller.Settings);
            _controller.ConfigurePorts(primaryConfig.PortName, backupConfig.PortName);
            WireService(_primaryService, "L1", 1);
            WireService(_backupService, "L2", 2);

            LogStatus("NUC", string.Format("Starting Link1 on {0} (link {1}, CA {2}) as Active.", primaryConfig.PortName, primaryConfig.LinkAddress, primaryConfig.CommonAddress));
            LogStatus("NUC", string.Format("Starting Link2 on {0} (link {1}, CA {2}) as Standby.", backupConfig.PortName, backupConfig.LinkAddress, backupConfig.CommonAddress));

            _primaryService.Start(primaryConfig, _controller.SignalStore.Snapshot());
            _backupService.Start(backupConfig, _controller.SignalStore.Snapshot());
            PushGatewayFaultSignals();
            FlushPendingFaultEvents();

            _healthTimer = new Timer(_ => OnHealthTick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            LogStatus("NUC", "Dual-link slave host started. Link1 active, Link2 standby.");
        }

        public void Stop()
        {
            if (_healthTimer != null)
            {
                _healthTimer.Dispose();
                _healthTimer = null;
            }

            _primaryService.Stop();
            _backupService.Stop();
            _controller.MarkLinkDisconnected(1);
            _controller.MarkLinkDisconnected(2);
            LogStatus("NUC", "Dual-link slave host stopped.");
        }

        public void DisconnectLink(int linkNumber)
        {
            if (linkNumber == 1)
            {
                _primaryService.Stop();
                _controller.MarkLinkDisconnected(1);
                PushGatewayFaultSignals();
                FlushPendingFaultEvents();
                LogStatus("NUC", "Fault injection: Link A disconnected.");
            }
            else if (linkNumber == 2)
            {
                _backupService.Stop();
                _controller.MarkLinkDisconnected(2);
                PushGatewayFaultSignals();
                FlushPendingFaultEvents();
                LogStatus("NUC", "Fault injection: Link B disconnected.");
            }
        }

        public void ReconnectLink(int linkNumber)
        {
            if (linkNumber == 1 && _primaryConfig != null)
            {
                _primaryService.Start(CloneConfig(_primaryConfig), _controller.SignalStore.Snapshot());
                PushGatewayFaultSignals();
                FlushPendingFaultEvents();
                LogStatus("NUC", "Fault injection cleared: Link A restarted.");
            }
            else if (linkNumber == 2 && _backupConfig != null)
            {
                _backupService.Start(CloneConfig(_backupConfig), _controller.SignalStore.Snapshot());
                PushGatewayFaultSignals();
                FlushPendingFaultEvents();
                LogStatus("NUC", "Fault injection cleared: Link B restarted.");
            }
        }

        public void UpdateSignal(SignalDefinition signal)
        {
            if (signal == null)
            {
                return;
            }

            _controller.SignalStore.Upsert(CloneSignal(signal));
            SignalDefinition shared = _controller.SignalStore.Get(signal.Ioa);
            if (shared != null)
            {
                _primaryService.UpdateSignal(shared);
                _backupService.UpdateSignal(shared);
            }
        }

        public int InjectBufferBurst()
        {
            int injected = _controller.InjectBufferBurst();
            FlushPendingFaultEvents();

            LogStatus("NUC", string.Format("Buffer burst injected: {0} shared signals.", injected));
            return injected;
        }

        private void WireService(Iec101SlaveService service, string tag, int linkNumber)
        {
            service.StatusLogged = (category, message) => LogStatus(tag + ":" + category, message);
            service.LinkActivityLogged = (category, message) => LogLink(tag + ":" + category, message);
            service.ApplicationTrafficEnabledProvider = () => _controller.CanServeApplication(linkNumber);
            service.RuntimeSignalUpdated = (ioa, runtimeValue, liveCot) =>
            {
                _controller.SignalStore.UpdateRuntimeValue(ioa, runtimeValue, liveCot);
                RuntimeSignalUpdated?.Invoke(ioa, runtimeValue, liveCot);
            };
            service.ConnectionStateChanged = (connected, reason) =>
            {
                LogStatus(tag, connected
                    ? string.Format("Port ready on link {0}. {1}", linkNumber, reason ?? "Started")
                    : string.Format("Port stopped on link {0}. {1}", linkNumber, reason ?? "Stopped"));
                if (connected)
                {
                    _controller.MarkLinkConnected(linkNumber);
                }
                else
                {
                    _controller.MarkLinkDisconnected(linkNumber);
                }
            };
            service.LinkFrameObserved = (isTx, isRx) => _controller.MarkLinkFrame(linkNumber, isTx, isRx);
            service.MasterApplicationTrafficObserved = () => _controller.MarkApplicationTraffic(linkNumber);
            service.WorkerPulseObserved = () => _controller.MarkWorkerPulse(linkNumber);
        }

        private void LogStatus(string category, string message)
        {
            StatusLogged?.Invoke(category, message);
        }

        private void LogLink(string category, string message)
        {
            LinkActivityLogged?.Invoke(category, message);
        }

        private void PushGatewayFaultSignals()
        {
            QueueFaultSignalByIoa(8388714);
            QueueFaultSignalByIoa(8388715);
            QueueFaultSignalByIoa(8388725);
        }

        private void QueueFaultSignalByIoa(int ioa)
        {
            SignalDefinition signal = _controller.GetSignal(ioa);
            if (signal == null)
            {
                return;
            }

            string lastPublishedValue;
            if (_lastPublishedGatewayValues.TryGetValue(ioa, out lastPublishedValue)
                && string.Equals(lastPublishedValue, signal.RuntimeValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastPublishedGatewayValues[ioa] = signal.RuntimeValue;
            _controller.EventBuffer.Enqueue(new IecSlaveSimulator.Models.SharedBufferEvent
            {
                Ioa = signal.Ioa,
                Value = signal.RuntimeValue,
                Cot = signal.LiveCot,
                TimestampUtc = DateTime.UtcNow,
                Source = "GatewayFault",
                SignalType = signal.SignalType,
                Quality = signal.Quality,
                UseTimestamp = signal.UseTimestamp,
                Casdu = signal.Casdu
            });
        }

        private void FlushPendingFaultEvents()
        {
            Iec101SlaveService activeService = GetActiveService();
            if (activeService == null)
            {
                return;
            }

            SharedBufferEvent entry;
            while (_controller.EventBuffer.TryPeek(out entry))
            {
                if (entry == null)
                {
                    _controller.EventBuffer.TryDequeue(out entry);
                    continue;
                }

                SignalDefinition signal = _controller.GetSignal(entry.Ioa);
                if (signal == null)
                {
                    _controller.EventBuffer.TryDequeue(out entry);
                    continue;
                }

                signal.RuntimeValue = entry.Value;
                signal.LiveCot = entry.Cot;
                if (!string.IsNullOrWhiteSpace(entry.Quality))
                {
                    signal.Quality = entry.Quality;
                }
                signal.UseTimestamp = entry.UseTimestamp || signal.UseTimestamp;

                if (!activeService.EnqueueBufferedEvent(entry, signal))
                {
                    break;
                }

                _controller.EventBuffer.TryDequeue(out entry);
                RuntimeSignalUpdated?.Invoke(signal.Ioa, signal.RuntimeValue, signal.LiveCot);
            }
        }

        private Iec101SlaveService GetActiveService()
        {
            if (_controller.ActiveEndpoint == NucEndpointId.LinkA)
            {
                return _primaryService;
            }

            if (_controller.ActiveEndpoint == NucEndpointId.LinkB)
            {
                return _backupService;
            }

            return null;
        }

        private void OnHealthTick()
        {
            _controller.EvaluateLinkHealth();
            NucEndpointId activeEndpoint = _controller.ActiveEndpoint;
            if (activeEndpoint != _lastObservedActiveEndpoint)
            {
                IReadOnlyList<SignalDefinition> snapshot = _controller.SignalStore.Snapshot();
                if (activeEndpoint == NucEndpointId.LinkA)
                {
                    _primaryService.SyncSnapshotCache(snapshot);
                }
                else if (activeEndpoint == NucEndpointId.LinkB)
                {
                    _backupService.SyncSnapshotCache(snapshot);
                }

                _lastObservedActiveEndpoint = activeEndpoint;
            }

            FlushPendingFaultEvents();
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

        private static SlaveRuntimeConfig CloneConfig(SlaveRuntimeConfig source)
        {
            if (source == null)
            {
                return null;
            }

            return new SlaveRuntimeConfig
            {
                PortName = source.PortName,
                BaudRate = source.BaudRate,
                Parity = source.Parity,
                DataBits = source.DataBits,
                StopBits = source.StopBits,
                CommonAddress = source.CommonAddress,
                LinkAddress = source.LinkAddress,
                Class1QueueSize = source.Class1QueueSize,
                RunLoopDelayMs = source.RunLoopDelayMs,
                ResponseTimeoutMs = source.ResponseTimeoutMs,
                BackgroundPublishIntervalMs = source.BackgroundPublishIntervalMs,
                EnableMeasurementStreaming = source.EnableMeasurementStreaming
            };
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
            _primaryService.Dispose();
            _backupService.Dispose();
        }
    }
}
