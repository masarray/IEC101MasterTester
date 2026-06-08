# Native Clean-Room IEC-101 Migration

This note documents the migration work that removes the previous `lib60870.NET` source dependency and promotes the project-owned IEC-101 stack as the active runtime path.

## Why This Migration Matters

The original vendor stack was useful as a known-good baseline, but it created a licensing and repository-positioning problem for an Apache-2.0 public open-source project. The native migration makes the repository easier to publish, audit, test, and extend without bundling third-party protocol source code.

## Migration Scope Completed

### Source and build cleanup

- Removed `Vendor/lib60870` from the repository.
- Removed vendor compile includes from `IEC101MasterTester.csproj`.
- Removed vendor compile includes from `IecSlaveSimulator/IecSlaveSimulator.csproj`.
- Removed the old `Services/Iec101/Iec101MasterService.cs` vendor-backed implementation.
- Removed the Release-time Inno Setup target that referenced a missing installer script.

### Runtime routing

- `Iec101MasterServiceRouter` now owns a single native runtime path backed by `NativeIec101MasterService`.
- App startup uses `IIec101MasterService` and `Iec101MasterServiceRouter`.
- NUC redundancy channels use `Iec101MasterServiceRouter` for both main and backup links.
- `NativeCleanRoom` is the default master engine in `ConnectionSettings`.

### Native protocol model

The native stack now includes:

- FT1.2 frame model and codec.
- primary/secondary control field handling.
- application profile model for link address, CASDU, IOA, and originator-address lengths.
- ASDU model, codec, Type ID enum, COT enum, quality descriptor, and information object model.
- monitor-type decoding for core FAT value/event workflows.
- command-type encoding/decoding for single, double, step, setpoint, interrogation, and clock-sync workflows.

### UI/data integration

- `Iec101DataMapper` now maps native `Iec101Asdu` objects directly into value rows.
- `LineMonitorFormatter` now formats native ASDU/frame facts without vendor types.
- `MainViewModel` command/COT parsing now uses native enums.
- Protocol evidence recording now uses native application-profile construction.

### Native simulator

The IEC slave simulator now uses shared native frame and ASDU code through linked source files. It simulates:

- serial unbalanced slave response behavior
- Class 1/Class 2 queues
- GI response flow
- command confirmation/rejection
- background and spontaneous monitor values
- link activity callbacks for UI testing

## What Still Needs Validation

This pass is a source-level migration. It has not been proven field-stable until these gates pass:

1. Windows Visual Studio/MSBuild Debug and Release build.
2. Native simulator end-to-end test.
3. Golden trace decoder/encoder tests.
4. Real IEC-101 device or gateway test.
5. NUC redundancy soak test.
6. Asset/dependency license audit before official Apache-2.0 binary release.

## Suggested Test Matrix

| Area | Test | Expected Result |
|---|---|---|
| Link startup | Connect to simulator | link opens, reset/link-status flow visible |
| GI | Send station GI | ACT_CON, monitor ASDUs, ACT_TERM visible |
| Class 1 | Enqueue event in simulator | ACD observed, Class 1 poll retrieves event |
| Class 2 | Background polling | periodic monitor values visible without flooding UI |
| Single command | direct and select/execute | command confirmation and value feedback visible |
| Double command | direct and select/execute | correct double-point state feedback |
| Setpoint | normalized command | confirmation and feedback row visible |
| Clock sync | send CP56 time | command confirmation visible |
| NUC | switch main/backup | only active channel owns communication |
| Evidence | export evidence | raw TX/RX rows available for replay |

## Known Risk Areas

- IEC-101 implementations differ in timing tolerance, FCB/FCV strictness, ACK usage, and negative confirmation behavior.
- Real devices may require special reset-link or link-status sequencing.
- Some devices treat Class 2 polling aggressively if idle delay is too short.
- Select-before-operate timeout behavior must be tested against real controlled stations.
- CP24/CP56 timestamp interpretation must be verified with timezone and daylight-saving assumptions.

## Final Field Truth

Removing the vendor stack is the right strategic move, but protocol confidence must come from repeatable evidence. The next serious engineering milestone is not another UI feature; it is a build-green native stack plus golden traces proving that the new code reads and writes IEC-101 frames correctly.
