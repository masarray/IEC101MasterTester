# Native Clean-Room Pass 3 — Redundancy Behavior & Health UI

Pass 3 focuses on behavior parity after the native clean-room IEC-101 runtime became stable enough for master/slave redundancy testing.

## Main fixes

- Separates link **role** from link **health** in the NUC ribbon model.
  - Role: `Active` / `Standby`
  - Health: `Responsive` / `No Response` / `Timeout` / `Fault` / `Switching`
- The Link A / Link B outer border now follows health, not role only.
  - `Timeout`, `No Response`, and `Fault` render red.
  - `Switching` renders amber.
  - Responsive active link renders green.
  - Responsive standby link remains blue/cyan.
- Redundancy controller now publishes last failover metadata:
  - source channel
  - target channel
  - completion timestamp
  - failover latency in milliseconds
- Native hot-standby failover detection is tuned for FAT/testing behavior:
  - health monitor tick changed from 1000 ms to 500 ms
  - active response timeout window changed from `max(3000 ms, ResponseTimeout x4)` to `max(1500 ms, ResponseTimeout x2)`
  - standby supervision tick changed from 2 s to 1 s
- NUC communication chips now treat `NO RESPONSE` as red instead of neutral gray.
- Controller-driven active-link changes are now counted as switchover evidence, even when no external gateway fault point is available.

## Why this matters

The native stack was already able to communicate and switch links in pass 2. The remaining problem was visibility: a link could be demoted to standby or timeout internally without the ribbon border clearly showing the unhealthy state. Pass 3 makes the redundancy health model easier to trust during bench/FAT testing.

## Validation checklist

- Start dual-link redundancy.
- Verify Link A active / Link B standby.
- Disconnect or block Link A response.
- Link A should show red `Timeout` / `No Response` state and red outer border.
- Link B should promote to active.
- Switchover count and last switchover text should update.
- Failover latency should be visible in continuity/switchover text.
- Command path should still show `TX`, `OK`, and process feedback after switchover.
