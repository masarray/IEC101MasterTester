# IEC101MasterTester

Lightweight WPF `.NET Framework 4.8` IEC-60870-5-101 master tester and analyzer for FAT, troubleshooting, and NUC redundancy observation.

## What This Project Is

This repository focuses on:
- protocol-correct IEC-101 communication through `lib60870.NET`
- practical operator workflow
- passive observability and diagnostics
- specialized windows for redundancy, SOE/buffer, availability, and findings

It is not a protocol lab that invents its own communication engine.

## PLN Pusertif Baseline

Default communication baseline used across master/slave profiles:
- `1200 bps`
- `8E1`
- `Link Address Length = 2`
- `CAASDU Length = 2`
- `IOA Length = 3`
- `OA = 0`
- `Link Address = 105`
- `CAASDU = 105`

This baseline lives in:
- [Models/ConnectionSettings.cs](D:/CODEX/NewEx/IEC101MasterTester/Models/ConnectionSettings.cs)
- [IecSlaveSimulator/Models/SlaveConnectionSettings.cs](D:/CODEX/NewEx/IEC101MasterTester/IecSlaveSimulator/Models/SlaveConnectionSettings.cs)

## Class Data Semantics

Important analyzer rule:
- `Class 1 / Class 2` is not a literal IOA field.
- It is delivery context inferred from IEC-101 request/response flow.

Current intended interpretation:
- response to `FC10` -> `Class 1`
- response to `FC11` -> `Class 2`
- `GI` / `INTERROGATED_BY_STATION` -> `Class 2` delivery path
- `BACKGROUND_SCAN` / `PERIODIC` -> `Class 2`
- `Spontaneous` is event traffic and must not overwrite factual `COT`

Files that matter:
- [Services/Iec101/Iec101MasterService.cs](D:/CODEX/NewEx/IEC101MasterTester/Services/Iec101/Iec101MasterService.cs)
- [ViewModels/MainViewModel.cs](D:/CODEX/NewEx/IEC101MasterTester/ViewModels/MainViewModel.cs)
- [Models/ValueViewerRow.cs](D:/CODEX/NewEx/IEC101MasterTester/Models/ValueViewerRow.cs)
- [Models/LineMonitorRow.cs](D:/CODEX/NewEx/IEC101MasterTester/Models/LineMonitorRow.cs)

## Core Rules

- All IEC-101 communication must go through `lib60870.NET`.
- UI and analyzer layers are passive observers.
- `COT` comes from real callback data.
- `ACD` comes from real frame/control-bit evidence.
- If classification is uncertain, prefer `Unknown`.

Read [AGENTS.md](D:/CODEX/NewEx/IEC101MasterTester/AGENTS.md) first before making changes.

## Main Areas

- [MainWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/MainWindow.xaml): legacy master/analyzer workspace
- [Views/NucRedundancyWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/NucRedundancyWindow.xaml): main NUC operator workspace
- [Views/NucLinkTraceWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/NucLinkTraceWindow.xaml): 60-second tape-style link traffic trace
- [Views/BufferedEventAuditWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/BufferedEventAuditWindow.xaml): SOE/buffer audit
- [Views/AvailabilityDashboardWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/AvailabilityDashboardWindow.xaml): availability telemetry
- [Views/FindingsWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/FindingsWindow.xaml): analyzer findings
- [IecSlaveSimulator](D:/CODEX/NewEx/IEC101MasterTester/IecSlaveSimulator): RTU/slave simulator workspace

## Build

Use MSBuild:

```powershell
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'IEC101MasterTester.csproj' /t:Build /p:Configuration=Debug /p:UseSharedCompilation=false
```

## Operator/AI Docs

- [CODEX_HANDOFF.md](D:/CODEX/NewEx/IEC101MasterTester/CODEX_HANDOFF.md)
- [USER_MANUAL.md](D:/CODEX/NewEx/IEC101MasterTester/USER_MANUAL.md)
- [PROJECT_STRUCTURE.md](D:/CODEX/NewEx/IEC101MasterTester/PROJECT_STRUCTURE.md)
- [ROADMAP.md](D:/CODEX/NewEx/IEC101MasterTester/ROADMAP.md)
- [PROJECT_OVERVIEW_FOR_AI.md](D:/CODEX/NewEx/IEC101MasterTester/PROJECT_OVERVIEW_FOR_AI.md)
- [AI_QUICK_MAP.md](D:/CODEX/NewEx/IEC101MasterTester/AI_QUICK_MAP.md)
