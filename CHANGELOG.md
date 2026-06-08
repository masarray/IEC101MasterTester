# Changelog

## Native Clean-Room Pass 7 — Deterministic GI Drain & Application-Image Truth

- Added deterministic GI sequence on the active IEC-101 channel.
- Paused normal polling during GI bootstrap so Class 2 background traffic cannot mask an incomplete image.
- Drained Class 1 after C_IC_NA_1 until GI data / activation termination / bounded no-data is observed.
- Changed the native slave to acknowledge confirmed primary ASDUs after application handling so ACD reflects newly queued Class 1 data.
- Prevented Class 2 polling from hiding pending Class 1 traffic during GI/bootstrap.
- Tightened the NUC application-image model: background scan values are partial only; GI/interrogated values are required before the image is considered ready.


## Native clean-room pass 6 — startup GI and NUC active arbitration

- Fixed NUC cold-start issue where links were responsive but Value Viewer stayed empty.
- Changed slave active-link election so standby supervision traffic cannot steal active application ownership.
- Added protocol-aware active evidence based on Class 1/Class 2 polls and primary ASDU traffic.
- Kept link health based on all RX/TX frames so standby supervision still works.
- Made native GI dispatch immediate when the active worker is ready, with Class 1 follow-up armed after ACK.


## Native clean-room pass 5 - Smart startup bootstrap

- Added NUC application-image state tracking: Empty, Bootstrapping, Partial, Ready, Stale, and Failed.
- NUC cold start now sends startup GI when the application image is empty, even when GI policy is Optional.
- Post-failover GI is now context-aware: optional GI is skipped only when the image is still fresh.
- Disabled per-channel auto-GI in NUC mode so the redundancy controller owns bootstrap orchestration.
- Added bootstrap evidence rows to NUC Line Monitor / NUC Event Log.
- Cleared stale switchover timestamps at session start to prevent startup from appearing as a real failover.


## Native clean-room pass 4

- Added smart NUC redundancy recovery behavior for repeated disconnect/reconnect testing.
- Added master-side `Recovering` and `Reopening` channel states.
- Kept standby supervision armed after timeout instead of stopping the timer.
- Separated physical transport fault from IEC-101 protocol timeout/no-response.
- Added controller recovery probing when no viable link is available.
- Updated slave reconnect handling so stale timeout state and stale timestamps do not latch forever.
- Added `Recovering` slave endpoint state and UX health state.
- Updated NUC visual model so recovery appears as amber `RECOVERING`, distinct from red `TIMEOUT`/`FAULT`.


## Native clean-room pass 3

- Improved NUC redundancy behavior parity after native clean-room migration.
- Split Link A / Link B visual state into role and health so timeout/no-response conditions render red even after role changes.
- Tuned hot-standby failover detection for faster FAT-style switchover.
- Added controller failover latency metadata and controller-driven switchover evidence.
- Updated NUC communication indicators so NO RESPONSE is treated as a fault-level visual state.

# Changelog

## Native clean-room pass 6 — startup GI and NUC active arbitration

- Fixed NUC cold-start issue where links were responsive but Value Viewer stayed empty.
- Changed slave active-link election so standby supervision traffic cannot steal active application ownership.
- Added protocol-aware active evidence based on Class 1/Class 2 polls and primary ASDU traffic.
- Kept link health based on all RX/TX frames so standby supervision still works.
- Made native GI dispatch immediate when the active worker is ready, with Class 1 follow-up armed after ACK.


## Native clean-room pass 2

- Tightened native IEC-101 behavior parity after removing bundled lib60870 source.
- FCB now toggles only after a valid link-layer response.
- Startup worker polling is held until reset/link-status handshake is complete.
- DFC=1 now applies busy backoff; ACD=1 now prioritizes Class 1 polling.
- Command/GI/clock-sync ASDUs remain visible in line monitor/evidence but are no longer inserted into Value Viewer as process values.
- Clock sync now uses local wall-clock fields to better match the previous runtime behavior.
- Added `docs/NATIVE_CLEANROOM_PASS2.md` behavior-parity notes and validation checklist.
- Build workflows now compile both the main tester and the native slave simulator; release ZIP includes simulator under `tools/IecSlaveSimulator`.


## Unreleased

### Changed

- Migrated the main IEC-101 runtime path to the project-owned native clean-room stack.
- Removed the previous `Vendor/lib60870` source tree from the repository and build configuration.
- Routed main application startup and NUC redundancy channels through `Iec101MasterServiceRouter` backed by `NativeIec101MasterService`.
- Migrated data mapping and line monitor formatting away from vendor protocol types.
- Migrated the IEC slave simulator to shared native FT1.2 and ASDU code.
- Added Apache-2.0 license files, third-party notice notes, native migration notes, and Windows build/release workflows.

### Validation Required

- Windows MSBuild Debug/Release build.
- Native simulator interoperability test.
- Golden trace decoder/encoder test coverage.
- Real device/gateway bench validation.