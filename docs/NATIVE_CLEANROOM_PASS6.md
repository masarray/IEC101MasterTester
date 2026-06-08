# Native clean-room pass 6 — NUC startup GI and active-link arbitration

Pass 6 fixes a startup behaviour where both serial links could look responsive, but the Value Viewer stayed empty until a link fault/failover occurred.

## Root cause

The dual-link slave arbiter was treating any received frame as active-master evidence. In hot-standby mode the standby link also receives link-layer supervision/test traffic. That standby supervision traffic could steal the slave-side active role from the real active master link before the startup GI was processed.

When that happened:

- the master sent startup GI on Link A,
- the slave had already marked Link B as active because it saw standby supervision traffic later,
- Link A's GI was acknowledged at link-layer but application handling was deferred,
- subsequent Class 1/Class 2 polls returned no data,
- the Value Viewer stayed empty until a failover made master/slave active-link selection match again.

## Changes

- Added protocol-aware slave active election.
- Standby link-layer supervision no longer steals the active slave role.
- Only application-bearing traffic can force the slave active endpoint:
  - Class 1 data request
  - Class 2 data request
  - variable primary ASDU traffic such as GI/commands
- Link health still observes all RX/TX frames, so standby supervision remains visible and recoverable.
- GI dispatch on the native master is now executed immediately when the active link worker is ready, instead of being only a queued single-slot command.
- Startup GI now arms Class 1 follow-up polling immediately after the link-layer ACK.

## Expected behaviour

On cold start:

1. Link A opens as active.
2. Link B opens as standby.
3. Link B supervision is allowed but does not steal application ownership.
4. Startup GI is sent on Link A.
5. Slave handles GI on Link A and queues GI response data.
6. Master drains response through Class 1 polling.
7. Value Viewer is populated without needing to disconnect Link A first.

