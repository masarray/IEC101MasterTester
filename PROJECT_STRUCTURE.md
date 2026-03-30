# PROJECT_STRUCTURE.md

## Top Level

- [AGENTS.md](D:/CODEX/NewEx/IEC101MasterTester/AGENTS.md): repo rules and constraints
- [README.md](D:/CODEX/NewEx/IEC101MasterTester/README.md): project entry summary
- [CODEX_HANDOFF.md](D:/CODEX/NewEx/IEC101MasterTester/CODEX_HANDOFF.md): latest implementation handoff
- [PROJECT_OVERVIEW_FOR_AI.md](D:/CODEX/NewEx/IEC101MasterTester/PROJECT_OVERVIEW_FOR_AI.md): AI continuity overview
- [AI_QUICK_MAP.md](D:/CODEX/NewEx/IEC101MasterTester/AI_QUICK_MAP.md): compact AI file map

## Main Application

- [App.xaml](D:/CODEX/NewEx/IEC101MasterTester/App.xaml)
- [App.xaml.cs](D:/CODEX/NewEx/IEC101MasterTester/App.xaml.cs)

## UI Layers

- [MainWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/MainWindow.xaml)
- [MainWindow.xaml.cs](D:/CODEX/NewEx/IEC101MasterTester/MainWindow.xaml.cs)
- [Views](D:/CODEX/NewEx/IEC101MasterTester/Views)
- [SharedUi](D:/CODEX/NewEx/IEC101MasterTester/SharedUi)
- [Controls](D:/CODEX/NewEx/IEC101MasterTester/Controls)

Important views:
- [Views/NucRedundancyWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/NucRedundancyWindow.xaml)
- [Views/NucLinkTraceWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/NucLinkTraceWindow.xaml)
- [Views/BufferedEventAuditWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/BufferedEventAuditWindow.xaml)
- [Views/AvailabilityDashboardWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/AvailabilityDashboardWindow.xaml)
- [Views/FindingsWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/FindingsWindow.xaml)

Important custom control:
- [Controls/NucLinkTraceTapeControl.cs](D:/CODEX/NewEx/IEC101MasterTester/Controls/NucLinkTraceTapeControl.cs)

## ViewModels

- [ViewModels/MainViewModel.cs](D:/CODEX/NewEx/IEC101MasterTester/ViewModels/MainViewModel.cs): central state coordinator
- [ViewModels/CommandLifeTrackerEngine.cs](D:/CODEX/NewEx/IEC101MasterTester/ViewModels/CommandLifeTrackerEngine.cs): command lifecycle logic

## Services

- [Services/Iec101](D:/CODEX/NewEx/IEC101MasterTester/Services/Iec101): IEC-101 communication services
- [Services/Redundancy](D:/CODEX/NewEx/IEC101MasterTester/Services/Redundancy): dual-link orchestration
- [Services/Profiles](D:/CODEX/NewEx/IEC101MasterTester/Services/Profiles): official point profile metadata
- [Services/Export](D:/CODEX/NewEx/IEC101MasterTester/Services/Export)
- [Services/Settings](D:/CODEX/NewEx/IEC101MasterTester/Services/Settings)
- [Services/Soe](D:/CODEX/NewEx/IEC101MasterTester/Services/Soe)

Most important service files:
- [Services/Iec101/Iec101MasterService.cs](D:/CODEX/NewEx/IEC101MasterTester/Services/Iec101/Iec101MasterService.cs)
- [Services/Iec101/Iec101DataMapper.cs](D:/CODEX/NewEx/IEC101MasterTester/Services/Iec101/Iec101DataMapper.cs)
- [Services/Redundancy/NucRedundancyService.cs](D:/CODEX/NewEx/IEC101MasterTester/Services/Redundancy/NucRedundancyService.cs)

## Models

- [Models](D:/CODEX/NewEx/IEC101MasterTester/Models): shared data contracts

Common examples:
- `LineMonitorRow`
- `EventLogRow`
- `FindingRow`
- `BufferReplaySession`
- `NucRedundancySettings`
- `PointDefinition`

## Vendor

- [Vendor/lib60870](D:/CODEX/NewEx/IEC101MasterTester/Vendor/lib60870): vendor source tree

Rule:
- do not compile vendor `obj/bin` artifacts

## Slave Simulator

- [IecSlaveSimulator](D:/CODEX/NewEx/IEC101MasterTester/IecSlaveSimulator): separate simulator workspace inside the same repo

## Current Hot Files

These are the best files to inspect first for current NUC link trace work:
- [Views/NucLinkTraceWindow.xaml.cs](D:/CODEX/NewEx/IEC101MasterTester/Views/NucLinkTraceWindow.xaml.cs)
- [Controls/NucLinkTraceTapeControl.cs](D:/CODEX/NewEx/IEC101MasterTester/Controls/NucLinkTraceTapeControl.cs)
- [Views/NucRedundancyWindow.xaml.cs](D:/CODEX/NewEx/IEC101MasterTester/Views/NucRedundancyWindow.xaml.cs)
