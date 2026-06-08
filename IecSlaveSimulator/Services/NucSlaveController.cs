using System;
using System.Diagnostics;
using System.Collections.Generic;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class NucSlaveController
    {
        private readonly SharedOutstationCore _sharedCore;
        private readonly NucActiveStandbyArbiter _arbiter;
        private NucSlaveSettings _settings;
        private bool? _lastExportedL1Fault;
        private bool? _lastExportedL2Fault;
        private bool? _lastExportedIedFault;

        public NucSlaveController()
        {
            _sharedCore = new SharedOutstationCore();
            _arbiter = new NucActiveStandbyArbiter();
            _settings = NucSlaveSettings.CreateDefault();
            LinkA = new NucPortEndpointState { EndpointId = NucEndpointId.LinkA, State = NucSlaveLinkState.Disconnected };
            LinkB = new NucPortEndpointState { EndpointId = NucEndpointId.LinkB, State = NucSlaveLinkState.Disconnected };
        }

        public SharedSignalStore SignalStore => _sharedCore.SignalStore;

        public SharedEventBuffer EventBuffer => _sharedCore.EventBuffer;

        public NucSlaveSettings Settings => _settings;

        public NucPortEndpointState LinkA { get; }

        public NucPortEndpointState LinkB { get; }

        public NucEndpointId ActiveEndpoint => _arbiter.ActiveEndpoint;

        public NucSlaveLinkState Link1State => LinkA.State;

        public NucSlaveLinkState Link2State => LinkB.State;

        public DateTime? Link1LastRxUtc => LinkA.LastRxUtc;

        public DateTime? Link2LastRxUtc => LinkB.LastRxUtc;

        public DateTime? Link1LastTxUtc => LinkA.LastTxUtc;

        public DateTime? Link2LastTxUtc => LinkB.LastTxUtc;

        public int Link1RxCount => LinkA.RxCount;

        public int Link2RxCount => LinkB.RxCount;

        public int Link1TxCount => LinkA.TxCount;

        public int Link2TxCount => LinkB.TxCount;

        public void LoadProject(IReadOnlyList<SignalDefinition> signals, NucSlaveSettings settings = null)
        {
            _settings = settings ?? NucSlaveSettings.CreateDefault();
            NormalizeSettings(_settings);
            _sharedCore.Load(signals, _settings.EmitGatewayBaselineOnStart);
            ResetEndpoints();
        }

        public void ConfigurePorts(string primaryPortName, string backupPortName)
        {
            LinkA.PortName = primaryPortName;
            LinkB.PortName = backupPortName;
        }

        public int InjectBufferBurst()
        {
            return _sharedCore.InjectBufferBurst(_settings.BufferInjectionSignalCount);
        }

        public bool CanServeApplication(int linkNumber)
        {
            NucPortEndpointState endpoint = ResolveEndpoint(linkNumber);
            return endpoint != null
                && endpoint.Role == NucEndpointRole.Active
                && endpoint.State == NucSlaveLinkState.ActivePolling;
        }

        public void MarkWorkerPulse(int linkNumber)
        {
            NucPortEndpointState endpoint = ResolveEndpoint(linkNumber);
            if (endpoint == null)
            {
                return;
            }

            endpoint.IsLoopAlive = true;
            endpoint.LastWorkerPulseUtc = DateTime.UtcNow;
        }

        public void MarkLinkConnected(int linkNumber)
        {
            NucPortEndpointState endpoint = ResolveEndpoint(linkNumber);
            if (endpoint == null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            endpoint.IsConnected = true;
            endpoint.IsLoopAlive = true;
            endpoint.ConnectedAtUtc = nowUtc;
            endpoint.RecoveryStartedUtc = nowUtc;
            endpoint.LastWorkerPulseUtc = nowUtc;
            endpoint.LastRxUtc = null;
            endpoint.LastTxUtc = null;
            endpoint.LastValidMasterActivityUtc = null;
            endpoint.Role = NucEndpointRole.None;
            endpoint.State = NucSlaveLinkState.Recovering;

            Trace.WriteLine(string.Format(
                "[SLAVE-LINK] {0} CONNECTED/RECOVERING ts={1:o}",
                endpoint.EndpointId,
                nowUtc));

            EvaluateArbiter();
        }

        public void MarkLinkDisconnected(int linkNumber)
        {
            NucPortEndpointState endpoint = ResolveEndpoint(linkNumber);
            if (endpoint == null)
            {
                return;
            }

            endpoint.IsConnected = false;
            endpoint.State = NucSlaveLinkState.Disconnected;
            endpoint.Role = NucEndpointRole.None;
            Trace.WriteLine(string.Format(
                "[SLAVE-LINK] {0} DISCONNECTED ts={1:o}",
                endpoint.EndpointId,
                DateTime.UtcNow));
            EvaluateArbiter();
        }

        public void MarkLinkFrame(int linkNumber, bool isTx, bool isRx)
        {
            DateTime nowUtc = DateTime.UtcNow;
            NucPortEndpointState endpoint = ResolveEndpoint(linkNumber);
            if (endpoint == null)
            {
                return;
            }

            endpoint.IsConnected = true;
            if (isTx)
            {
                endpoint.LastTxUtc = nowUtc;
                endpoint.TxCount++;
            }

            if (isRx)
            {
                endpoint.LastRxUtc = nowUtc;
                endpoint.RxCount++;
                Trace.WriteLine(string.Format(
                    "[SLAVE-LINK] {0} RX ts={1:o}",
                    endpoint.EndpointId,
                    nowUtc));
            }

            if (endpoint.State == NucSlaveLinkState.Timeout
                || endpoint.State == NucSlaveLinkState.Faulted
                || endpoint.State == NucSlaveLinkState.Recovering)
            {
                endpoint.State = endpoint.Role == NucEndpointRole.Active
                    ? NucSlaveLinkState.ActivePolling
                    : NucSlaveLinkState.StandbyReady;
                endpoint.RecoveryStartedUtc = null;
            }

            EvaluateArbiter();
        }

        public void MarkApplicationTraffic(int linkNumber)
        {
            DateTime nowUtc = DateTime.UtcNow;
            NucPortEndpointState endpoint = ResolveEndpoint(linkNumber);
            NucPortEndpointState other = linkNumber == 1 ? LinkB : LinkA;
            if (endpoint == null)
            {
                return;
            }

            endpoint.IsConnected = true;
            endpoint.LastRxUtc = nowUtc;
            endpoint.LastValidMasterActivityUtc = nowUtc;
            endpoint.Role = NucEndpointRole.Active;
            endpoint.State = NucSlaveLinkState.ActivePolling;
            endpoint.RecoveryStartedUtc = null;

            if (other != null && other.IsConnected)
            {
                other.Role = NucEndpointRole.Standby;
                if (other.State != NucSlaveLinkState.Timeout && other.State != NucSlaveLinkState.Faulted)
                {
                    other.State = NucSlaveLinkState.StandbyReady;
                }
            }

            _arbiter.PreferActive(endpoint.EndpointId);
            EvaluateArbiter();

            Trace.WriteLine(string.Format(
                "[SLAVE-LINK] {0} APPLICATION-ACTIVE ts={1:o}",
                endpoint.EndpointId,
                nowUtc));
        }

        public void EvaluateLinkHealth()
        {
            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan timeoutWindow = TimeSpan.FromSeconds(4);
            TimeSpan loopWindow = TimeSpan.FromSeconds(8);

            EvaluateEndpointHealth(LinkA, nowUtc, timeoutWindow, loopWindow);
            EvaluateEndpointHealth(LinkB, nowUtc, timeoutWindow, loopWindow);
            EvaluateArbiter();
        }

        public SignalDefinition GetSignal(int ioa)
        {
            return _sharedCore.SignalStore.Get(ioa);
        }

        private void EvaluateEndpointHealth(NucPortEndpointState endpoint, DateTime nowUtc, TimeSpan timeoutWindow, TimeSpan loopWindow)
        {
            NucSlaveLinkState previousState = endpoint.State;
            NucSlaveLinkState newState = previousState;
            string endpointName = endpoint.EndpointId.ToString();

            if (!endpoint.IsConnected)
            {
                newState = NucSlaveLinkState.Disconnected;
            }
            else if (endpoint.LastWorkerPulseUtc.HasValue && nowUtc - endpoint.LastWorkerPulseUtc.Value > loopWindow)
            {
                newState = NucSlaveLinkState.Faulted;
            }
            else if (!endpoint.LastRxUtc.HasValue)
            {
                // Connected but no master link-layer frame has arrived yet. This is recovery/awaiting-master, not a fault.
                newState = NucSlaveLinkState.Recovering;
            }
            else if (nowUtc - endpoint.LastRxUtc.Value > timeoutWindow)
            {
                newState = NucSlaveLinkState.Timeout;
            }
            else if (endpoint.Role == NucEndpointRole.Active)
            {
                newState = NucSlaveLinkState.ActivePolling;
            }
            else
            {
                newState = NucSlaveLinkState.StandbyReady;
            }

            if (newState == previousState)
            {
                return;
            }

            endpoint.State = newState;
            if (newState == NucSlaveLinkState.Disconnected
                || newState == NucSlaveLinkState.Faulted
                || newState == NucSlaveLinkState.Timeout)
            {
                endpoint.Role = NucEndpointRole.None;
            }

            Trace.WriteLine(string.Format(
                "[ENDPOINT-STATE] {0} {1} -> {2} ts={3:o}",
                endpointName,
                previousState,
                newState,
                nowUtc));

            if (newState == NucSlaveLinkState.Timeout)
            {
                Trace.WriteLine(string.Format(
                    "[SLAVE-LINK] {0} TIMEOUT ts={1:o} lastMasterActivity={2} lastTx={3} lastRx={4}",
                    endpoint.EndpointId,
                    nowUtc,
                    endpoint.LastValidMasterActivityUtc.HasValue ? endpoint.LastValidMasterActivityUtc.Value.ToString("o") : "-",
                    endpoint.LastTxUtc.HasValue ? endpoint.LastTxUtc.Value.ToString("o") : "-",
                    endpoint.LastRxUtc.HasValue ? endpoint.LastRxUtc.Value.ToString("o") : "-"));
            }
        }

        private void EvaluateArbiter()
        {
            _arbiter.Evaluate(LinkA, LinkB);
            SyncGatewayFaults();
        }

        private void SyncGatewayFaults()
        {
            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan timeoutWindow = TimeSpan.FromSeconds(4);
            TimeSpan loopWindow = TimeSpan.FromSeconds(8);

            bool l1Fault = !LinkA.IsConnected
                || (LinkA.LastWorkerPulseUtc.HasValue && (nowUtc - LinkA.LastWorkerPulseUtc.Value > loopWindow))
                || (!LinkA.LastRxUtc.HasValue || nowUtc - LinkA.LastRxUtc.Value > timeoutWindow);
            bool l2Fault = !LinkB.IsConnected
                || (LinkB.LastWorkerPulseUtc.HasValue && (nowUtc - LinkB.LastWorkerPulseUtc.Value > loopWindow))
                || (!LinkB.LastRxUtc.HasValue || nowUtc - LinkB.LastRxUtc.Value > timeoutWindow);
            bool iedFault = false;

            if (_lastExportedL1Fault == l1Fault
                && _lastExportedL2Fault == l2Fault
                && _lastExportedIedFault == iedFault)
            {
                return;
            }

            _lastExportedL1Fault = l1Fault;
            _lastExportedL2Fault = l2Fault;
            _lastExportedIedFault = iedFault;

            if (l1Fault)
            {
                Trace.WriteLine(string.Format("[SLAVE-FAULT] L1FT={0} ts={1:o}", LinkA.State, DateTime.UtcNow));
            }

            if (l2Fault)
            {
                Trace.WriteLine(string.Format("[SLAVE-FAULT] L2FT={0} ts={1:o}", LinkB.State, DateTime.UtcNow));
            }

            Trace.WriteLine(string.Format(
                "[SYNC-FAULT] LinkAState={0} LinkBState={1} L1={2} L2={3} ts={4:o}",
                LinkA.State,
                LinkB.State,
                l1Fault,
                l2Fault,
                DateTime.UtcNow));

            _sharedCore.ApplyLinkFaultState(l1Fault, l2Fault, "SYNC");
        }

        private void ResetEndpoints()
        {
            _arbiter.Reset();
            LinkA.Reset();
            LinkB.Reset();
            _lastExportedL1Fault = null;
            _lastExportedL2Fault = null;
            _lastExportedIedFault = null;
            LinkA.PortName = _settings.PrimaryPortName;
            LinkB.PortName = _settings.BackupPortName;
            LinkA.LinkAddress = _settings.PrimaryLinkAddress;
            LinkB.LinkAddress = _settings.BackupLinkAddress;
        }

        private static void NormalizeSettings(NucSlaveSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.PrimaryLinkAddress <= 0)
            {
                settings.PrimaryLinkAddress = 1;
            }

            if (settings.BackupLinkAddress <= 0)
            {
                settings.BackupLinkAddress = settings.PrimaryLinkAddress;
            }
        }

        private NucPortEndpointState ResolveEndpoint(int linkNumber)
        {
            return linkNumber == 1 ? LinkA : linkNumber == 2 ? LinkB : null;
        }
    }
}
