# IEC101 Master Tester — IEC 60870-5-101 SCADA Master Tester

**IEC101 Master Tester** is a Windows desktop **IEC 60870-5-101 master tester, protocol analyzer, and FAT/SAT evidence workspace** for serial SCADA communication.

It helps SCADA, substation automation, commissioning, and RTU/gateway engineers verify **General Interrogation**, **Class 1 / Class 2 polling**, command feedback, SOE replay, NUC dual-link redundancy behavior, and decoded protocol traces in one practical Windows tool.

[![Windows Build](https://github.com/masarray/IEC101MasterTester/actions/workflows/windows-build.yml/badge.svg)](https://github.com/masarray/IEC101MasterTester/actions/workflows/windows-build.yml)
[![Release](https://img.shields.io/github/v/release/masarray/IEC101MasterTester?display_name=tag)](https://github.com/masarray/IEC101MasterTester/releases)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-1f6feb)](#download)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512bd4)](#build-from-source)
[![Protocol](https://img.shields.io/badge/protocol-IEC--60870--5--101-0f766e)](#what-the-application-does)

[Website](https://masarray.github.io/IEC101MasterTester/) · [Download](https://github.com/masarray/IEC101MasterTester/releases) · [Quick Start](docs/QUICK_START.md) · [User Manual](USER_MANUAL.md) · [FAQ](docs/FAQ.md) · [Troubleshooting](docs/TROUBLESHOOTING.md)

![IEC101 Master Tester mission control](docs/assets/screenshot/mission-control.webp)

## What the application does

IEC101 Master Tester connects to an **IEC 60870-5-101 slave, controlled station, outstation, RTU, or gateway** over serial communication and makes the protocol session visible in an engineer-friendly workspace. It is built for practical IEC-101 testing where engineers need readable values, decoded frames, command feedback, redundancy evidence, and repeatable screenshots for review.

Core capabilities:

- **IEC-101 master session** for unbalanced serial communication.
- **General Interrogation monitoring** with startup image readiness awareness.
- **Class 1 / Class 2 polling visibility** with ACD, DFC, COT, CASDU, IOA, and quality detail.
- **Value Viewer** for live process values.
- **Event Log** for command, spontaneous, GI, and diagnostic events.
- **Line Monitor** for frame-level evidence and raw protocol inspection.
- **NUC dual-link redundancy analyzer** for active/standby link testing, switchover observation, and recovery behavior.
- **Command workflow monitor** for single, double, regulating, and setpoint command tests.
- **SOE buffer audit** for event replay, duplicate detection, ordering review, and minimum-capacity checks.
- **48h availability dashboard** for long-session observation.
- **Findings window** for suspicious protocol behavior and evidence-oriented warnings.
- **Built-in slave simulator** for bench testing and demonstration without external equipment.

## Common use cases

Use IEC101 Master Tester when you need to:

- Test an **IEC 60870-5-101 master/slave serial link** during SCADA FAT or SAT preparation.
- Validate RTU, gateway, protection interface, or controlled-station behavior before site work.
- Confirm **General Interrogation** response, startup image readiness, and missing point behavior.
- Observe **Class 1 events**, **Class 2 background/cyclic data**, ACD, DFC, COT, CASDU, IOA, and quality flags.
- Verify single command, double command, regulating command, setpoint command, and feedback response.
- Review NUC-style active/standby link behavior, switchover evidence, and recovery after link interruption.
- Capture screenshots and traces for protocol troubleshooting reports, punch-list discussion, or internal engineering review.

## Who should use it

This project is useful for:

- SCADA engineers preparing or executing FAT, SAT, integration tests, or troubleshooting sessions.
- Substation automation engineers testing IEC-101 RTU, gateway, and controlled-station behavior.
- Commissioning engineers validating serial telecontrol links and command response.
- Protection/control engineers who need protocol evidence around indications, commands, SOE, and link redundancy.
- Developers building or validating IEC-101 integrations, simulators, gateways, or test benches.
- Teams that need a lightweight Windows tool for reproducible protocol screenshots and traces.

## Download

The easiest way to use the application is the Windows portable release package.

1. Open the [Releases](https://github.com/masarray/IEC101MasterTester/releases) page.
2. Download `IEC101MasterTester-<version>-windows-portable.zip`.
3. Extract the ZIP to a local folder, for example `D:\Tools\IEC101MasterTester`.
4. Run `IEC101MasterTester.exe`.
5. Optionally run `tools\IecSlaveSimulator\IecSlaveSimulator.exe` for a local bench test.

No installer is required for the portable package.

## Quick start

### Test with the built-in slave simulator

1. Start `IecSlaveSimulator.exe`.
2. Configure Link A and Link B COM ports.
3. Start the simulator runtime.
4. Start `IEC101MasterTester.exe`.
5. Open **NUC Redundancy** or **Single Link** mode.
6. Configure the same serial parameters and link/CASDU profile.
7. Start the session.
8. Confirm that Value Viewer, Event Log, and Line Monitor begin receiving data.

See [Quick Start](docs/QUICK_START.md) for the full walkthrough.

### Connect to real equipment

Before connecting to a real RTU, gateway, or controlled station:

- Use an approved test plan.
- Confirm the serial wiring, converter, port ownership, baud rate, parity, stop bits, link address, CASDU, IOA length, and command policy.
- Start in monitor/verification mode before issuing commands.
- Validate command behavior in a simulator or isolated test bay first.
- Capture Line Monitor evidence when reporting protocol findings.

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

## Professional FAT, SAT, and commissioning use

IEC101 Master Tester can be used as an engineering support tool for professional FAT, SAT preparation, commissioning checks, troubleshooting, and protocol evidence review.

Use it responsibly:

- Treat the tool as a tester/analyzer, not as a certified control system.
- Confirm every command workflow in a safe test boundary before live equipment use.
- Keep exported traces and screenshots as supporting evidence, not as the only acceptance record.
- Align acceptance criteria with the project specification, utility standard, interoperability profile, and approved test procedure.
- For official project records, pair the tool output with signed FAT/SAT forms and site-approved test reports.

See [Professional Use](docs/PROFESSIONAL_USE.md).

## Build from source

Requirements:

- Windows 10/11.
- Visual Studio 2022 or Visual Studio Build Tools with .NET desktop workload.
- .NET Framework 4.8 developer pack.

The main application uses framework assemblies only; no project package restore is required.

Build commands:

```powershell
msbuild IEC101MasterTester.csproj /t:Rebuild /p:Configuration=Release /p:UseSharedCompilation=false /m
msbuild IecSlaveSimulator\IecSlaveSimulator.csproj /restore /t:Rebuild /p:Configuration=Release /p:UseSharedCompilation=false /m
```

See [Build from Source](docs/BUILD_FROM_SOURCE.md) for details.


## GitHub automation

The repository is prepared for a clean `main`-only workflow:

- `Windows Build` runs on pushes and pull requests targeting `main`.
- `Deploy GitHub Pages` publishes the `docs/` website using GitHub Actions. No separate Pages branch is required.
- `Release Windows Portable` creates a portable ZIP and SHA256 checksum when a version tag such as `v0.1.0` is pushed.

See [Repository Setup](docs/REPOSITORY_SETUP.md) for the exact GitHub settings and commands.

## Repository guide

- `IEC101MasterTester.csproj` — main Windows WPF application.
- `IecSlaveSimulator/` — built-in IEC-101 slave simulator for bench testing.
- `Services/Iec101/Native/` — IEC-101 frame, ASDU, and master communication implementation.
- `Services/Redundancy/` — NUC dual-link redundancy session controller.
- `Services/Diagnostics/` — protocol evidence recording and export support.
- `ViewModels/` and `Views/` — WPF presentation layer.
- `docs/` — GitHub Pages website and user documentation.
- `.github/workflows/` — CI build, portable release, and GitHub Pages deployment workflows.

## GitHub repository metadata

Recommended repository metadata for discoverability:

- **Description:** `IEC 60870-5-101 Windows master tester for SCADA FAT/SAT, RTU gateway testing, NUC redundancy, commands, SOE audit, and protocol traces.`
- **Website:** `https://masarray.github.io/IEC101MasterTester/`
- **Topics:** `iec60870-5-101`, `iec101`, `iec-101`, `scada`, `substation-automation`, `rtu`, `gateway`, `protocol-analyzer`, `master-tester`, `serial-communication`, `fat-testing`, `sat-testing`, `commissioning`, `telecontrol`, `soe`, `wpf`, `dotnet`, `windows`, `apache-2-0`

## Documentation

- [Quick Start](docs/QUICK_START.md)
- [User Manual](USER_MANUAL.md)
- [FAQ](docs/FAQ.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Build from Source](docs/BUILD_FROM_SOURCE.md)
- [Professional Use](docs/PROFESSIONAL_USE.md)
- [Validation Guide](docs/VALIDATION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Repository Setup](docs/REPOSITORY_SETUP.md)
- [Release Checklist](docs/RELEASE_CHECKLIST.md)
- [Roadmap](ROADMAP.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

## License

IEC101 Master Tester is released under the [Apache License 2.0](LICENSE).

Third-party notices are recorded in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
