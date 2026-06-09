# IEC101 Master Tester

Windows desktop master tester and analyzer for **IEC 60870-5-101** serial communication workflows.

It is designed for engineers who need a practical tool for SCADA FAT, SAT preparation, gateway testing, RTU troubleshooting, NUC dual-link redundancy observation, command verification, SOE audit, and protocol evidence capture.

[![Windows Build](https://github.com/masarray/IEC101MasterTester/actions/workflows/windows-build.yml/badge.svg)](https://github.com/masarray/IEC101MasterTester/actions/workflows/windows-build.yml)
[![Release](https://img.shields.io/github/v/release/masarray/IEC101MasterTester?display_name=tag)](https://github.com/masarray/IEC101MasterTester/releases)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-1f6feb)](#download)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512bd4)](#build-from-source)
[![Protocol](https://img.shields.io/badge/protocol-IEC--60870--5--101-0f766e)](#what-the-application-does)

[Website](https://masarray.github.io/IEC101MasterTester/) · [Download](https://github.com/masarray/IEC101MasterTester/releases) · [Quick Start](docs/QUICK_START.md) · [User Manual](USER_MANUAL.md) · [FAQ](docs/FAQ.md) · [Troubleshooting](docs/TROUBLESHOOTING.md)

![IEC101 Master Tester mission control](docs/assets/screenshot/mission-control.webp)

## What the application does

IEC101 Master Tester connects to an IEC 60870-5-101 slave/outstation over serial communication and makes the session visible in an engineer-friendly workspace.

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

## Who should use it

This project is useful for:

- SCADA engineers preparing or executing FAT/SAT.
- Substation automation engineers testing RTU/gateway IEC-101 behavior.
- Commissioning engineers validating serial telecontrol links.
- Protection/control engineers who need protocol evidence around indications, commands, and SOE.
- Developers building or validating IEC-101 integrations.
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
- NuGet.

Build commands:

```powershell
nuget restore IEC101MasterTester.csproj -PackagesDirectory packages
msbuild IEC101MasterTester.csproj /t:Rebuild /p:Configuration=Release /p:UseSharedCompilation=false /m
msbuild IecSlaveSimulator\IecSlaveSimulator.csproj /restore /t:Rebuild /p:Configuration=Release /p:UseSharedCompilation=false /m
```

See [Build from Source](docs/BUILD_FROM_SOURCE.md) for details.

## Repository guide

- `IEC101MasterTester.csproj` — main Windows WPF application.
- `IecSlaveSimulator/` — built-in IEC-101 slave simulator for bench testing.
- `Services/Iec101/Native/` — IEC-101 frame, ASDU, and master communication implementation.
- `Services/Redundancy/` — NUC dual-link redundancy session controller.
- `Services/Diagnostics/` — protocol evidence recording and export support.
- `ViewModels/` and `Views/` — WPF presentation layer.
- `docs/` — GitHub Pages website and user documentation.
- `.github/workflows/` — CI build, portable release, and GitHub Pages deployment workflows.

## Documentation

- [Quick Start](docs/QUICK_START.md)
- [User Manual](USER_MANUAL.md)
- [FAQ](docs/FAQ.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Build from Source](docs/BUILD_FROM_SOURCE.md)
- [Professional Use](docs/PROFESSIONAL_USE.md)
- [Validation Guide](docs/VALIDATION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](ROADMAP.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

## License

IEC101 Master Tester is released under the [Apache License 2.0](LICENSE).

Third-party package and asset notes are recorded in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
