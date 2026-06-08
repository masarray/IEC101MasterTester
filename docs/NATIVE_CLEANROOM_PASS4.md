# Native clean-room pass 4 — context-aware NUC recovery

Pass 4 hardens the native clean-room IEC 60870-5-101 redundancy runtime after repeated slave disconnect/reconnect testing.

## Goal

Make the master tester behave like a field-aware redundancy analyzer:

- distinguish physical serial transport state from IEC-101 protocol responsiveness;
- keep standby recovery probes alive after timeout;
- avoid terminal timeout latches after slave reconnect;
- keep the UX aligned with the actual runtime condition: open, timeout, recovering, reopening, active, or standby;
- recover from repeated link fault injection without requiring a full application restart.

## Master-side changes

- Added explicit `Recovering` and `Reopening` channel states.
- Added `INucLinkChannel.RecoverAsync(...)` for controller-driven recovery probes.
- Protocol timeout no longer automatically means `PORT CLOSED`.
- Standby supervision timer remains armed after timeout; it keeps probing instead of stopping permanently.
- Controller now enters `Recovering` when no viable link is available, and schedules recovery probes instead of dead-ending at `NoAvailableLink`.
- If the active link is healthy but standby is unhealthy, the controller marks the session `Degraded` and keeps repairing the standby channel in the background.
- Recovery evidence is reported to the session state so the UI can show `RECOVERING` instead of misleading closed/no-response states.

## Slave-side changes

- Slave reconnect clears stale RX/TX timestamps so old timeout evidence does not immediately poison a new port session.
- Reconnected links enter `Recovering` until the first valid master frame arrives.
- Timeout is no longer a permanent latch; valid master activity moves the endpoint back to standby-ready/active-polling.
- Gateway fault signals now follow current master activity rather than old stale timestamps.

## Expected behavior after repeated fault injection

1. Disconnect Link B on slave.
2. Master marks Link B timeout/recovering while keeping the active Link A alive.
3. Reconnect Link B on slave.
4. Link B shows recovering/awaiting master, then returns to standby-ready after the next supervision response.
5. Disconnect Link A.
6. Link B is promoted and resumes application traffic without needing a full master/slave restart.

## Validation checklist

- [ ] Link B timeout does not permanently lock Link B.
- [ ] Link B returns from timeout after slave reconnect and master supervision probe.
- [ ] Link A protocol timeout does not appear as `PORT CLOSED` unless the serial port is actually closed.
- [ ] Both unhealthy links show controller `Recovering`, not a dead session.
- [ ] Command and spontaneous events still work after switchover.
- [ ] Repeated disconnect/reconnect cycles do not require app restart.
