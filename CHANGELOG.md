# Changelog

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