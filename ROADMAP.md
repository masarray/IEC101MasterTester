# Roadmap

IEC101 Master Tester is moving from a feature-rich analyzer into a release-ready, native clean-room IEC-60870-5-101 tool for Windows FAT, SCADA troubleshooting, gateway verification, and NUC redundancy observation.

## Product Direction

The project must stay:

- protocol-correct
- operator-usable
- lightweight during long FAT sessions
- evidence-oriented
- legally clean for Apache-2.0 public open-source release

## Current Migration Status

Completed in the native clean-room pass:

- Removed the previous `Vendor/lib60870` source tree from the repository.
- Removed `lib60870` compile includes from the main WPF project and simulator project.
- Removed `lib60870.CS101` dependencies from mapper, line monitor formatter, main view model, and master-service routing.
- Promoted `NativeCleanRoom` as the default master engine.
- Routed main app and NUC redundancy channels through `Iec101MasterServiceRouter` backed by `NativeIec101MasterService`.
- Migrated simulator runtime to the same project-owned native FT1.2 and ASDU codec path.
- Added Apache-2.0 repository license files, pending final asset/dependency audit before official binary release.

## Native Stack Validation Roadmap

### 1. Build validation

Required before merging/releasing:

- Open in Visual Studio on Windows.
- Restore NuGet packages.
- Build Debug and Release.
- Confirm `0 Error(s)`.
- Review warnings manually; protocol-related warnings must be fixed, cosmetic warnings may be triaged.

### 2. Golden trace tests

Add test coverage for:

- FT1.2 single-character ACK, fixed frame, and variable frame decode.
- checksum rejection.
- configurable link-address length.
- ASDU Type ID, VSQ, COT, CASDU, IOA decode.
- monitor types: single point, double point, measured normalized/scaled/short float, step position, integrated total.
- command types: single command, double command, step command, normalized setpoint.
- CP24Time2a and CP56Time2a time decoding/encoding.

Recommended structure:

```text
tests/
  IEC101MasterTester.Tests/
    Native/
      Iec101FrameCodecTests.cs
      Iec101AsduCodecTests.cs
      NativeMasterTxFrameTests.cs
    Fixtures/
      golden-traces/
```

### 3. Simulator interoperability

Validate against the included native simulator:

- startup/reset-link behavior
- link-status request
- Class 1 poll while ACD is active
- Class 2 background poll
- general interrogation activation/confirmation/termination
- single/double/step command confirmation
- setpoint command confirmation
- spontaneous event queue behavior
- link timeout and reconnect behavior

### 4. Real equipment / gateway validation

Validate against at least one real IEC-101 controlled station or gateway:

- PLN-style 1200 bps 8E1 profile
- link address length 2
- CASDU length 2
- IOA length 3
- GI response completeness
- spontaneous event retrieval
- Class 1/Class 2 behavior
- command confirmation and negative confirmation
- select-before-operate timing
- clock synchronization confirmation
- long-session stability

### 5. NUC redundancy validation

Validate:

- only one communication owner is active at a time
- primary/backup link activity is separated clearly
- switchover does not duplicate command ownership
- GI after switchover is controlled and traceable
- availability dashboard does not invent protocol facts

## Release Readiness Roadmap

### P0 - merge blocker

- Build on Windows.
- Fix compile errors from native migration.
- Confirm no remaining source/build dependency on `lib60870`.
- Confirm `Vendor/lib60870` is not present.
- Update README and landing page to remove stale vendor-baseline wording.

### P1 - public release

- Add GitHub Actions Windows build workflow.
- Add portable ZIP release workflow.
- Add SHA256 checksum artifact.
- Add `CHANGELOG.md`.
- Add `SECURITY.md` and `CONTRIBUTING.md`.
- Add quick-start and troubleshooting docs.
- Add release notes with explicit validation status.

### P2 - engineering hardening

- Add tests and golden traces.
- Split `MainViewModel.cs` into smaller feature view models.
- Add structured diagnostics log.
- Add protocol compatibility matrix.
- Add bench-test scripts/checklists.
- Add release candidate soak-test checklist.

## Clean-Room Rule

Do not copy implementation code from `lib60870.NET`, lib60870 C, or other GPL/commercial stacks. Public IEC protocol documentation, interoperability behavior, and project-owned raw traces may be used as behavioral references. Any new protocol behavior should be implemented as project-owned code and backed by repeatable tests or captured evidence.
