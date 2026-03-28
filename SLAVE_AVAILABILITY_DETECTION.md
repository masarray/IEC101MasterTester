# Slave Availability Detection

## Purpose
This phase adds a passive slave availability analyzer for IEC-101 sessions.

It must:
- use only existing communication callbacks
- avoid false disconnect verdicts
- distinguish transport loss from silent/sluggish slave behavior
- feed availability dashboard and findings engine

It must not:
- create its own polling routine
- fabricate protocol facts
- relabel communication state beyond evidence windows

## Evidence Inputs
The state machine consumes only passive evidence already produced by the tester:
- connection state callback
- line monitor TX/RX rows
- ASDU presence from parsed RX rows
- error frame observations
- availability timing counters already tracked in `MainViewModel`

## States

### `Disconnected`
Transport is not connected or the service reported `Disconnected` / `Faulted`.

### `Connecting`
Connection is in transition and transport is not yet proven usable.

### `TransportUp`
Master session is connected, but there is not yet enough RX evidence to claim the slave is responsive.

### `LinkResponsive`
The slave is responding at frame level, but recent valid application data is not yet proven.

### `ApplicationResponsive`
Recent RX evidence includes valid ASDU/application activity.

### `NoApplicationData`
RX frames still arrive, but no valid ASDU has been seen within the configured application freshness window.

### `Silent`
Transport remains connected, but no RX frame has been observed within the silence window.

### `Degraded`
Transport is connected and the slave still answers sometimes, but repeated recent protocol/error evidence makes the session unhealthy.

## Transition Strategy

### Normal path
- `Disconnected` -> `Connecting`
- `Connecting` -> `TransportUp`
- `TransportUp` -> `LinkResponsive`
- `LinkResponsive` -> `ApplicationResponsive`

### Degraded paths
- `ApplicationResponsive` -> `NoApplicationData`
  when link remains active but valid ASDU freshness exceeds threshold
- `ApplicationResponsive` / `LinkResponsive` -> `Silent`
  when RX evidence disappears for too long
- `ApplicationResponsive` / `LinkResponsive` -> `Degraded`
  when repeated recent corrupt/error evidence crosses threshold

### Recovery paths
- `Silent` -> `LinkResponsive`
  when RX resumes
- `NoApplicationData` -> `ApplicationResponsive`
  when valid ASDU resumes
- `Degraded` -> `ApplicationResponsive`
  when fresh valid ASDU resumes and recent error pressure falls below threshold

## Default Thresholds
These are analyzer defaults, not protocol truths.

- `SlaveNoRxWindow = 5 s`
- `SlaveNoAsduWindow = 8 s`
- `RecentErrorWindow = 20 s`
- `RecentErrorDegradedThreshold = 3`

These should be treated as `ConfigurablePolicy` if future SPLN/vendor requirements define stricter values.

## Findings Policy
Only raise findings for meaningful unhealthy states, not every fluctuation.

### `SLAVE_SILENT`
Raised when session is still connected but no RX frame has been observed within `SlaveNoRxWindow`.

### `SLAVE_NO_APPLICATION_DATA`
Raised when frame-level RX remains active but valid application data is stale past `SlaveNoAsduWindow`.

### `SLAVE_CORRUPT_RESPONSE_PRESSURE`
Raised when recent protocol/error evidence within `RecentErrorWindow` reaches the degraded threshold.

Do not raise a special slave finding for ordinary connection callback `Disconnected`; that already exists as factual transport state.

## Dashboard Mapping

### Hero / summary
- use `ApplicationResponsive` as healthiest live state
- show human-readable state and detail

### Link State card
Show:
- `Slave state`
- `State detail`
- `Redundancy active link`
- `GI after switchover`

### Reliability score
Apply penalty from:
- reconnect count
- downtime
- protocol errors
- slave silent state
- no application data state
- degraded/corrupt response pressure

## Anti False-Disconnect Rules
- never declare slave disconnected from a single timeout
- never infer disconnect only from lack of spontaneous event
- require connection callback for factual transport disconnect
- classify stale/noisy behavior as `Silent`, `NoApplicationData`, or `Degraded` first

## Current Scope
This phase is for passive IEC-101 availability analysis only.

Out of scope for now:
- active watchdog traffic
- vendor-specific retry semantics
- per-transaction availability scoring
- redundant dual-link slave health merge logic
