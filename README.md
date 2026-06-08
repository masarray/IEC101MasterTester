# IEC101 Master Tester

Windows WPF `.NET Framework 4.8` IEC-60870-5-101 master tester and analyzer for SCADA FAT, gateway troubleshooting, NUC redundancy observation, SOE replay audit, and protocol evidence capture.

[![Platform](https://img.shields.io/badge/platform-Windows-1f6feb)](#build)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512bd4)](#build)
[![Protocol](https://img.shields.io/badge/protocol-IEC--60870--5--101-0f766e)](#what-it-does)
[![Stack](https://img.shields.io/badge/IEC--101%20stack-native%20clean--room-16a34a)](#native-clean-room-stack)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)

[Landing page](https://masarray.github.io/IEC101MasterTester/) | [Roadmap](ROADMAP.md) | [User manual](USER_MANUAL.md) | [Native migration notes](docs/NATIVE_CLEANROOM_MIGRATION.md) | [Pass 2 behavior parity](docs/NATIVE_CLEANROOM_PASS2.md) | [Pass 3 redundancy tuning](docs/NATIVE_CLEANROOM_PASS3.md) | [Pass 4 smart recovery](docs/NATIVE_CLEANROOM_PASS4.md)

![IEC101 Master Tester mission control](docs/assets/screenshot/mission-control.webp)

## Current Status

This repository now uses a **project-owned native clean-room IEC-101 communication stack** for the main tester path. The previous `lib60870.NET` vendor source tree has been removed from the source tree and from the project build.

Current state:

- `NativeCleanRoom` is the default communication engine.
- `NativeExperimental` remains as a compatibility enum value for older saved settings and controlled bench testing.
- The main WPF app, NUC redundancy channels, data mapper, line monitor formatter, and simulator no longer compile against `lib60870.NET` namespaces.
- The native stack includes FT1.2 fixed/variable frame codec, ASDU codec, IEC-101 profile handling, unbalanced master service, command encoding, GI/polling flow, line monitor integration, and protocol evidence capture.
- The native simulator has also been migrated to project-owned IEC-101 frame/ASDU code.

Important validation note: this migration pass removes the vendor dependency at source level, but the project still needs Windows MSBuild/Visual Studio validation and bench testing against real RTU/gateway/simulator before it should be called field-stable.

## What It Does

- IEC-101 serial master workflow for practical FAT and troubleshooting.
- Operator value viewer, event journal, status history, findings, availability telemetry, and command lifecycle tracking.
- Line Monitor with factual frame, COT, ACD, DFC, CASDU, IOA, and raw evidence visibility.
- NUC redundancy observation with link activity, switchover, continuity, and GI observation context.
- SOE/buffer replay audit with duplicate/FIFO/minimum-capacity checks.
- Protocol evidence ring buffer for golden trace and native-stack validation work.
- Lightweight UI snapshot buffering so long sessions do not retain large raw payload strings in WPF grids.

## Screenshots

| Mission Control | NUC Redundancy |
| --- | --- |
| ![Mission control](docs/assets/screenshot/mission-control.webp) | ![NUC redundancy](docs/assets/screenshot/nuc-redundancy.webp) |

| Line Monitor | SOE Buffer Audit |
| --- | --- |
| ![Line monitor](docs/assets/screenshot/line-monitor.webp) | ![SOE buffer audit](docs/assets/screenshot/soe-buffer-audit.webp) |

| Availability | Findings |
| --- | --- |
| ![Availability dashboard](docs/assets/screenshot/availability-dashboard.webp) | ![Findings dashboard](docs/assets/screenshot/findings-dashboard.webp) |

## PLN-Oriented Defaults

The default IEC-101 profile follows the working PLN/Pusertif-style baseline used in this repository:

- `1200 bps`
- `8E1`
- link address length `2`
- CASDU length `2`
- IOA length `3`
- originator address `0`
- link address `105`
- CASDU `105`

## Native Clean-Room Stack

The native stack lives under:

- `Services/Iec101/Native/Frames`
- `Services/Iec101/Native/Asdu`
- `Services/Iec101/Native/Master`
- `Services/Diagnostics/ProtocolEvidenceRecorder.cs`
- `Services/Diagnostics/ProtocolEvidenceExportService.cs`

Implemented in this migration pass:

- FT1.2 fixed frame, variable frame, and single-character ACK handling.
- Configurable link address, CASDU, IOA, and originator-address lengths.
- ASDU decode/encode for key monitor and command types used by FAT workflows.
- GI, Class 1/Class 2 polling, link-layer test, reset-link style startup, local-time clock sync, and command queue flow.
- Pass 2 behavior parity: FCB toggles only after valid responses, DFC applies busy backoff, ACD prioritizes Class 1 polling, and command ASDUs stay out of the process Value Viewer.
- Native mapping into existing `ValueViewerRow` and `LineMonitorRow` models.
- Protocol evidence export path for raw TX/RX validation.
- Native simulator response path for repeatable bench testing without vendor protocol code.

Still required before declaring field-stable:

- Windows MSBuild validation.
- Golden trace tests for FT1.2 and ASDU codec.
- Simulator interoperability test.
- Real RTU/gateway test covering GI, spontaneous/event retrieval, Class 1/Class 2 polling, command confirmation, select-before-operate, setpoint, and clock sync.
- Longer NUC redundancy soak test.

## Repository Guide

- `ROADMAP.md` - product and native-stack validation roadmap.
- `docs/NATIVE_CLEANROOM_MIGRATION.md` - migration notes and validation checklist.
- `docs/NATIVE_CLEANROOM_PASS2.md` / `docs/NATIVE_CLEANROOM_PASS3.md` / `docs/NATIVE_CLEANROOM_PASS4.md` - behavior-parity, redundancy tuning, and smart recovery notes for native NUC operation.
- `USER_MANUAL.md` - operator notes.
- `PROJECT_STRUCTURE.md` - source map.
- `docs/` - GitHub Pages landing page.
- `AGENTS.md` - contributor/development rules.

## Build

Use MSBuild on Windows with Visual Studio Build Tools / Visual Studio Community:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' IEC101MasterTester.csproj /t:Build /p:Configuration=Debug /p:UseSharedCompilation=false
```

Release build:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' IEC101MasterTester.csproj /t:Rebuild /p:Configuration=Release /p:UseSharedCompilation=false
```

## License

The repository is prepared for **Apache-2.0** after the removal of the previous GPL/commercial vendor protocol source tree. Before publishing a formal release, perform a final asset and dependency audit, especially for screenshots, icons, and any files copied from external sources.


### Native clean-room pass 5

Pass 5 adds smart startup bootstrap, application-image readiness tracking, and context-aware GI orchestration for NUC redundancy. See [`docs/NATIVE_CLEANROOM_PASS5.md`](docs/NATIVE_CLEANROOM_PASS5.md).

- [Native clean-room pass 6](docs/NATIVE_CLEANROOM_PASS6.md) / [Pass 7](docs/NATIVE_CLEANROOM_PASS7.md) — startup GI and NUC active arbitration.
