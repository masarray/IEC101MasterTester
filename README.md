# IEC101MasterTester

Lightweight WPF `.NET Framework 4.8` IEC-60870-5-101 master tester and analyzer for FAT, troubleshooting, and NUC redundancy observation.

## What This Project Is

This repository focuses on:
- protocol-correct IEC-101 communication through `lib60870.NET`
- practical operator workflow
- passive observability and diagnostics
- specialized windows for redundancy, SOE/buffer, availability, and findings

It is not a protocol lab that invents its own communication engine.

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
