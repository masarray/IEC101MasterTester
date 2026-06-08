using System;

namespace IecSlaveSimulator.Models
{
    public sealed class NucPortEndpointState
    {
        public NucEndpointId EndpointId { get; set; }
        public string PortName { get; set; }
        public int LinkAddress { get; set; }
        public bool IsConnected { get; set; }
        public bool IsLoopAlive { get; set; }
        public NucEndpointRole Role { get; set; }
        public NucSlaveLinkState State { get; set; }
        public DateTime? ConnectedAtUtc { get; set; }
        public DateTime? RecoveryStartedUtc { get; set; }
        public DateTime? LastRxUtc { get; set; }
        public DateTime? LastTxUtc { get; set; }
        public DateTime? LastValidMasterActivityUtc { get; set; }
        public DateTime? LastWorkerPulseUtc { get; set; }
        public int RxCount { get; set; }
        public int TxCount { get; set; }

        public void Reset()
        {
            IsConnected = false;
            IsLoopAlive = false;
            Role = NucEndpointRole.None;
            State = NucSlaveLinkState.Disconnected;
            ConnectedAtUtc = null;
            RecoveryStartedUtc = null;
            LastRxUtc = null;
            LastTxUtc = null;
            LastValidMasterActivityUtc = null;
            LastWorkerPulseUtc = null;
            RxCount = 0;
            TxCount = 0;
        }
    }
}
