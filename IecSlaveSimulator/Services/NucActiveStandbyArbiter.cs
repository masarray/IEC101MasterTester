using System;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class NucActiveStandbyArbiter
    {
        public NucEndpointId ActiveEndpoint { get; private set; }

        public void Reset()
        {
            ActiveEndpoint = NucEndpointId.None;
        }

        public void Evaluate(NucPortEndpointState linkA, NucPortEndpointState linkB)
        {
            if (linkA == null || linkB == null)
            {
                ActiveEndpoint = NucEndpointId.None;
                return;
            }

            bool aHealthy = IsHealthy(linkA);
            bool bHealthy = IsHealthy(linkB);

            if (ActiveEndpoint == NucEndpointId.LinkA && !aHealthy)
            {
                ActiveEndpoint = bHealthy ? NucEndpointId.LinkB : NucEndpointId.None;
            }
            else if (ActiveEndpoint == NucEndpointId.LinkB && !bHealthy)
            {
                ActiveEndpoint = aHealthy ? NucEndpointId.LinkA : NucEndpointId.None;
            }
            else if (ActiveEndpoint == NucEndpointId.None)
            {
                ActiveEndpoint = SelectPreferredHealthy(linkA, linkB, aHealthy, bHealthy);
            }

            if (ActiveEndpoint == NucEndpointId.None)
            {
                ApplyRoles(linkA, linkB, NucEndpointRole.None, NucEndpointRole.None);
                return;
            }

            if (ActiveEndpoint == NucEndpointId.LinkA)
            {
                ApplyRoles(linkA, linkB, NucEndpointRole.Active, bHealthy ? NucEndpointRole.Standby : NucEndpointRole.None);
            }
            else
            {
                ApplyRoles(linkA, linkB, aHealthy ? NucEndpointRole.Standby : NucEndpointRole.None, NucEndpointRole.Active);
            }
        }

        private static NucEndpointId SelectPreferredHealthy(NucPortEndpointState linkA, NucPortEndpointState linkB, bool aHealthy, bool bHealthy)
        {
            if (aHealthy && !bHealthy)
            {
                return NucEndpointId.LinkA;
            }

            if (bHealthy && !aHealthy)
            {
                return NucEndpointId.LinkB;
            }

            if (aHealthy && bHealthy)
            {
                DateTime aSeen = linkA.LastValidMasterActivityUtc ?? DateTime.MinValue;
                DateTime bSeen = linkB.LastValidMasterActivityUtc ?? DateTime.MinValue;
                return aSeen >= bSeen ? NucEndpointId.LinkA : NucEndpointId.LinkB;
            }

            return NucEndpointId.None;
        }

        private static bool IsHealthy(NucPortEndpointState endpoint)
        {
            return endpoint.IsConnected
                && endpoint.State != NucSlaveLinkState.Timeout
                && endpoint.State != NucSlaveLinkState.Faulted
                && endpoint.State != NucSlaveLinkState.Disconnected;
        }

        private static void ApplyRoles(NucPortEndpointState linkA, NucPortEndpointState linkB, NucEndpointRole roleA, NucEndpointRole roleB)
        {
            linkA.Role = roleA;
            linkB.Role = roleB;

            linkA.State = ResolveRoleState(linkA, roleA);
            linkB.State = ResolveRoleState(linkB, roleB);
        }

        private static NucSlaveLinkState ResolveRoleState(NucPortEndpointState endpoint, NucEndpointRole role)
        {
            if (!endpoint.IsConnected)
            {
                return NucSlaveLinkState.Disconnected;
            }

            if (endpoint.State == NucSlaveLinkState.Timeout || endpoint.State == NucSlaveLinkState.Faulted)
            {
                return endpoint.State;
            }

            switch (role)
            {
                case NucEndpointRole.Active:
                    return NucSlaveLinkState.ActivePolling;
                case NucEndpointRole.Standby:
                    return NucSlaveLinkState.StandbyReady;
                default:
                    return endpoint.IsConnected ? NucSlaveLinkState.StandbyReady : NucSlaveLinkState.Disconnected;
            }
        }
    }
}
